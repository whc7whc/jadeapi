// ChartNotificationController.cs - 通知圖表控制器

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ClosedXML.Excel;
using System.IO;
using System.Data;

namespace Team.Backend.Controllers
{
    public class ChartNotificationController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ChartNotificationController> _logger;

        public ChartNotificationController(AppDbContext context, ILogger<ChartNotificationController> logger)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

        // 通知圖表頁面
        [HttpGet]
        public IActionResult ChartNotification()
        {
            _logger.LogInformation("顯示通知圖表頁面");
            
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

            return View("ChartNotification", viewModel);
        }

        // 獲取統計資料
        [HttpGet]
        [Route("ChartNotification/GetStatistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                _logger.LogInformation("開始獲取通知統計資料");

                // 檢查資料庫連接
                var connectionTest = await _context.Database.CanConnectAsync();
                if (!connectionTest)
                {
                    _logger.LogError("無法連接到資料庫");
                    return Json(new { success = false, message = "資料庫連接失敗" });
                }

                // 從資料庫計算統計資料
                var stats = await CalculateNotificationStatistics();

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取通知統計資料時發生錯誤");
                return Json(new { success = false, message = "獲取統計資料時發生錯誤: " + ex.Message });
            }
        }

        // 匯出統計資料為Excel
        [HttpGet]
        [Route("ChartNotification/ExportStatistics")]
        public async Task<IActionResult> ExportStatistics()
        {
            try
            {
                _logger.LogInformation("開始匯出通知統計資料");

                // 獲取統計資料
                var stats = await CalculateNotificationStatistics();

                // 建立Excel工作簿
                using (var workbook = new XLWorkbook())
                {
                    // 加入概覽工作表
                    var overviewSheet = workbook.Worksheets.Add("通知統計概覽");
                    
                    // 標題
                    overviewSheet.Cell(1, 1).Value = "通知統計報告";
                    overviewSheet.Cell(1, 1).Style.Font.Bold = true;
                    overviewSheet.Cell(1, 1).Style.Font.FontSize = 16;
                    overviewSheet.Range(1, 1, 1, 3).Merge();
                    
                    // 產生時間
                    overviewSheet.Cell(2, 1).Value = "產生時間:";
                    overviewSheet.Cell(2, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    overviewSheet.Range(2, 1, 2, 3).Merge();
                    
                    // 概覽資料
                    overviewSheet.Cell(4, 1).Value = "項目";
                    overviewSheet.Cell(4, 2).Value = "數值";
                    overviewSheet.Cell(5, 1).Value = "總通知數";
                    overviewSheet.Cell(5, 2).Value = stats.TotalCount;
                    overviewSheet.Cell(6, 1).Value = "已送達數";
                    overviewSheet.Cell(6, 2).Value = stats.DeliveredCount;
                    overviewSheet.Cell(7, 1).Value = "失敗數";
                    overviewSheet.Cell(7, 2).Value = stats.FailedCount;
                    overviewSheet.Cell(8, 1).Value = "今日通知數";
                    overviewSheet.Cell(8, 2).Value = stats.TodayCount;
                    overviewSheet.Cell(9, 1).Value = "排程數";
                    overviewSheet.Cell(9, 2).Value = stats.ScheduledCount;
                    overviewSheet.Cell(10, 1).Value = "成功率";
                    overviewSheet.Cell(10, 2).Value = stats.SuccessRate + "%";
                    
                    // 加入分類統計工作表
                    var categorySheet = workbook.Worksheets.Add("分類統計");
                    categorySheet.Cell(1, 1).Value = "分類";
                    categorySheet.Cell(1, 2).Value = "數量";
                    categorySheet.Cell(1, 3).Value = "百分比";
                    
                    int row = 2;
                    foreach (var category in stats.CategoryStats)
                    {
                        categorySheet.Cell(row, 1).Value = category.Label;
                        categorySheet.Cell(row, 2).Value = category.Count;
                        categorySheet.Cell(row, 3).Value = category.Percentage + "%";
                        row++;
                    }
                    
                    // 加入狀態統計工作表
                    var statusSheet = workbook.Worksheets.Add("狀態統計");
                    statusSheet.Cell(1, 1).Value = "狀態";
                    statusSheet.Cell(1, 2).Value = "數量";
                    statusSheet.Cell(1, 3).Value = "百分比";
                    
                    row = 2;
                    foreach (var status in stats.StatusStats)
                    {
                        statusSheet.Cell(row, 1).Value = status.Label;
                        statusSheet.Cell(row, 2).Value = status.Count;
                        statusSheet.Cell(row, 3).Value = status.Percentage + "%";
                        row++;
                    }
                    
                    // 加入管道統計工作表
                    var channelSheet = workbook.Worksheets.Add("管道統計");
                    channelSheet.Cell(1, 1).Value = "管道";
                    channelSheet.Cell(1, 2).Value = "數量";
                    channelSheet.Cell(1, 3).Value = "百分比";
                    
                    row = 2;
                    foreach (var channel in stats.ChannelStats)
                    {
                        channelSheet.Cell(row, 1).Value = channel.Label;
                        channelSheet.Cell(row, 2).Value = channel.Count;
                        channelSheet.Cell(row, 3).Value = channel.Percentage + "%";
                        row++;
                    }
                    
                    // 格式化所有工作表
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        worksheet.Columns().AdjustToContents();
                        worksheet.Row(1).Style.Font.Bold = true;
                    }
                    
                    // 保存到記憶體流
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0;
                        
                        // 回傳檔案
                        return File(
                            stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"通知統計報告_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "匯出通知統計資料時發生錯誤");
                return Json(new { success = false, message = "匯出統計資料時發生錯誤: " + ex.Message });
            }
        }

        // 計算通知統計資料
        private async Task<dynamic> CalculateNotificationStatistics()
        {
            // 查詢所有未刪除的通知
            var notifications = await _context.Notifications
                .Where(n => !n.Is_Deleted)
                .ToListAsync();

            // 如果沒有資料，回傳模擬資料
            if (notifications == null || !notifications.Any())
            {
                return GenerateMockStatistics();
            }

            // 計算基本統計資料
            var totalCount = notifications.Count;
            var deliveredCount = notifications.Count(n => n.Email_Status == "sent" || n.Email_Status == "delivered");
            var failedCount = notifications.Count(n => n.Email_Status == "failed");
            var todayCount = notifications.Count(n => n.Created_At.Date == DateTime.Now.Date);
            var scheduledCount = notifications.Count(n => n.Email_Status == "scheduled");
            var successRate = totalCount > 0 ? Math.Round((double)deliveredCount / totalCount * 100, 1) : 0;

            // 分類統計
            var categoryStats = notifications
                .GroupBy(n => n.Category)
                .Select(g => new
                {
                    Label = GetCategoryLabel(g.Key),
                    Count = g.Count(),
                    Percentage = Math.Round((double)g.Count() / totalCount * 100, 1)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // 狀態統計
            var statusStats = notifications
                .GroupBy(n => n.Email_Status)
                .Select(g => new
                {
                    Label = GetStatusLabel(g.Key),
                    Count = g.Count(),
                    Percentage = Math.Round((double)g.Count() / totalCount * 100, 1)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // 管道統計
            var channelStats = notifications
                .GroupBy(n => n.Channel)
                .Select(g => new
                {
                    Label = GetChannelLabel(g.Key),
                    Count = g.Count(),
                    Percentage = Math.Round((double)g.Count() / totalCount * 100, 1)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // 回傳結果
            return new
            {
                TotalCount = totalCount,
                DeliveredCount = deliveredCount,
                FailedCount = failedCount,
                TodayCount = todayCount,
                ScheduledCount = scheduledCount,
                SuccessRate = successRate,
                CategoryStats = categoryStats,
                StatusStats = statusStats,
                ChannelStats = channelStats
            };
        }

        // 產生模擬統計資料
        private dynamic GenerateMockStatistics()
        {
            // 模擬概覽
            var totalCount = 1250;
            var deliveredCount = 1150;
            var failedCount = 50;
            var todayCount = 35;
            var scheduledCount = 50;
            var successRate = 92.0;

            // 模擬分類統計
            var categoryStats = new[]
            {
                new { Label = "訂單", Count = 450, Percentage = 36.0 },
                new { Label = "系統", Count = 350, Percentage = 28.0 },
                new { Label = "優惠", Count = 250, Percentage = 20.0 },
                new { Label = "安全", Count = 120, Percentage = 9.6 },
                new { Label = "帳戶", Count = 80, Percentage = 6.4 }
            };

            // 模擬狀態統計
            var statusStats = new[]
            {
                new { Label = "已發送", Count = 1150, Percentage = 92.0 },
                new { Label = "排程中", Count = 50, Percentage = 4.0 },
                new { Label = "失敗", Count = 50, Percentage = 4.0 }
            };

            // 模擬管道統計
            var channelStats = new[]
            {
                new { Label = "電子郵件", Count = 750, Percentage = 60.0 },
                new { Label = "推播通知", Count = 300, Percentage = 24.0 },
                new { Label = "簡訊", Count = 150, Percentage = 12.0 },
                new { Label = "站內", Count = 50, Percentage = 4.0 }
            };

            // 回傳模擬資料
            return new
            {
                TotalCount = totalCount,
                DeliveredCount = deliveredCount,
                FailedCount = failedCount,
                TodayCount = todayCount,
                ScheduledCount = scheduledCount,
                SuccessRate = successRate,
                CategoryStats = categoryStats,
                StatusStats = statusStats,
                ChannelStats = channelStats
            };
        }

        // 輔助方法：取得分類標籤
        private string GetCategoryLabel(string category)
        {
            if (string.IsNullOrEmpty(category))
                return "未分類";

            var labelMap = new Dictionary<string, string>
            {
                { "order", "訂單" },
                { "payment", "付款" },
                { "account", "帳戶" },
                { "security", "安全" },
                { "promotion", "優惠" },
                { "system", "系統" },
                { "test", "測試" },
                { "restock", "補貨" }
            };

            return labelMap.ContainsKey(category.ToLower()) ? labelMap[category.ToLower()] : category;
        }

        // 輔助方法：取得狀態標籤
        private string GetStatusLabel(string status)
        {
            if (string.IsNullOrEmpty(status))
                return "未知";

            var labelMap = new Dictionary<string, string>
            {
                { "sent", "已發送" },
                { "delivered", "已送達" },
                { "failed", "失敗" },
                { "scheduled", "排程中" },
                { "draft", "草稿" },
                { "pending", "待處理" },
                { "processing", "處理中" },
                { "canceled", "已取消" },
                { "immediate", "即刻發送" }
            };

            return labelMap.ContainsKey(status.ToLower()) ? labelMap[status.ToLower()] : status;
        }

        // 輔助方法：取得管道標籤
        private string GetChannelLabel(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return "未知";

            var labelMap = new Dictionary<string, string>
            {
                { "email", "電子郵件" },
                { "sms", "簡訊" },
                { "push", "推播" },
                { "internal", "站內通知" },
                { "whatsapp", "WhatsApp" },
                { "line", "LINE" },
                { "wechat", "微信" }
            };

            return labelMap.ContainsKey(channel.ToLower()) ? labelMap[channel.ToLower()] : channel;
        }
    }
}