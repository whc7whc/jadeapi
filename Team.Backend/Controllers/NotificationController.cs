// NotificationController.cs - 通知管理API控制器

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using Team.Backend.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mail;
using System.Text;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Font = iTextSharp.text.Font;
using System.Data;
using Microsoft.Extensions.Logging;
using Team.Backend.Services; // 確保排程服務介面可用

namespace Team.Backend.Controllers
{
	[Route("[controller]")]
	public class NotificationController : BaseController
    {
		private readonly AppDbContext _context;
		private readonly ILogger<NotificationController> _logger;
		private readonly IMemoryCache _memoryCache;
		private readonly INotificationEmailSender _emailSender;
		private readonly IScheduleService _scheduleService; // 新增

		public NotificationController(
            AppDbContext context, 
            ILogger<NotificationController> logger, 
            IMemoryCache memoryCache,
            INotificationEmailSender emailSender,
            IScheduleService scheduleService) // 添加 scheduleService 參數
            : base(context, logger)
		{
			_context = context;
			_logger = logger;
			_memoryCache = memoryCache;
			_emailSender = emailSender;
			_scheduleService = scheduleService; // 賦值
		}

		// 主頁面
		[HttpGet("MainNotification")]
		public IActionResult MainNotification()
		{
			var viewModel = new NotificationManagementViewModel
			{
				Notifications = new List<Notification>(),
				CurrentPage = 1,
				ItemsPerPage = 10,
				TotalCount = 0,
				TotalPages = 0,
				Categories = new List<string> { "order", "payment", "account", "security", "promotion", "system", "test" },
				EmailStatuses = new List<string> { "immediate", "scheduled", "draft" },
				Channels = new List<string> { "email", "sms", "push", "internal" },
				FilterCount = 0,
				StatisticsByCategory = new Dictionary<string, int>(),
				TodayCount = 0,
				IsLoading = false
			};

			return View(viewModel);
		}

		// API: 獲取最新通知 (for layout notification dropdown) - 篩選推播通知
		[HttpGet("GetLatestNotifications")]
		public async Task<IActionResult> GetLatestNotifications(int limit = 8)
		{
			try
			{
				_logger.LogInformation("開始獲取最新推播通知，限制數量：{Limit}", limit);

				// 檢查資料庫連接
				var connectionTest = await _context.Database.CanConnectAsync();
				if (!connectionTest)
				{
					_logger.LogError("無法連接到資料庫");
					return Json(new
					{
						success = false,
						message = "資料庫連接失敗",
						data = new
						{
							HasNotifications = false,
							Notifications = new List<object>(),
							UnreadCount = "0",
							TotalCount = 0
						}
					});
				}

				// 獲取最新的推播通知 (channel = "push")
				var notifications = await _context.Notifications
					.AsNoTracking()
					.Where(n => !n.Is_Deleted && 
							   n.Email_Status == "sent" && 
							   n.Channel == "push")  // 只篩選推播通知
					.OrderByDescending(n => n.Sent_At)
					.Take(limit)
					.ToListAsync();

				_logger.LogInformation("獲取到 {Count} 筆推播通知", notifications.Count);

				// 轉換為前端所需的格式
				var notificationItems = notifications.Select(n => new
				{
					Id = n.Id,
					Message = n.Message ?? "系統推播通知",
					CategoryLabel = GetCategoryLabel(n.Category),
					CategoryIcon = GetCategoryIcon(n.Category),
					CategoryColor = GetCategoryColor(n.Category),
					FormattedSentAt = n.Sent_At.ToString("MM/dd HH:mm"),
					RelativeTime = GetRelativeTime(n.Sent_At),
					SentAt = n.Sent_At,
					Channel = n.Channel,
					EmailAddress = n.Email_Address
				}).ToList();

				// 計算未讀數量（所有已發送的推播通知）
				var unreadCount = await _context.Notifications
					.CountAsync(n => !n.Is_Deleted && 
								   n.Email_Status == "sent" && 
								   n.Channel == "push");  // 只計算推播通知，移除時間限制

				var response = new
				{
					success = true,
					message = "獲取推播通知成功",
					data = new
					{
						HasNotifications = notificationItems.Any(),
						Notifications = notificationItems,
						UnreadCount = unreadCount > 99 ? "99+" : unreadCount.ToString(),
						TotalCount = notifications.Count,
						FilterType = "push" // 標示這是推播通知
					}
				};

				_logger.LogInformation("成功返回推播通知，共 {Count} 筆，未讀 {UnreadCount} 筆", 
					notificationItems.Count, unreadCount);

				return Json(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "獲取推播通知失敗");
				return Json(new
				{
					success = false,
					message = $"獲取推播通知失敗：{ex.Message}",
					data = new
					{
						HasNotifications = false,
						Notifications = new List<object>(),
						UnreadCount = "0",
						TotalCount = 0
					}
				});
			}
		}

