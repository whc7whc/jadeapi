// Services/HangfireScheduleService.cs
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Team.Backend.Models;
using Team.Backend.Models.EfModel;
using System.IO;
using System.Linq;

namespace Team.Backend.Services
{
    public class HangfireScheduleService : IScheduleService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HangfireScheduleService>? _logger;
        private readonly INotificationEmailSender? _emailSender;
        private readonly IConfiguration _configuration;

        public HangfireScheduleService(
            AppDbContext context,
            ILogger<HangfireScheduleService>? logger = null,
            INotificationEmailSender? emailSender = null,
            IConfiguration configuration = null)
        {
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        private void LogInfo(string message)
        {
            if (_logger != null)
                _logger.LogInformation(message);
            else
                Console.WriteLine(message);
        }
        private void LogWarn(string message)
        {
            if (_logger != null)
                _logger.LogWarning(message);
            else
                Console.WriteLine("[Warn] " + message);
        }
        private void LogError(Exception ex, string message)
        {
            if (_logger != null)
                _logger.LogError(ex, message);
            else
                Console.WriteLine($"[Error] {message}: {ex.Message}");
        }

        public async Task<ScheduleResult> ScheduleTaskAsync(string contentType, int contentId, DateTime scheduledTime, int userId, string actionType = "publish")
        {
            try
            {
                // 檢查內容是否存在
                if (contentType == "notification")
                {
                    var notification = await _context.Notifications.FindAsync(contentId);
                    if (notification == null)
                    {
                        LogWarn($"排程失敗：找不到通知 ID={contentId}");
                        return ScheduleResult.ErrorResult("找不到指定的通知");
                    }

                    // 檢查通知是否有必要欄位
                    if (string.IsNullOrEmpty(notification.Email_Address))
                    {
                        LogWarn($"排程失敗：通知缺少郵件地址 ID={contentId}");
                        return ScheduleResult.ErrorResult("通知缺少必要的郵件地址");
                    }

                    // 檢查通知狀態是否允許排程
                    if (notification.Email_Status != "draft" && notification.Email_Status != "pending" && notification.Email_Status != "immediate")
                    {
                        LogWarn($"排程失敗：通知狀態不允許排程 ID={contentId}, Status={notification.Email_Status}");
                        return ScheduleResult.ErrorResult("只能排程草稿、待發送或立即發送狀態的通知");
                    }

                    // 處理立即發送的情況
                    if (notification.Email_Status == "immediate")
                    {
                        LogInfo($"準備立即發送通知 ID={contentId}");
                        // 直接執行發送任務，不創建排程記錄
                        bool success = await SendNotificationImmediately(notification);
                        if (success)
                        {
                            LogInfo($"立即發送通知成功 ID={contentId}");
                            return ScheduleResult.SuccessResult("immediate");
                        }
                        else
                        {
                            LogWarn($"立即發送通知失敗 ID={contentId}");
                            return ScheduleResult.ErrorResult("立即發送通知失敗");
                        }
                    }
                }
                else if (contentType == "official_post")
                {
                    var post = await _context.OfficialPosts.FindAsync(contentId);
                    if (post == null)
                    {
                        LogWarn($"排程失敗：找不到文章 ID={contentId}");
                        return ScheduleResult.ErrorResult("找不到指定的文章");
                    }
                }
                else if (contentType == "coupon")
                {
                    var coupon = await _context.Coupons.FindAsync(contentId);
                    if (coupon == null)
                    {
                        LogWarn($"排程失敗：找不到優惠券 ID={contentId}");
                        return ScheduleResult.ErrorResult("找不到指定的優惠券");
                    }
                }

                // 建立排程記錄
                var schedule = new ContentPublishingSchedule
                {
                    ContentType = contentType,
                    ContentId = contentId,
                    ActionType = actionType, // ✅ 使用傳入的動作類型
                    ScheduledTime = scheduledTime,
                    Status = "pending",
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                };

                _context.ContentPublishingSchedules.Add(schedule);
                await _context.SaveChangesAsync();

                // 建立 Hangfire 任務
                var jobId = BackgroundJob.Schedule(
                    () => ExecuteTaskJob(schedule.Id, contentId, contentType),
                    scheduledTime
                );

                LogInfo($"排程建立成功: Type={contentType}, ID={contentId}, Action={actionType}, Schedule={schedule.Id}, JobId={jobId}");

                return ScheduleResult.SuccessResult(schedule.Id.ToString());
            }
            catch (Exception ex)
            {
                LogError(ex, $"建立排程時發生錯誤: Type={contentType}, ID={contentId}, Action={actionType}");
                return ScheduleResult.ErrorResult($"建立排程時發生錯誤: {ex.Message}");
            }
        }

        public async Task<bool> CancelScheduleAsync(int scheduleId)
        {
            try
            {
                var schedule = await _context.ContentPublishingSchedules.FindAsync(scheduleId);
                if (schedule == null || schedule.Status != "pending")
                {
                    LogWarn($"取消排程失敗：排程不存在或狀態不是待執行 ID={scheduleId}");
                    return false;
                }

                schedule.Status = "cancelled";
                //schedule.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                LogInfo($"排程已取消 ID={scheduleId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, $"取消排程時發生錯誤 ID={scheduleId}");
                return false;
            }
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ExecuteTaskJob(int scheduleId, int contentId, string contentType)
        {
            LogInfo($"開始執行任務: Schedule={scheduleId}, Type={contentType}, ID={contentId}");

            using var context = new AppDbContext(GetDbContextOptions());
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var schedule = await context.ContentPublishingSchedules.FindAsync(scheduleId);
                if (schedule == null || schedule.Status != "pending")
                {
                    LogWarn($"排程已被取消或不存在 ID={scheduleId}");
                    return;
                }

                // 執行對應的任務
                switch (contentType.ToLower())
                {
                    case "official_post":
                        await ExecuteArticlePublish(context, contentId);
                        break;
                    case "notification":
                        await ExecuteNotificationSend(context, contentId);
                        break;
                    case "coupon":
                        await ExecuteCouponAction(context, contentId, schedule.ActionType);
                        break;
                    default:
                        throw new ArgumentException($"不支援的內容類型: {contentType}");
                }

                // 更新排程狀態
                schedule.Status = "executed";
                schedule.ExecutedAt = DateTime.Now;
                //schedule.UpdatedAt = DateTime.Now;
                await context.SaveChangesAsync();

                await transaction.CommitAsync();
                LogInfo($"任務執行成功 Schedule={scheduleId}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var schedule = await context.ContentPublishingSchedules.FindAsync(scheduleId);
                if (schedule != null)
                {
                    schedule.Status = "failed";
                    schedule.ErrorMessage = ex.Message;
                    //schedule.UpdatedAt = DateTime.Now;
                    await context.SaveChangesAsync();
                }

                LogError(ex, $"任務執行失敗 Schedule={scheduleId}");
                throw;
            }
        }

        // ✅ 修正：處理優惠券發送動作
        private async Task ExecuteCouponAction(AppDbContext context, int couponId, string actionType)
        {
            var coupon = await context.Coupons.FindAsync(couponId);
            if (coupon == null)
            {
                throw new InvalidOperationException($"找不到指定的優惠券 ID={couponId}");
            }

            // 執行優惠券發送
            await ExecuteCouponDispatch(context, coupon, actionType);
        }

        // ✅ 執行優惠券發送
        private async Task ExecuteCouponDispatch(AppDbContext context, Coupon coupon, string memberLevelTarget)
        {
            switch (memberLevelTarget)
            {
                case "all":
                    // 發送給全部會員
                    await DispatchCouponToMemberLevel(context, coupon, null, "全部");
                    break;

                default:
                    // 嘗試解析為會員等級ID
                    if (int.TryParse(memberLevelTarget, out int levelId))
                    {
                        var level = await context.MembershipLevels.FindAsync(levelId);
                        if (level != null)
                        {
                            await DispatchCouponToMemberLevel(context, coupon, levelId, level.LevelName);
                        }
                        else
                        {
                            LogWarn($"找不到會員等級 ID: {levelId}");
                            throw new InvalidOperationException($"找不到會員等級 ID: {levelId}");
                        }
                    }
                    else
                    {
                        LogWarn($"無效的會員等級設定: {memberLevelTarget}");
                        throw new InvalidOperationException($"無效的會員等級設定: {memberLevelTarget}");
                    }
                    break;
            }

            coupon.UpdatedAt = DateTime.Now;
        }

        // ✅ 新增：發送優惠券給指定會員等級的方法（複製自 BasicScheduleService）
        private async Task DispatchCouponToMemberLevel(AppDbContext context, Coupon coupon, int? targetLevelId, string levelName)
        {
            try
            {
                // 查詢目標會員
                var targetMembers = await context.Members
                    .Include(m => m.LevelNavigation)
                    .Where(m => m.IsActive &&
                               (targetLevelId == null || m.Level == targetLevelId))
                    .ToListAsync();

                int successCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                foreach (var member in targetMembers)
                {
                    try
                    {
                        // 檢查是否已經擁有此優惠券
                        var existingCoupon = await context.MemberCoupons
                            .FirstOrDefaultAsync(mc => mc.MemberId == member.Id &&
                                                     mc.CouponId == coupon.Id &&
                                                     mc.Status == "active");

                        if (existingCoupon != null)
                        {
                            skippedCount++;
                            continue;
                        }

                        // 生成驗證碼
                        string verificationCode;
                        do
                        {
                            verificationCode = GenerateVerificationCode();
                        }
                        while (await context.MemberCoupons.AnyAsync(mc => mc.VerificationCode == verificationCode));

                        // 創建會員優惠券記錄
                        var memberCoupon = new MemberCoupon
                        {
                            MemberId = member.Id,
                            CouponId = coupon.Id,
                            Status = "active",
                            AssignedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            VerificationCode = verificationCode
                        };

                        context.MemberCoupons.Add(memberCoupon);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, $"發送優惠券給會員 {member.Id} 失敗");
                        errorCount++;
                    }
                }

                await context.SaveChangesAsync();

                LogInfo($"🎫 優惠券「{coupon.Title}」發送完成");
                LogInfo($"   目標等級：{levelName}");
                LogInfo($"   成功：{successCount} 張");
                LogInfo($"   跳過：{skippedCount} 張");
                LogInfo($"   錯誤：{errorCount} 張");
            }
            catch (Exception ex)
            {
                LogError(ex, "批量發送優惠券失敗");
                throw;
            }
        }

        // ✅ 新增：生成驗證碼的方法
        private string GenerateVerificationCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task ExecuteArticlePublish(AppDbContext context, int articleId)
        {
            var article = await context.OfficialPosts.FindAsync(articleId);
            if (article == null)
            {
                throw new InvalidOperationException($"找不到指定的文章 ID={articleId}");
            }

            article.Status = "published";
            article.PublishedAt = DateTime.Now;
            await context.SaveChangesAsync();

            LogInfo($"文章已發布 ID={articleId}, Title={article.Title}");
        }

        private async Task ExecuteNotificationSend(AppDbContext context, int notificationId)
        {
            var notification = await context.Notifications.FindAsync(notificationId);
            if (notification == null)
            {
                throw new InvalidOperationException($"找不到指定的通知 ID={notificationId}");
            }

            // 檢查是否有收件人
            if (string.IsNullOrEmpty(notification.Email_Address))
            {
                throw new InvalidOperationException("通知缺少收件人郵件地址");
            }

            bool emailSent = await SendEmailNotification(notification);

            // 更新通知狀態
            notification.Email_Status = emailSent ? "sent" : "failed";
            notification.Updated_At = DateTime.Now;
            notification.Email_Sent_At = emailSent ? DateTime.Now : null;

            // 如果發送失敗，增加重試計數
            if (!emailSent)
            {
                notification.Email_Retry += 1;
            }

            await context.SaveChangesAsync();

            LogInfo($"通知狀態已更新 ID={notificationId}, Status={notification.Email_Status}, Email={notification.Email_Address}");
        }

        // 立即發送通知的方法
        private async Task<bool> SendNotificationImmediately(Notification notification)
        {
            try
            {
                bool emailSent = await SendEmailNotification(notification);

                // 更新通知狀態
                notification.Email_Status = emailSent ? "sent" : "failed";
                notification.Updated_At = DateTime.Now;
                notification.Email_Sent_At = emailSent ? DateTime.Now : null;

                // 如果發送失敗，增加重試計數
                if (!emailSent)
                {
                    notification.Email_Retry += 1;
                }

                await _context.SaveChangesAsync();

                LogInfo($"立即發送通知結果: {(emailSent ? "成功" : "失敗")} ID={notification.Id}, Email={notification.Email_Address}");
                return emailSent;
            }
            catch (Exception ex)
            {
                LogError(ex, $"立即發送通知時發生錯誤 ID={notification.Id}");
                return false;
            }
        }

        // 統一的郵件發送方法
        private async Task<bool> SendEmailNotification(Notification notification)
        {
            if (notification.Channel?.ToLower() != "email")
            {
                LogInfo($"跳過郵件發送，通知管道為 {notification.Channel} ID={notification.Id}");
                return true; // 對於非email管道的通知，直接標記為成功
            }

            try
            {
                // 優先使用注入的郵件發送服務
                if (_emailSender != null)
                {
                    bool result = await _emailSender.SendNotificationEmailAsync(notification);
                    LogInfo($"通知郵件發送結果: {(result ? "成功" : "失敗")} ID={notification.Id}, Email={notification.Email_Address}");
                    return result;
                }
                else
                {
                    // 如果沒有注入郵件發送服務，則建立一個臨時的實例
                    ILogger<NotificationEmailSender> tempLogger;
                    if (_logger is ILoggerFactory loggerFactory)
                    {
                        tempLogger = loggerFactory.CreateLogger<NotificationEmailSender>();
                    }
                    else
                    {
                        tempLogger = new LoggerFactory().CreateLogger<NotificationEmailSender>();
                    }
                    var config = _configuration ?? GetConfiguration();

                    var tempSender = new NotificationEmailSender(config, tempLogger);
                    bool result = await tempSender.SendNotificationEmailAsync(notification);
                    LogInfo($"使用臨時郵件發送服務結果: {(result ? "成功" : "失敗")} ID={notification.Id}, Email={notification.Email_Address}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                LogError(ex, $"發送通知郵件時發生錯誤 ID={notification.Id}, Email={notification.Email_Address}");
                return false;
            }
        }

        public Task<bool> IsAvailable()
        {
            return Task.FromResult(true);
        }

        public async Task<List<ContentPublishingSchedule>> GetScheduledTasksAsync(string contentType = null)
        {
            try
            {
                var query = _context.ContentPublishingSchedules.AsNoTracking();

                // 只查詢進行中或已完成的任務
                query = query.Where(s => s.Status == "pending" || s.Status == "executed");

                if (!string.IsNullOrEmpty(contentType))
                {
                    query = query.Where(s => s.ContentType == contentType);
                }

                var tasks = await query
                    .OrderBy(s => s.ScheduledTime)
                    .ToListAsync();

                LogInfo($"查詢排程任務: Type={contentType ?? "all"}, Count={tasks.Count}");

                return tasks;
            }
            catch (Exception ex)
            {
                LogError(ex, $"查詢排程任務失敗 Type={contentType}");
                return new List<ContentPublishingSchedule>();
            }
        }

        public string GetSystemType()
        {
            return "Hangfire Professional System";
        }

        private DbContextOptions<AppDbContext> GetDbContextOptions()
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                var configuration = GetConfiguration();

                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("找不到資料庫連線字串");
                }

                optionsBuilder.UseSqlServer(connectionString);
                return optionsBuilder.Options;
            }
            catch (Exception ex)
            {
                LogError(ex, "取得資料庫連線選項時發生錯誤");
                throw;
            }
        }

        private IConfiguration GetConfiguration()
        {
            return _configuration ?? new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
        }

        [Obsolete("請使用 ExecuteTaskJob 方法")]
        public async Task ExecutePublishJob(int scheduleId, int articleId)
        {
            await ExecuteTaskJob(scheduleId, articleId, "official_post");
        }
    }
}