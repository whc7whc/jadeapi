using Microsoft.EntityFrameworkCore;
using Team.Backend.Models;
using Team.Backend.Models.EfModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Team.Backend.Services
{
    public class BasicScheduleService : IScheduleService
    {
        private readonly AppDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        private static Timer _scheduleTimer;
        private static readonly object _timerLock = new object();

        public BasicScheduleService(AppDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
            StartScheduleProcessor();
        }

        // ✅ 正確的 ScheduleTaskAsync 方法，支援 actionType 參數
        public async Task<ScheduleResult> ScheduleTaskAsync(string contentType, int contentId, DateTime scheduledTime, int userId, string actionType = "publish")
        {
            // 創建排程記錄到資料庫
            try
            {
                var schedule = new ContentPublishingSchedule
                {
                    ContentType = contentType,
                    ContentId = contentId,
                    ActionType = actionType, // ✅ 新增：儲存動作類型
                    ScheduledTime = scheduledTime,
                    Status = "pending",
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                };

                _context.ContentPublishingSchedules.Add(schedule);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ 基礎排程已建立: Type={contentType}, ID={contentId}, Action={actionType}, Schedule={schedule.Id}");

                return ScheduleResult.SuccessResult(schedule.Id.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 建立排程失敗: {ex.Message}");
                return ScheduleResult.ErrorResult(ex.Message);
            }
        }

        public Task<bool> CancelScheduleAsync(int scheduleId)
        {
            try
            {
                // 基礎服務支援簡單的取消功能
                using var context = CreateDbContext();
                var schedule = context.ContentPublishingSchedules.Find(scheduleId);
                if (schedule != null && schedule.Status == "pending")
                {
                    schedule.Status = "cancelled";
                    context.SaveChanges();
                    Console.WriteLine($"✅ 基礎排程已取消: {scheduleId}");
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取消排程失敗: {ex.Message}");
                return Task.FromResult(false);
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
                using var context = CreateDbContext();
                var query = context.ContentPublishingSchedules.AsNoTracking();

                if (!string.IsNullOrEmpty(contentType))
                {
                    query = query.Where(s => s.ContentType == contentType);
                }

                return await query
                    .OrderBy(s => s.ScheduledTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取得排程列表失敗: {ex.Message}");
                return new List<ContentPublishingSchedule>();
            }
        }

        public string GetSystemType()
        {
            return "Basic System";
        }

        // 創建新的 DbContext 實例（用於靜態方法和獨立操作）
        private AppDbContext CreateDbContext()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }

        // 啟動定時處理器
        private static void StartScheduleProcessor()
        {
            lock (_timerLock)
            {
                if (_scheduleTimer == null)
                {
                    _scheduleTimer = new Timer(ProcessScheduledTasks, null,
                        TimeSpan.Zero, TimeSpan.FromMinutes(1));
                }
            }
        }

        private static async void ProcessScheduledTasks(object state)
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .Build();

                var connectionString = configuration.GetConnectionString("DefaultConnection");

                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseSqlServer(connectionString);

                using (var context = new AppDbContext(optionsBuilder.Options))
                {
                    // 修正：直接處理排程，不需要創建 Service 實例
                    await ProcessPendingSchedulesAsync(context);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"處理排程任務錯誤: {ex.Message}");
            }
        }

        // 將處理邏輯改為靜態方法
        private static async Task ProcessPendingSchedulesAsync(AppDbContext context)
        {
            var now = DateTime.Now;
            var pendingSchedules = await context.ContentPublishingSchedules
                .Where(s => s.Status == "pending" && s.ScheduledTime <= now)
                .Take(10)
                .ToListAsync();

            foreach (var schedule in pendingSchedules)
            {
                await ExecuteScheduleAsync(context, schedule);
            }
        }

        // 將執行邏輯改為靜態方法
        private static async Task ExecuteScheduleAsync(AppDbContext context, ContentPublishingSchedule schedule)
        {
            using (var transaction = context.Database.BeginTransaction())
            {
                try
                {
                    // ✅ 根據內容類型和動作類型執行不同的邏輯
                    if (schedule.ContentType == "official_post")
                    {
                        await ExecuteArticlePublish(context, schedule);
                    }
                    else if (schedule.ContentType == "notification")
                    {
                        await ExecuteNotificationSend(context, schedule);
                    }
                    // ✅ 優惠券排程處理
                    else if (schedule.ContentType == "coupon")
                    {
                        await ExecuteCouponAction(context, schedule);
                    }

                    // 更新排程狀態
                    schedule.Status = "executed";
                    schedule.ExecutedAt = DateTime.Now;

                    await context.SaveChangesAsync();
                    transaction.Commit();

                    Console.WriteLine($"✅ 排程執行成功: {schedule.ContentType} ID {schedule.ContentId}, Action: {schedule.ActionType}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    schedule.Status = "failed";
                    schedule.ErrorMessage = ex.Message;
                    await context.SaveChangesAsync();

                    Console.WriteLine($"❌ 排程執行失敗: {ex.Message}");
                }
            }
        }

        // ✅ 處理優惠券動作
        private static async Task ExecuteCouponAction(AppDbContext context, ContentPublishingSchedule schedule)
        {
            var coupon = await context.Coupons.FindAsync(schedule.ContentId);
            if (coupon != null)
            {
                // ✅ 修改：ActionType 現在表示會員等級ID或特殊值
                switch (schedule.ActionType)
                {
                    case "all":
                        // 發送給全部會員
                        await DispatchCouponToMemberLevel(context, coupon, null, "全部");
                        break;

                    default:
                        // 嘗試解析為會員等級ID
                        if (int.TryParse(schedule.ActionType, out int levelId))
                        {
                            var level = await context.MembershipLevels.FindAsync(levelId);
                            if (level != null)
                            {
                                await DispatchCouponToMemberLevel(context, coupon, levelId, level.LevelName);
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ 找不到會員等級 ID: {levelId}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ 無效的會員等級設定: {schedule.ActionType}");
                        }
                        break;
                }

                coupon.UpdatedAt = DateTime.Now;
            }
        }

        // ✅ 新增：發送優惠券給指定會員等級的方法
        private static async Task DispatchCouponToMemberLevel(AppDbContext context, Coupon coupon, int? targetLevelId, string levelName)
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
                        Console.WriteLine($"❌ 發送優惠券給會員 {member.Id} 失敗: {ex.Message}");
                        errorCount++;
                    }
                }

                await context.SaveChangesAsync();

                Console.WriteLine($"🎫 優惠券「{coupon.Title}」發送完成");
                Console.WriteLine($"   目標等級：{levelName}");
                Console.WriteLine($"   成功：{successCount} 張");
                Console.WriteLine($"   跳過：{skippedCount} 張");
                Console.WriteLine($"   錯誤：{errorCount} 張");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 批量發送優惠券失敗: {ex.Message}");
                throw;
            }
        }

        // ✅ 新增：生成驗證碼的方法
        private static string GenerateVerificationCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // ✅ 處理文章發布 - 保持原有邏輯不變
        private static async Task ExecuteArticlePublish(AppDbContext context, ContentPublishingSchedule schedule)
        {
            var article = await context.OfficialPosts.FindAsync(schedule.ContentId);
            if (article != null)
            {
                // 文章發布只使用 "publish" 動作類型，或者預設行為
                article.Status = "published";
                article.PublishedAt = DateTime.Now;
                Console.WriteLine($"📄 文章發布: {article.Title}");
            }
        }

        // ✅ 處理通知發送
        private static async Task ExecuteNotificationSend(AppDbContext context, ContentPublishingSchedule schedule)
        {
            // 未來實作通知發送邏輯
            Console.WriteLine($"📧 通知發送: ID {schedule.ContentId}");

            // 範例：更新通知狀態
            // var notification = await context.Notifications.FindAsync(schedule.ContentId);
            // if (notification != null)
            // {
            //     notification.Status = "sent";
            //     notification.SentAt = DateTime.Now;
            // }
        }

        // 實例方法版本（供外部調用）
        private async Task ProcessPendingSchedulesAsync()
        {
            await ProcessPendingSchedulesAsync(_context);
        }

        private async Task ExecuteScheduleAsync(ContentPublishingSchedule schedule)
        {
            await ExecuteScheduleAsync(_context, schedule);
        }
    }
}