		// API: 獲取通知列表
		[HttpGet("GetNotifications")]
		public async Task<IActionResult> GetNotifications([FromQuery] NotificationQueryDto query)
		{
			try
			{
				_logger.LogInformation("開始獲取通知列表，參數：{@Query}", query);

				// 檢查資料庫連接
				var connectionTest = await _context.Database.CanConnectAsync();
				if (!connectionTest)
				{
					_logger.LogError("無法連接到資料庫");
					return Json(ApiResponseDto<object>.ErrorResult("資料庫連接失敗"));
				}

				// 檢查是否有資料，如果沒有資料則自動創建測試資料
				var hasData = await _context.Notifications.AnyAsync(n => !n.Is_Deleted);
				if (!hasData)
				{
					_logger.LogWarning("資料庫中沒有通知資料，將創建測試資料");
					await CreateInitialTestData();
				}

				// 使用實際的資料表欄位名稱進行查詢
				var queryable = _context.Notifications
					.AsNoTracking()
					.Where(n => !n.Is_Deleted);

				_logger.LogInformation("基礎查詢完成，開始應用篩選條件");

				// 搜尋條件 - 使用實際欄位名稱
				if (!string.IsNullOrEmpty(query.Search))
				{
					queryable = queryable.Where(n =>
						EF.Functions.Like(n.Message, $"%{query.Search}%") ||
						EF.Functions.Like(n.Email_Address, $"%{query.Search}%") ||
						EF.Functions.Like(n.Category, $"%{query.Search}%"));
				}

				// 篩選條件 - 使用實際欄位名稱
				if (!string.IsNullOrEmpty(query.Category))
					queryable = queryable.Where(n => n.Category == query.Category);

				if (!string.IsNullOrEmpty(query.EmailStatus))
					queryable = queryable.Where(n => n.Email_Status == query.EmailStatus);

				if (!string.IsNullOrEmpty(query.Channel))
					queryable = queryable.Where(n => n.Channel == query.Channel);

				if (query.StartDate.HasValue)
					queryable = queryable.Where(n => n.Sent_At >= query.StartDate);

				if (query.EndDate.HasValue)
					queryable = queryable.Where(n => n.Sent_At <= query.EndDate.Value.AddDays(1));

				// 排序 - 使用實際欄位名稱
				var isDesc = query.SortDirection.ToLower() == "desc";
				queryable = query.SortBy.ToLower() switch
				{
					"emailaddress" => isDesc ? queryable.OrderByDescending(n => n.Email_Address) : queryable.OrderBy(n => n.Email_Address),
					"category" => isDesc ? queryable.OrderByDescending(n => n.Category) : queryable.OrderBy(n => n.Category),
					"emailstatus" => isDesc ? queryable.OrderByDescending(n => n.Email_Status) : queryable.OrderBy(n => n.Email_Status),
					"channel" => isDesc ? queryable.OrderByDescending(n => n.Channel) : queryable.OrderBy(n => n.Channel),
					"message" => isDesc ? queryable.OrderByDescending(n => n.Message) : queryable.OrderBy(n => n.Message),
					"createdat" => isDesc ? queryable.OrderByDescending(n => n.Created_At) : queryable.OrderBy(n => n.Created_At),
					_ => isDesc ? queryable.OrderByDescending(n => n.Sent_At) : queryable.OrderBy(n => n.Sent_At)
				};

				// 計算總數
				var totalCount = await queryable.CountAsync();
				_logger.LogInformation("查詢到 {TotalCount} 筆記錄", totalCount);

				// 分頁查詢
				var notifications = await queryable
					.Skip((query.Page - 1) * query.ItemsPerPage)
					.Take(query.ItemsPerPage)
					.ToListAsync();

				_logger.LogInformation("分頁後取得 {Count} 筆記錄", notifications.Count);

				// 轉換為 DTO
				var notificationDtos = notifications.Select(n => new NotificationResponseDto
				{
					Id = n.Id,
					MemberId = n.Member_Id,
					SellerId = n.Seller_Id,
					EmailAddress = n.Email_Address ?? "",
					Category = n.Category ?? "",
					CategoryLabel = GetCategoryLabel(n.Category),
					EmailStatus = n.Email_Status ?? "",
					EmailStatusLabel = GetEmailStatusLabel(n.Email_Status),
					Channel = n.Channel ?? "",
					ChannelLabel = GetChannelLabel(n.Channel),
					Message = n.Message ?? "",
					SentAt = n.Sent_At,
					FormattedSentAt = n.Sent_At.ToString("yyyy/MM/dd HH:mm"),
					EmailSentAt = n.Email_Sent_At,
					EmailRetry = n.Email_Retry,
					CreatedAt = n.Created_At,
					FormattedCreatedAt = n.Created_At.ToString("yyyy/MM/dd HH:mm"),
					UpdatedAt = n.Updated_At,
					FormattedUpdatedAt = n.Updated_At.ToString("yyyy/MM/dd HH:mm"),
					IsDeleted = n.Is_Deleted
				}).ToList();

				var response = new PagedResponseDto<NotificationResponseDto>
				{
					Success = true,
					Message = "獲取通知列表成功",
					Data = notificationDtos,
					TotalCount = totalCount,
					CurrentPage = query.Page,
					TotalPages = (int)Math.Ceiling((double)totalCount / query.ItemsPerPage),
					ItemsPerPage = query.ItemsPerPage
				};

				_logger.LogInformation("成功返回通知列表，總計 {Count} 筆", response.Data.Count());
				return Json(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "獲取通知列表失敗，查詢參數：{@Query}", query);
				return Json(ApiResponseDto<object>.ErrorResult($"獲取通知列表失敗：{ex.Message}"));
			}
		}

		// 創建初始測試資料
		private async Task CreateInitialTestData()
		{
			try
			{
				var now = DateTime.Now;
				var testNotifications = new List<Notification>
				{
					// 推播通知 - 這些會在Layout中顯示
					new Notification
					{
						Member_Id = 1,
						Email_Address = "user1@example.com",
						Category = "promotion",
						Email_Status = "sent",
						Channel = "push", // 推播通知
						Message = "🎉 新年特惠活動開跑！全館商品8折優惠，限時3天！",
						Sent_At = now.AddMinutes(-10),
						Created_At = now.AddMinutes(-10),
						Updated_At = now.AddMinutes(-10),
						Email_Sent_At = now.AddMinutes(-10),
						Email_Retry = 0,
						Is_Deleted = false
					},
					new Notification
					{
						Member_Id = 2,
						Email_Address = "user2@example.com",
						Category = "system",
						Email_Status = "sent",
						Channel = "push", // 推播通知
						Message = "⚠️ 系統維護通知：今晚23:00-01:00進行系統升級，期間服務可能中斷",
						Sent_At = now.AddHours(-1),
						Created_At = now.AddHours(-1),
						Updated_At = now.AddHours(-1),
						Email_Sent_At = now.AddHours(-1),
						Email_Retry = 0,
						Is_Deleted = false
					},
					new Notification
					{
						Member_Id = 3,
						Email_Address = "user3@example.com",
						Category = "security",
						Email_Status = "sent",
						Channel = "push", // 推播通知
						Message = "🔒 安全提醒：檢測到異常登入行為，請確認是否為本人操作",
						Sent_At = now.AddHours(-3),
						Created_At = now.AddHours(-3),
						Updated_At = now.AddHours(-3),
						Email_Sent_At = now.AddHours(-3),
						Email_Retry = 0,
						Is_Deleted = false
					},
					// 郵件通知 - 這些不會在Layout中顯示，只在通知管理頁面中顯示
					new Notification
					{
						Member_Id = 4,
						Email_Address = "test@example.com",
						Category = "order",
						Email_Status = "sent",
						Channel = "email", // 郵件通知
						Message = "您的訂單已成功提交，訂單編號：#12345",
						Sent_At = now.AddMinutes(-30),
						Created_At = now.AddMinutes(-30),
						Updated_At = now.AddMinutes(-30),
						Email_Sent_At = now.AddMinutes(-30),
						Email_Retry = 0,
						Is_Deleted = false
					},
					new Notification
					{
						Member_Id = 5,
						Email_Address = "user4@example.com",
						Category = "payment",
						Email_Status = "sent",
						Channel = "email", // 郵件通知
						Message = "付款成功！感謝您的購買，我們將盡快為您出貨",
						Sent_At = now.AddHours(-2),
						Created_At = now.AddHours(-2),
						Updated_At = now.AddHours(-2),
						Email_Sent_At = now.AddHours(-2),
						Email_Retry = 0,
						Is_Deleted = false
					}
				};

				_context.Notifications.AddRange(testNotifications);
				await _context.SaveChangesAsync();
				_logger.LogInformation("成功創建 {Count} 筆測試通知資料（包含 {PushCount} 筆推播通知）", 
					testNotifications.Count, 
					testNotifications.Count(n => n.Channel == "push"));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "創建測試資料失敗");
			}
		}

