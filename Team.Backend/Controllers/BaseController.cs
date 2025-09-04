using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Controllers
{
    public class BaseController : Controller
    {
        protected readonly AppDbContext _context;
        private readonly ILogger<BaseController> _logger;

        // 無參數建構函數 - 用於不需要通知功能的 Controller
        public BaseController()
        {
            _context = null;
            _logger = null;
        }

        // 有參數建構函數 - 用於需要通知功能的 Controller
        public BaseController(AppDbContext context, ILogger<BaseController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = context.HttpContext.Session.GetInt32("UserId");

            // 如果未登入，就跳回登入頁面
            if (userId == null)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // 防止瀏覽器快取已登入畫面（避免按返回鍵）
            context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.HttpContext.Response.Headers["Pragma"] = "no-cache";
            context.HttpContext.Response.Headers["Expires"] = "0";

            // 載入通知數據 (只在有 context 和 logger 時執行)
            if (_context != null && _logger != null)
            {
                try
                {
                    LoadNotificationData().Wait();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "載入通知數據失敗");
                    // 設定預設值，避免頁面出錯
                    ViewBag.NotificationData = new
                    {
                        HasNotifications = false,
                        Notifications = new List<object>(),
                        UnreadCount = "0"
                    };
                }
            }

            base.OnActionExecuting(context);
        }

        private async Task LoadNotificationData()
        {
            try
            {
                const int limit = 8; // 限制顯示的通知數量

                // 檢查資料庫連接
                var connectionTest = await _context.Database.CanConnectAsync();
                if (!connectionTest)
                {
                    _logger?.LogWarning("無法連接到資料庫，使用預設通知數據");
                    SetDefaultNotificationData();
                    return;
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
                                   n.Channel == "push");

                // 設定 ViewBag 數據
                ViewBag.NotificationData = new
                {
                    HasNotifications = notificationItems.Any(),
                    Notifications = notificationItems,
                    UnreadCount = unreadCount > 99 ? "99+" : unreadCount.ToString()
                };

                _logger?.LogInformation("成功載入通知數據，共 {Count} 筆推播通知，未讀 {UnreadCount} 筆", 
                    notificationItems.Count, unreadCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "載入通知數據時發生錯誤");
                SetDefaultNotificationData();
            }
        }

        private void SetDefaultNotificationData()
        {
            ViewBag.NotificationData = new
            {
                HasNotifications = false,
                Notifications = new List<object>(),
                UnreadCount = "0"
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
    }
}