        // API: 新增通知
        [HttpPost("CreateNotification")]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto dto)
        {
            try
            {
                _logger.LogInformation("開始創建通知，資料: {@Dto}", dto);

                // 詳細的模型驗證
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.First().ErrorMessage
                        );

                    _logger.LogWarning("創建通知輸入驗證失敗: {@Errors}", errors);
                    return Json(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                // 額外的業務邏輯驗證
                var businessValidationErrors = ValidateNotificationBusiness(dto);
                if (businessValidationErrors.Any())
                {
                    return Json(ApiResponseDto<object>.ErrorResult("業務驗證失敗", businessValidationErrors));
                }

                // 手動映射到實際的資料表欄位
                var notification = new Notification
                {
                    Member_Id = dto.MemberId,
                    Seller_Id = dto.SellerId,
                    Email_Address = dto.EmailAddress, // 這裡直接使用 EmailAddress
                    Category = dto.Category ?? "",
                    Email_Status = dto.EmailStatus ?? "draft",
                    Channel = dto.Channel ?? "email",
                    Message = dto.Message ?? "",
                    Sent_At = (dto.SentAt is DateTime d1 ? d1 : DateTime.Now),
                    Created_At = DateTime.Now,
                    Updated_At = DateTime.Now,
                    Email_Sent_At = null,
                    Email_Retry = 0,
                    Is_Deleted = false
                };

                // 處理立即發送
                if (dto.EmailStatus == "immediate" && dto.Channel?.ToLower() == "email")
                {
                    notification.Email_Sent_At = DateTime.Now;
                    notification.Sent_At = DateTime.Now;

                    // 先保存到資料庫
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("通知已保存到資料庫，ID: {Id}，準備發送郵件", notification.Id);

                    // 實際發送郵件
                    try
                    {
                        bool emailSent = await _emailSender.SendNotificationEmailAsync(notification);

                        // 更新發送狀態
                        notification.Email_Status = emailSent ? "sent" : "failed";
                        if (!emailSent)
                        {
                            notification.Email_Retry += 1;
                        }
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("郵件發送結果: {Result}，通知ID: {Id}", 
                            emailSent ? "成功" : "失敗", notification.Id);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "發送郵件時發生錯誤，通知ID: {Id}", notification.Id);
                        notification.Email_Status = "failed";
                        notification.Email_Retry += 1;
                        await _context.SaveChangesAsync();
                    }
                }
                else if (dto.EmailStatus == "scheduled")
                {
                    // 排程：將通知儲存為 scheduled，並建立 Hangfire 排程
                    notification.Email_Status = "scheduled";
                    notification.Sent_At = (dto.SentAt is DateTime d2 ? d2 : DateTime.Now);

                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();

                    try
                    {
                        var scheduleResult = await _scheduleService.ScheduleTaskAsync(
                            "notification",
                            notification.Id,
                            notification.Sent_At,
                            notification.Member_Id ?? 0
                        );

                        if (!scheduleResult.Success)
                        {
                            _logger.LogWarning("建立通知排程失敗: {ErrorMessage}", scheduleResult.ErrorMessage);
                            // 若排程建立失敗，可選擇回滾或回報給前端
                        }
                        else
                        {
                            _logger.LogInformation("建立通知排程成功，ScheduleId={ScheduleId}", scheduleResult.ScheduleId ?? "N/A");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "建立 Hangfire 排程失敗，通知ID: {Id}", notification.Id);
                    }
                }
                else
                {
                    // 非立即發送或非排程，只保存到資料庫
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("成功創建通知，ID: {Id}", notification.Id);

                var responseDto = new NotificationResponseDto
                {
                    Id = notification.Id,
                    MemberId = notification.Member_Id,
                    SellerId = notification.Seller_Id,
                    EmailAddress = notification.Email_Address,
                    Category = notification.Category,
                    CategoryLabel = GetCategoryLabel(notification.Category),
                    EmailStatus = notification.Email_Status,
                    EmailStatusLabel = GetEmailStatusLabel(notification.Email_Status),
                    Channel = notification.Channel,
                    ChannelLabel = GetChannelLabel(notification.Channel),
                    Message = notification.Message,
                    SentAt = notification.Sent_At,
                    FormattedSentAt = notification.Sent_At.ToString("yyyy/MM/dd HH:mm"),
                    EmailSentAt = notification.Email_Sent_At,
                    EmailRetry = notification.Email_Retry,
                    CreatedAt = notification.Created_At,
                    FormattedCreatedAt = notification.Created_At.ToString("yyyy/MM/dd HH:mm"),
                    UpdatedAt = notification.Updated_At,
                    FormattedUpdatedAt = notification.Updated_At.ToString("yyyy/MM/dd HH:mm"),
                    IsDeleted = notification.Is_Deleted
                };

                return Json(ApiResponseDto<NotificationResponseDto>.SuccessResult(
                    responseDto,
                    "通知創建成功"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "創建通知失敗，資料: {@Dto}", dto);
                return Json(ApiResponseDto<object>.ErrorResult($"創建通知失敗：{ex.Message}"));
            }
        }

        // API: 批量新增通知
        [HttpPost("CreateBulkNotification")]
        public async Task<IActionResult> CreateBulkNotification([FromBody] CreateBulkNotificationDto dto)
        {
            try
            {
                // 記錄接收到的完整資料
                _logger.LogInformation("===== CreateBulkNotification 開始 =====");
                _logger.LogInformation("接收到的 DTO: {@Dto}", dto);
                _logger.LogInformation("TargetType: {TargetType}", dto.TargetType);
                _logger.LogInformation("SpecificAccount: '{SpecificAccount}'", dto.SpecificAccount ?? "null");

                // 檢查 ModelState
                _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
                if (!ModelState.IsValid)
                {
                    _logger.LogInformation("ModelState 錯誤詳情:");
                    foreach (var modelState in ModelState)
                    {
                        var key = modelState.Key;
                        var errors = modelState.Value.Errors.Select(e => e.ErrorMessage).ToList();
                        _logger.LogInformation("欄位 '{Key}': 錯誤 = [{Errors}]", key, string.Join(", ", errors));
                    }
                }

                // 🔧 臨時修正：手動移除 SpecificAccount 的 ModelState 錯誤
                if (dto.TargetType != 3)
                {
                    // 如果不是指定帳號模式，移除 SpecificAccount 的驗證錯誤
                    if (ModelState.ContainsKey("SpecificAccount"))
                    {
                        ModelState.Remove("SpecificAccount");
                        _logger.LogInformation("已移除 SpecificAccount 的 ModelState 驗證錯誤");
                    }
                }

                // 重新檢查 ModelState
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.First().ErrorMessage
                        );

                    _logger.LogWarning("批量創建通知輸入驗證失敗: {@Errors}", errors);
                    return Json(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                // 手動驗證：只有當 TargetType = 3 時才檢查 SpecificAccount
                if (dto.TargetType == 3 && string.IsNullOrWhiteSpace(dto.SpecificAccount))
                {
                    _logger.LogWarning("指定帳號模式但未提供 SpecificAccount");
                    return Json(ApiResponseDto<object>.ErrorResult(
                        "選擇指定帳號時，必須提供帳號資訊",
                        new Dictionary<string, string> { { "SpecificAccount", "指定帳號為必填欄位" } }
                    ));
                }

                // 額外的業務邏輯驗證
                var businessValidationErrors = ValidateBulkNotificationBusiness(dto);
                if (businessValidationErrors.Any())
                {
                    _logger.LogWarning("業務驗證失敗: {@Errors}", businessValidationErrors);
                    return Json(ApiResponseDto<object>.ErrorResult("業務驗證失敗", businessValidationErrors));
                }

                var notifications = new List<Notification>();
                var emailAddresses = new List<string>();

                // 根據目標類型獲取收件人列表
                _logger.LogInformation("開始根據目標類型 {TargetType} 獲取收件人", dto.TargetType);

                switch (dto.TargetType)
                {
                    case 1: // 所有使用者
                        _logger.LogInformation("獲取所有使用者郵件地址");
                        emailAddresses = await GetAllUserEmails();
                        break;
                    case 2: // 所有賣家
                        _logger.LogInformation("獲取所有賣家郵件地址");
                        emailAddresses = await GetAllSellerEmails();
                        break;
                    case 3: // 指定帳號
                        _logger.LogInformation("回答指定帳號: {Account}", dto.SpecificAccount);
                        if (!string.IsNullOrEmpty(dto.SpecificAccount))
                        {
                            emailAddresses.Add(dto.SpecificAccount);
                        }
                        break;
                    default:
                        _logger.LogError("不支援的目標類型: {TargetType}", dto.TargetType);
                        return Json(ApiResponseDto<object>.ErrorResult("不支援的目標類型"));
                }

                if (!emailAddresses.Any())
                {
                    _logger.LogWarning("找不到符合條件的收件人，目標類型: {TargetType}", dto.TargetType);
                    return Json(ApiResponseDto<object>.ErrorResult("找不到符合條件的收件人"));
                }

                _logger.LogInformation("找到 {Count} 個收件人: {@Emails}", emailAddresses.Count, emailAddresses);

                // 為每個收件人建立通知
                var currentTime = DateTime.Now;
                foreach (var email in emailAddresses)
                {
                    var notification = new Notification
                    {
                        Member_Id = dto.MemberId,
                        Seller_Id = dto.SellerId,
                        Email_Address = email,
                        Category = dto.Category ?? "",
                        Email_Status = dto.EmailStatus ?? "draft",
                        Channel = dto.Channel ?? "email",
                        Message = dto.Message ?? "",
                        Sent_At = (dto.SentAt is DateTime d3 ? d3 : currentTime),
                        Created_At = currentTime,
                        Updated_At = currentTime,
                        Email_Sent_At = dto.EmailStatus == "immediate" ? currentTime : null,
                        Email_Retry = 0,
                        Is_Deleted = false
                    };

                    notifications.Add(notification);
                }

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation("成功批量創建 {Count} 筆通知", notifications.Count);

                // 如果是排程，為每筆建立 Hangfire 排程
                if (dto.EmailStatus == "scheduled")
                {
                    foreach (var notification in notifications)
                    {
                        try
                        {
                            var scheduleResult = await _scheduleService.ScheduleTaskAsync(
                                "notification",
                                notification.Id,
                                notification.Sent_At,
                                notification.Member_Id ?? 0
                            );

                            if (!scheduleResult.Success)
                            {
                                _logger.LogWarning("批量建立排程失敗，通知ID={Id}, ErrorMessage={Msg}", notification.Id, scheduleResult.ErrorMessage);
                            }
                            else
                            {
                                _logger.LogInformation("批量建立排程成功，通知ID={Id}", notification.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "為通知建立排程時發生錯誤，通知ID: {Id}", notification.Id);
                        }
                    }
                }

                // 🚀 如果是立即發送且為電子郵件，實際發送郵件
                if (dto.EmailStatus == "immediate" && dto.Channel?.ToLower() == "email")
                {
                    _logger.LogInformation("開始批量發送 {Count} 封郵件", notifications.Count);
                    
                    int successCount = 0;
                    int failCount = 0;

                    foreach (var notification in notifications)
                    {
                        try
                        {
                            bool emailSent = await _emailSender.SendNotificationEmailAsync(notification);
                            
                            // 更新發送狀態
                            notification.Email_Status = emailSent ? "sent" : "failed";
                            if (!emailSent)
                            {
                                notification.Email_Retry += 1;
                                failCount++;
                            }
                            else
                            {
                                successCount++;
                            }
                            
                            _logger.LogInformation("郵件發送結果: {Result}，收件人: {Email}，通知ID: {Id}", 
                                emailSent ? "成功" : "失敗", notification.Email_Address, notification.Id);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "發送郵件時發生錯誤，收件人: {Email}，通知ID: {Id}", 
                                notification.Email_Address, notification.Id);
                            notification.Email_Status = "failed";
                            notification.Email_Retry += 1;
                            failCount++;
                        }
                    }

                    // 批量更新狀態
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("批量郵件發送完成，成功: {Success} 筆，失敗: {Fail} 筆", 
                        successCount, failCount);
                }

                return Json(ApiResponseDto<object>.SuccessResult(
                    new
                    {
                        CreatedCount = notifications.Count,
                        EmailAddresses = emailAddresses
                    },
                    $"批量通知創建成功，共 {notifications.Count} 筆"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量創建通知失敗，資料: {@Dto}", dto);
                return Json(ApiResponseDto<object>.ErrorResult($"批量創建通知失敗：{ex.Message}"));
            }
        }

		// API: 更新通知
		[HttpPut("UpdateNotification/{id}")]
		public async Task<IActionResult> UpdateNotification(int id, [FromBody] UpdateNotificationDto dto)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = ModelState
						.Where(x => x.Value.Errors.Count > 0)
						.ToDictionary(
							kvp => kvp.Key,
							kvp => kvp.Value.Errors.First().ErrorMessage
						);

					return Json(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
				}

				var notification = await _context.Notifications.FindAsync(id);
				if (notification == null)
				{
					return Json(ApiResponseDto<object>.ErrorResult("找不到指定的通知"));
				}

				// 手動更新實際的資料表欄位
				if (!string.IsNullOrEmpty(dto.EmailAddress))
					notification.Email_Address = dto.EmailAddress;
				if (!string.IsNullOrEmpty(dto.Category))
					notification.Category = dto.Category;
				if (!string.IsNullOrEmpty(dto.EmailStatus))
					notification.Email_Status = dto.EmailStatus;
				if (!string.IsNullOrEmpty(dto.Channel))
					notification.Channel = dto.Channel;
				if (!string.IsNullOrEmpty(dto.Message))
					notification.Message = dto.Message;
				if (dto.SentAt.HasValue)
					notification.Sent_At = dto.SentAt.Value;
				notification.Updated_At = DateTime.Now;

				await _context.SaveChangesAsync();

				var responseDto = new NotificationResponseDto
				{
					Id = notification.Id,
					MemberId = notification.Member_Id,
					SellerId = notification.Seller_Id,
					EmailAddress = notification.Email_Address,
					Category = notification.Category,
					CategoryLabel = GetCategoryLabel(notification.Category),
					EmailStatus = notification.Email_Status,
					EmailStatusLabel = GetEmailStatusLabel(notification.Email_Status),
					Channel = notification.Channel,
					ChannelLabel = GetChannelLabel(notification.Channel),
					Message = notification.Message,
					SentAt = notification.Sent_At,
					FormattedSentAt = notification.Sent_At.ToString("yyyy/MM/dd HH:mm"),
					EmailSentAt = notification.Email_Sent_At,
					EmailRetry = notification.Email_Retry,
					IsDeleted = notification.Is_Deleted
				};

				return Json(ApiResponseDto<NotificationResponseDto>.SuccessResult(
					responseDto,
					"通知更新成功"
				));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "更新通知失敗");
				return Json(ApiResponseDto<object>.ErrorResult($"更新通知失敗：{ex.Message}"));
			}
		}

		// API: 發布通知（將草稿變為已發送）
		[HttpPost("PublishNotification/{id}")]
		public async Task<IActionResult> PublishNotification(int id)
		{
			try
			{
				var notification = await _context.Notifications.FindAsync(id);
				if (notification == null)
				{
					return Json(ApiResponseDto<object>.ErrorResult("找不到指定的通知"));
				}

				if (notification.Email_Status != "draft")
				{
					return Json(ApiResponseDto<object>.ErrorResult("只能發布草稿狀態的通知"));
				}

				notification.Email_Status = "sent";
				notification.Sent_At = DateTime.Now;
				notification.Email_Sent_At = DateTime.Now;
				notification.Updated_At = DateTime.Now;

				// 🚀 實際發送郵件
				if (notification.Channel?.ToLower() == "email")
				{
					try
					{
						bool emailSent = await _emailSender.SendNotificationEmailAsync(notification);
						
						// 更新發送狀態
						notification.Email_Status = emailSent ? "sent" : "failed";
                        if (!emailSent)
                        {
                            notification.Email_Retry += 1;
                        }
						
						_logger.LogInformation("發布通知郵件發送結果: {Result}，通知ID: {Id}", 
							emailSent ? "成功" : "失敗", notification.Id);
					}
					catch (Exception emailEx)
					{
						_logger.LogError(emailEx, "發布通知時發送郵件發生錯誤，通知ID: {Id}", notification.Id);
						notification.Email_Status = "failed";
						notification.Email_Retry += 1;
					}
				}

				await _context.SaveChangesAsync();

				return Json(ApiResponseDto<object>.SuccessResult(null, "通知發布成功"));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "發布通知失敗，ID: {Id}", id);
				return Json(ApiResponseDto<object>.ErrorResult($"發布通知失敗：{ex.Message}"));
			}
		}

		// API: 重新發送通知
		[HttpPost("ResendNotification/{id}")]
		public async Task<IActionResult> ResendNotification(int id)
		{
			try
			{
				var notification = await _context.Notifications.FindAsync(id);
				if (notification == null)
				{
					return Json(ApiResponseDto<object>.ErrorResult("找不到指定的通知"));
				}

				// 重置發送狀態
				notification.Email_Status = "sent";
				notification.Sent_At = DateTime.Now;
				notification.Email_Sent_At = DateTime.Now;
				notification.Email_Retry = notification.Email_Retry + 1;
				notification.Updated_At = DateTime.Now;

				// 🚀 實際重新發送郵件
				if (notification.Channel?.ToLower() == "email")
				{
					try
					{
						bool emailSent = await _emailSender.SendNotificationEmailAsync(notification);
						
						// 更新發送狀態
						notification.Email_Status = emailSent ? "sent" : "failed";
                        if (!emailSent)
                        {
                            notification.Email_Retry += 1;
                        }
						
						_logger.LogInformation("重發通知郵件發送結果: {Result}，通知ID: {Id}", 
							emailSent ? "成功" : "失敗", notification.Id);
					}
					catch (Exception emailEx)
					{
						_logger.LogError(emailEx, "重發通知時發送郵件發生錯誤，通知ID: {Id}", notification.Id);
						notification.Email_Status = "failed";
						notification.Email_Retry += 1;
					}
				}

				await _context.SaveChangesAsync();

				return Json(ApiResponseDto<object>.SuccessResult(null, "通知重發成功"));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "重發通知失敗，ID: {Id}", id);
				return Json(ApiResponseDto<object>.ErrorResult($"重發通知失敗：{ex.Message}"));
			}
		}

		// API: 取消排程通知
		[HttpPost("CancelSchedule/{id}")]
		public async Task<IActionResult> CancelSchedule(int id)
		{
			try
			{
				var notification = await _context.Notifications.FindAsync(id);
				if (notification == null)
				{
					return Json(ApiResponseDto<object>.ErrorResult("找不到指定的通知"));
				}

				if (notification.Email_Status != "scheduled")
				{
					return Json(ApiResponseDto<object>.ErrorResult("只能取消排程狀態的通知"));
				}

				// 取消排程，變更為草稿狀態
				notification.Email_Status = "draft";
				notification.Updated_At = DateTime.Now;

				await _context.SaveChangesAsync();

				return Json(ApiResponseDto<object>.SuccessResult(null, "排程取消成功"));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "取消排程失敗，ID: {Id}", id);
				return Json(ApiResponseDto<object>.ErrorResult($"取消排程失敗：{ex.Message}"));
			}
		}

		// API: 批量發布通知
		[HttpPost("PublishNotifications")]
		public async Task<IActionResult> PublishNotifications([FromBody] BatchOperationDto dto)
		{
			try
			{
				if (!ModelState.IsValid || dto.Ids == null || !dto.Ids.Any())
				{
					return Json(ApiResponseDto<object>.ErrorResult("請選擇要發布的通知"));
				}

				var notifications = await _context.Notifications
					.Where(n => dto.Ids.Contains(n.Id) && n.Email_Status == "draft" && !n.Is_Deleted)
					.ToListAsync();

				if (!notifications.Any())
				{
					return Json(ApiResponseDto<object>.ErrorResult("找不到可發布的草稿通知"));
				}

				var now = DateTime.Now;
				foreach (var notification in notifications)
				{
					notification.Email_Status = "sent";
					notification.Sent_At = now;
					notification.Email_Sent_At = now;
					notification.Updated_At = now;
				}

				// 先保存狀態更新到資料庫
				await _context.SaveChangesAsync();

				// 🚀 批量發送郵件
				int successCount = 0;
				int failCount = 0;

				foreach (var notification in notifications.Where(n => n.Channel?.ToLower() == "email"))
				{
					try
					{
						bool emailSent = await _emailSender.SendNotificationEmailAsync(notification);
						
						// 更新發送狀態
						notification.Email_Status = emailSent ? "sent" : "failed";
						if (!emailSent)
						{
							notification.Email_Retry += 1;
							failCount++;
						}
						else
						{
							successCount++;
						}
						
						_logger.LogInformation("批量發布郵件發送結果: {Result}，收件人: {Email}，通知ID: {Id}", 
							emailSent ? "成功" : "失敗", notification.Email_Address, notification.Id);
					}
					catch (Exception emailEx)
					{
						_logger.LogError(emailEx, "批量發布時發送郵件發生錯誤，收件人: {Email}，通知ID: {Id}", 
							notification.Email_Address, notification.Id);
						notification.Email_Status = "failed";
						notification.Email_Retry += 1;
						failCount++;
					}
				}

				// 批量更新最終狀態
				await _context.SaveChangesAsync();
				
				_logger.LogInformation("批量發布郵件發送完成，成功: {Success} 筆，失敗: {Fail} 筆", 
					successCount, failCount);

				return Json(ApiResponseDto<object>.SuccessResult(
					new { PublishedCount = notifications.Count },
					$"成功發布 {notifications.Count} 筆通知"
				));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "批量發布通知失敗");
				return Json(ApiResponseDto<object>.ErrorResult($"批量發布通知失敗：{ex.Message}"));
			}
		}

		// API: 刪除通知
		[HttpPost("DeleteNotification")]
		public async Task<IActionResult> DeleteNotification([FromBody] DeleteNotificationRequestDto request)
		{
			try
			{
				_logger.LogInformation("接收到刪除通知請求: {@Request}", request);

				// 驗證請求
				if (request?.Ids == null || !request.Ids.Any())
				{
					_logger.LogWarning("刪除請求無效：未提供通知ID");
					return Json(ApiResponseDto<object>.ErrorResult("請提供要刪除的通知ID"));
				}

				// 限制批量操作數量
				if (request.Ids.Count > 1000)
				{
					_logger.LogWarning("批量操作數量超過限制：{Count}", request.Ids.Count);
					return Json(ApiResponseDto<object>.ErrorResult("批量操作最多支援 1000 筆記錄"));
				}

				// 檢查資料庫連接
				var canConnect = await _context.Database.CanConnectAsync();
				if (!canConnect)
				{
					_logger.LogError("資料庫連接失敗");
					return Json(ApiResponseDto<object>.ErrorResult("資料庫連接失敗，請稍後重試"));
				}

				// 開始交易
				using var transaction = await _context.Database.BeginTransactionAsync();

				try
				{
					// 🔧 關鍵修正：詳細查詢和日誌記錄
					_logger.LogInformation("開始查詢要刪除的通知，ID列表: [{Ids}]", string.Join(", ", request.Ids));

					// 首先查詢所有相關的通知（包括已刪除的）
					var allMatchingNotifications = await _context.Notifications
						.Where(n => request.Ids.Contains(n.Id))
						.Select(n => new { n.Id, n.Is_Deleted, n.Email_Address, n.Message })
						.ToListAsync();

					_logger.LogInformation("查詢到 {TotalCount} 筆匹配的通知", allMatchingNotifications.Count);

					// 查找未刪除的通知
					var notificationsToDelete = await _context.Notifications
						.Where(n => request.Ids.Contains(n.Id) && !n.Is_Deleted)
						.ToListAsync();

					_logger.LogInformation("其中 {DeleteableCount} 筆可以刪除", notificationsToDelete.Count);

					// 🔧 修正：如果沒有找到任何匹配的通知
					if (!allMatchingNotifications.Any())
					{
						_logger.LogWarning("未找到任何匹配的通知記錄，請求的ID: [{Ids}]", string.Join(", ", request.Ids));
						return Json(ApiResponseDto<object>.ErrorResult("找不到指定的通知記錄，可能已被刪除或不存在"));
					}

					// 🔧 修正：如果找到了通知但都已被刪除
					if (allMatchingNotifications.Any() && !notificationsToDelete.Any())
					{
						var deletedCount = allMatchingNotifications.Count(n => n.Is_Deleted);
						_logger.LogWarning("找到 {Total} 筆通知，但其中 {Deleted} 筆已被刪除", 
							allMatchingNotifications.Count, deletedCount);
						return Json(ApiResponseDto<object>.ErrorResult("指定的通知已被刪除，無需重複操作"));
					}

					// 標記為刪除
					var updateTime = DateTime.Now;
					foreach (var notification in notificationsToDelete)
					{
						_logger.LogInformation("標記通知為已刪除: ID={Id}, 收件人={Email}", 
							notification.Id, notification.Email_Address);
						notification.Is_Deleted = true;
						notification.Updated_At = updateTime;
					}

					// 保存變更
					await _context.SaveChangesAsync();
					await transaction.CommitAsync();

					// 清除快取
					_memoryCache.Remove("notification_stats");

					_logger.LogInformation("成功刪除 {Count} 筆通知", notificationsToDelete.Count);

					var message = request.Ids.Count == 1 
						? "通知刪除成功" 
						: $"成功刪除 {notificationsToDelete.Count} 筆通知";

					// 🔧 改進：返回更詳細的結果
					var result = new
					{
						DeletedCount = notificationsToDelete.Count,
						RequestedCount = request.Ids.Count,
						AlreadyDeletedCount = allMatchingNotifications.Count(n => n.Is_Deleted),
						NotFoundCount = request.Ids.Count - allMatchingNotifications.Count
					};

					return Json(ApiResponseDto<object>.SuccessResult(result, message));
				}
				catch (Exception innerEx)
				{
					_logger.LogError(innerEx, "刪除通知過程中發生錯誤");
					await transaction.RollbackAsync();
					throw;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "刪除通知失敗，請求: {@Request}", request);
				return Json(ApiResponseDto<object>.ErrorResult($"刪除通知失敗：{ex.Message}"));
			}
		}

		// 🔧 保留舊的 DELETE 方法以支援 RESTful API，但重定向到 POST 方法
		[HttpDelete("DeleteNotification/{id}")]
		public async Task<IActionResult> DeleteNotificationById(int id)
		{
			var request = new DeleteNotificationRequestDto { Ids = new List<int> { id } };
			return await DeleteNotification(request);
		}

		// Helper 方法
		private static string GetCategoryLabel(string category)
		{
			return category?.ToLower() switch
			{
				"order" => "訂單",
				"payment" => "付款",
				"account" => "帳戶",
				"security" => "安全",
				"promotion" => "優惠",
				"system" => "系統",
				"test" => "測試",
				"restock" => "補貨",
				_ => category ?? "未知類別"
			};
		}

		private static string GetEmailStatusLabel(string emailStatus)
		{
			return emailStatus?.ToLower() switch
			{
				"pending" => "待發送",
				"sent" => "已發送",
				"delivered" => "已送達",
				"failed" => "發送失敗",
				"bounce" => "退信",
				"immediate" => "立即發送",
				"scheduled" => "排程發送",
				"draft" => "草稿",
				_ => emailStatus ?? "未知狀態"
			};
		}

		private static string GetChannelLabel(string channel)
		{
			return channel?.ToLower() switch
			{
				"email" => "電子郵件",
				"sms" => "簡訊",
				"push" => "推播通知",
				"internal" => "站內通知",
				_ => channel ?? "未知通道"
			};
		}

		// 輔助方法：獲取相對時間
		private static string GetRelativeTime(DateTime dateTime)
		{
			var timeSpan = DateTime.Now - dateTime;
			if (timeSpan.TotalMinutes < 1) return "剛剛";
			if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}分鐘前";
			if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}小時前";
			if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}天前";
			return dateTime.ToString("MM/dd");
		}

		// 輔助方法：獲取分類圖示
		private static string GetCategoryIcon(string category)
		{
			return category?.ToLower() switch
			{
				"order" => "fas fa-receipt",
				"payment" => "fas fa-credit-card",
				"account" => "fas fa-user",
				"security" => "fas fa-shield-alt",
				"promotion" => "fas fa-gift",
				"system" => "fas fa-cog",
				"test" => "fas fa-flask",
				"restock" => "fas fa-boxes",
				_ => "fas fa-bell"
			};
		}

		// 輔助方法：獲取分類顏色
		private static string GetCategoryColor(string category)
		{
			return category?.ToLower() switch
			{
				"order" => "bg-primary",
				"payment" => "bg-success",
				"account" => "bg-info",
				"security" => "bg-danger",
				"promotion" => "bg-warning",
				"system" => "bg-secondary",
				"test" => "bg-dark",
				"restock" => "bg-primary",
				_ => "bg-primary"
			};
		}

        // 輔助方法：獲取所有使用者郵件地址
        private async Task<List<string>> GetAllUserEmails()
        {
            try
            {
                // 嘗試從 Members 表獲取郵件地址
                if (_context.Members != null)
                {
                    var memberEmails = await _context.Members
                        .Where(m => !string.IsNullOrEmpty(m.Email) && m.IsActive) // 確保使用正確的欄位名稱
                        .Select(m => m.Email)
                        .Distinct()
                        .ToListAsync();

                    if (memberEmails.Any())
                    {
                        _logger.LogInformation("從 Members 表獲取到 {Count} 個郵件地址", memberEmails.Count);
                        return memberEmails;
                    }
                }

                // 如果沒有 Members 表或沒有資料，從現有通知中獲取會員郵件
                var notificationEmails = await _context.Notifications
                    .Where(n => n.Member_Id.HasValue && !string.IsNullOrEmpty(n.Email_Address) && !n.Is_Deleted)
                    .Select(n => n.Email_Address)
                    .Distinct()
                    .ToListAsync();

                if (notificationEmails.Any())
                {
                    _logger.LogInformation("從現有通知獲取到 {Count} 個會員郵件地址", notificationEmails.Count);
                    return notificationEmails;
                }

                // 最後備選方案：提供測試郵件地址
                _logger.LogWarning("無法找到會員郵件地址，使用測試郵件");
                return new List<string>
        {
            "member1@example.com",
            "member2@example.com",
            "member3@example.com",
            "member4@example.com",
            "member5@example.com"
        };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取使用者郵件地址失敗");
                return new List<string> { "fallback@example.com" };
            }
        }

        // API: 取得會員 Email 列表（供前端下拉選擇）
        [HttpGet("GetMemberEmails")]
        public async Task<IActionResult> GetMemberEmails()
        {
            try
            {
                var emails = await GetAllUserEmails();
                // 回傳符合前端預期格式
                return Json(new { success = true, data = emails });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員郵件列表失敗");
                return Json(new { success = false, message = "取得會員郵件失敗" });
            }
        }

        // 輔助方法：獲取所有賣家郵件地址
        private async Task<List<string>> GetAllSellerEmails()
        {
            try
            {
                // 根據資料庫架構圖，使用正確的欄位名稱
                if (_context.Sellers != null && _context.Members != null)
                {
                    var sellerEmails = await _context.Sellers
                        .Where(s => s.IsActive) 
                        .Join(_context.Members,
                            seller => seller.MembersId, 
                            member => member.Id,
                            (seller, member) => member.Email)
                        .Where(email => !string.IsNullOrEmpty(email))
                        .Distinct()
                        .ToListAsync();

                    if (sellerEmails.Any())
                    {
                        _logger.LogInformation("從 Sellers 表獲取到 {Count} 個郵件地址", sellerEmails.Count);
                        return sellerEmails;
                    }
                }

                // 從現有通知中獲取賣家郵件地址
                var notificationEmails = await _context.Notifications
                    .Where(n => n.Seller_Id.HasValue && !string.IsNullOrEmpty(n.Email_Address) && !n.Is_Deleted)
                    .Select(n => n.Email_Address)
                    .Distinct()
                    .ToListAsync();

                if (notificationEmails.Any())
                {
                    _logger.LogInformation("從現有通知獲取到 {Count} 個賣家郵件地址", notificationEmails.Count);
                    return notificationEmails;
                }

                // 備選方案：返回測試賣家郵件
                _logger.LogWarning("無法找到賣家郵件地址，使用測試郵件");
                return new List<string>
        {
            "seller1@example.com",
            "seller2@example.com",
            "seller3@example.com"
        };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家郵件地址失敗");
                return new List<string> { "fallback-seller@example.com" };
            }
        }

        // 業務邏輯驗證方法
        private Dictionary<string, string> ValidateNotificationBusiness(CreateNotificationDto dto)
        {
            var errors = new Dictionary<string, string>();

            // 驗證分類是否有效
            var validCategories = new[] { "order", "payment", "account", "security", "promotion", "system", "test" };
            if (!validCategories.Contains(dto.Category?.ToLower()))
            {
                errors.Add("Category", "無效的通知分類");
            }

            // 驗證通道是否有效
            var validChannels = new[] { "email", "sms", "push", "internal" };
            if (!validChannels.Contains(dto.Channel?.ToLower()))
            {
                errors.Add("Channel", "無效的通知管道");
            }

            // 驗證郵件狀態是否有效
            var validStatuses = new[] { "immediate", "scheduled", "draft" };
            if (!validStatuses.Contains(dto.EmailStatus?.ToLower()))
            {
                errors.Add("EmailStatus", "無效的郵件狀態");
            }

            // 驗證排程時間
            if (dto.EmailStatus?.ToLower() == "scheduled" && dto.SentAt.HasValue)
            {
                try
                {
                    var sentAtUtc = dto.SentAt.Value.ToUniversalTime();
                    var nowUtc = DateTime.UtcNow;
                    // require at least 1 minute in the future to account for clock skew
                    if (sentAtUtc <= nowUtc.AddMinutes(1))
                    {
                        errors.Add("SentAt", "排程時間必須是未來時間");
                    }
                }
                catch
                {
                    errors.Add("SentAt", "排程時間格式錯誤");
                }
            }

            // 驗證郵件地址格式
            if (!string.IsNullOrEmpty(dto.EmailAddress))
            {
                try
                {
                    var mailAddress = new System.Net.Mail.MailAddress(dto.EmailAddress);
                }
                catch
                {
                    errors.Add("EmailAddress", "無效的郵件地址格式");
                }
            }

            return errors;
        }

        // 批量通知業務邏輯驗證
        private Dictionary<string, string> ValidateBulkNotificationBusiness(CreateBulkNotificationDto dto)
        {
            var errors = new Dictionary<string, string>();

            // 驗証分類是否有效
            var validCategories = new[] { "order", "payment", "account", "security", "promotion", "system", "test" };
            if (!validCategories.Contains(dto.Category?.ToLower()))
            {
                errors.Add("Category", "無效的通知分類");
            }

            // 驗證通道是否有效
            var validChannels = new[] { "email", "sms", "push", "internal" };
            if (!validChannels.Contains(dto.Channel?.ToLower()))
            {
                errors.Add("Channel", "無效的通知管道");
            }

            // 驗證郵件狀態是否有效
            var validStatuses = new[] { "immediate", "scheduled", "draft" };
            if (!validStatuses.Contains(dto.EmailStatus?.ToLower()))
            {
                errors.Add("EmailStatus", "無效的郵件狀態");
            }

            // 驗證排程時間
            if (dto.EmailStatus?.ToLower() == "scheduled" && dto.SentAt.HasValue)
            {
                try
                {
                    var sentAtUtc = dto.SentAt.Value.ToUniversalTime();
                    var nowUtc = DateTime.UtcNow;
                    // require at least 1 minute in the future to account for clock skew
                    if (sentAtUtc <= nowUtc.AddMinutes(1))
                    {
                        errors.Add("SentAt", "排程時間必須是未來時間");
                    }
                }
                catch
                {
                    errors.Add("SentAt", "排程時間格式錯誤");
                }
            }

            // 批量特有驗證
            if (dto.TargetType < 1 || dto.TargetType > 3)
            {
                errors.Add("TargetType", "目標類型必須是 1(全部會員)、2(全部廠商) 或 3(指定帳號)");
            }

            // 🔧 啟用詳細的 SpecificAccount 驗證
            if (dto.TargetType == 3)
            {
                if (string.IsNullOrWhiteSpace(dto.SpecificAccount))
                {
                    errors.Add("SpecificAccount", "選擇指定帳號時，必須提供帳號資訊");
                }
                else
                {
                    // 驗證指定帳號的格式
                    try
                    {
                        var mailAddress = new System.Net.Mail.MailAddress(dto.SpecificAccount);
                    }
                    catch
                    {
                        errors.Add("SpecificAccount", "指定帳號必須是有效的郵件地址");
                    }
                }
            }
            // 對於 TargetType = 1 或 2，完全不檢查 SpecificAccount

            return errors;
        }

		// 輔助方法: 添加 PDF 表格單元格
		private void addTableCell(PdfPTable table, string label, string value, Font font)
		{
			table.AddCell(new PdfPCell(new Phrase(label, font)));
			table.AddCell(new PdfPCell(new Phrase(value, font)));
		}

        // API: 測試推播通知數據 (調試用)
        [HttpGet("TestPushData")]
        public async Task<IActionResult> TestPushData()
        {
            try
            {
                _logger.LogInformation("=== 開始測試推播通知數據 ===");

                // 檢查資料庫連接
                var connectionTest = await _context.Database.CanConnectAsync();
                _logger.LogInformation("資料庫連接狀態: {CanConnect}", connectionTest);

                if (!connectionTest)
                {
                    return Json(new { error = "資料庫連接失敗" });
                }

                // 查詢所有通知
                var allNotifications = await _context.Notifications
                    .AsNoTracking()
                    .Where(n => !n.Is_Deleted)
                    .Select(n => new {
                        Id = n.Id,
                        Channel = n.Channel,
                        EmailStatus = n.Email_Status,
                        Message = n.Message,
                        SentAt = n.Sent_At,
                        IsDeleted = n.Is_Deleted
                    })
                    .ToListAsync();

                _logger.LogInformation("總通知數: {Count}", allNotifications.Count);

                // 查詢推播通知
                var pushNotifications = allNotifications.Where(n => n.Channel == "push").ToList();
                _logger.LogInformation("推播通知數: {Count}", pushNotifications.Count);

                // 查詢已發送的推播通知
                var sentPushNotifications = pushNotifications
                    .Where(n => n.EmailStatus == "sent")
                    .ToList();
                _logger.LogInformation("已發送推播通知數: {Count}", sentPushNotifications.Count);

                var result = new
                {
                    DatabaseConnected = connectionTest,
                    TotalNotifications = allNotifications.Count,
                    PushNotifications = pushNotifications.Count,
                    SentPushNotifications = sentPushNotifications.Count,
                    AllNotifications = allNotifications.Take(10), // 只顯示前10筆
                    PushNotificationsSample = sentPushNotifications.Take(5) // 只顯示前5筆推播通知
                };

                _logger.LogInformation("測試結果: {@Result}", result);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "測試推播通知數據失敗");
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
	}
}