// ChartNotificationController.cs - �q���Ϫ��

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

        // �q���Ϫ���
        [HttpGet]
        public IActionResult ChartNotification()
        {
            _logger.LogInformation("��ܳq���Ϫ���");
            
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

        // ����έp���
        [HttpGet]
        [Route("ChartNotification/GetStatistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                _logger.LogInformation("�}�l����q���έp���");

                // �ˬd��Ʈw�s��
                var connectionTest = await _context.Database.CanConnectAsync();
                if (!connectionTest)
                {
                    _logger.LogError("�L�k�s�����Ʈw");
                    return Json(new { success = false, message = "��Ʈw�s������" });
                }

                // �q��Ʈw�p��έp���
                var stats = await CalculateNotificationStatistics();

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "����q���έp��Ʈɵo�Ϳ��~");
                return Json(new { success = false, message = "����έp��Ʈɵo�Ϳ��~: " + ex.Message });
            }
        }

        // �ץX�έp��Ƭ�Excel
        [HttpGet]
        [Route("ChartNotification/ExportStatistics")]
        public async Task<IActionResult> ExportStatistics()
        {
            try
            {
                _logger.LogInformation("�}�l�ץX�q���έp���");

                // ����έp���
                var stats = await CalculateNotificationStatistics();

                // �إ�Excel�u�@ï
                using (var workbook = new XLWorkbook())
                {
                    // �[�J�����u�@��
                    var overviewSheet = workbook.Worksheets.Add("�q���έp����");
                    
                    // ���D
                    overviewSheet.Cell(1, 1).Value = "�q���έp���i";
                    overviewSheet.Cell(1, 1).Style.Font.Bold = true;
                    overviewSheet.Cell(1, 1).Style.Font.FontSize = 16;
                    overviewSheet.Range(1, 1, 1, 3).Merge();
                    
                    // ���ͮɶ�
                    overviewSheet.Cell(2, 1).Value = "���ͮɶ�:";
                    overviewSheet.Cell(2, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    overviewSheet.Range(2, 1, 2, 3).Merge();
                    
                    // �������
                    overviewSheet.Cell(4, 1).Value = "����";
                    overviewSheet.Cell(4, 2).Value = "�ƭ�";
                    overviewSheet.Cell(5, 1).Value = "�`�q����";
                    overviewSheet.Cell(5, 2).Value = stats.TotalCount;
                    overviewSheet.Cell(6, 1).Value = "�w�e�F��";
                    overviewSheet.Cell(6, 2).Value = stats.DeliveredCount;
                    overviewSheet.Cell(7, 1).Value = "���Ѽ�";
                    overviewSheet.Cell(7, 2).Value = stats.FailedCount;
                    overviewSheet.Cell(8, 1).Value = "����q����";
                    overviewSheet.Cell(8, 2).Value = stats.TodayCount;
                    overviewSheet.Cell(9, 1).Value = "�Ƶ{��";
                    overviewSheet.Cell(9, 2).Value = stats.ScheduledCount;
                    overviewSheet.Cell(10, 1).Value = "���\�v";
                    overviewSheet.Cell(10, 2).Value = stats.SuccessRate + "%";
                    
                    // �[�J�����έp�u�@��
                    var categorySheet = workbook.Worksheets.Add("�����έp");
                    categorySheet.Cell(1, 1).Value = "����";
                    categorySheet.Cell(1, 2).Value = "�ƶq";
                    categorySheet.Cell(1, 3).Value = "�ʤ���";
                    
                    int row = 2;
                    foreach (var category in stats.CategoryStats)
                    {
                        categorySheet.Cell(row, 1).Value = category.Label;
                        categorySheet.Cell(row, 2).Value = category.Count;
                        categorySheet.Cell(row, 3).Value = category.Percentage + "%";
                        row++;
                    }
                    
                    // �[�J���A�έp�u�@��
                    var statusSheet = workbook.Worksheets.Add("���A�έp");
                    statusSheet.Cell(1, 1).Value = "���A";
                    statusSheet.Cell(1, 2).Value = "�ƶq";
                    statusSheet.Cell(1, 3).Value = "�ʤ���";
                    
                    row = 2;
                    foreach (var status in stats.StatusStats)
                    {
                        statusSheet.Cell(row, 1).Value = status.Label;
                        statusSheet.Cell(row, 2).Value = status.Count;
                        statusSheet.Cell(row, 3).Value = status.Percentage + "%";
                        row++;
                    }
                    
                    // �[�J�޹D�έp�u�@��
                    var channelSheet = workbook.Worksheets.Add("�޹D�έp");
                    channelSheet.Cell(1, 1).Value = "�޹D";
                    channelSheet.Cell(1, 2).Value = "�ƶq";
                    channelSheet.Cell(1, 3).Value = "�ʤ���";
                    
                    row = 2;
                    foreach (var channel in stats.ChannelStats)
                    {
                        channelSheet.Cell(row, 1).Value = channel.Label;
                        channelSheet.Cell(row, 2).Value = channel.Count;
                        channelSheet.Cell(row, 3).Value = channel.Percentage + "%";
                        row++;
                    }
                    
                    // �榡�ƩҦ��u�@��
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        worksheet.Columns().AdjustToContents();
                        worksheet.Row(1).Style.Font.Bold = true;
                    }
                    
                    // �O�s��O����y
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0;
                        
                        // �^���ɮ�
                        return File(
                            stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"�q���έp���i_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�ץX�q���έp��Ʈɵo�Ϳ��~");
                return Json(new { success = false, message = "�ץX�έp��Ʈɵo�Ϳ��~: " + ex.Message });
            }
        }

        // �p��q���έp���
        private async Task<dynamic> CalculateNotificationStatistics()
        {
            // �d�ߩҦ����R�����q��
            var notifications = await _context.Notifications
                .Where(n => !n.Is_Deleted)
                .ToListAsync();

            // �p�G�S����ơA�^�Ǽ������
            if (notifications == null || !notifications.Any())
            {
                return GenerateMockStatistics();
            }

            // �p��򥻲έp���
            var totalCount = notifications.Count;
            var deliveredCount = notifications.Count(n => n.Email_Status == "sent" || n.Email_Status == "delivered");
            var failedCount = notifications.Count(n => n.Email_Status == "failed");
            var todayCount = notifications.Count(n => n.Created_At.Date == DateTime.Now.Date);
            var scheduledCount = notifications.Count(n => n.Email_Status == "scheduled");
            var successRate = totalCount > 0 ? Math.Round((double)deliveredCount / totalCount * 100, 1) : 0;

            // �����έp
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

            // ���A�έp
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

            // �޹D�έp
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

            // �^�ǵ��G
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

        // ���ͼ����έp���
        private dynamic GenerateMockStatistics()
        {
            // ��������
            var totalCount = 1250;
            var deliveredCount = 1150;
            var failedCount = 50;
            var todayCount = 35;
            var scheduledCount = 50;
            var successRate = 92.0;

            // ���������έp
            var categoryStats = new[]
            {
                new { Label = "�q��", Count = 450, Percentage = 36.0 },
                new { Label = "�t��", Count = 350, Percentage = 28.0 },
                new { Label = "�u�f", Count = 250, Percentage = 20.0 },
                new { Label = "�w��", Count = 120, Percentage = 9.6 },
                new { Label = "�b��", Count = 80, Percentage = 6.4 }
            };

            // �������A�έp
            var statusStats = new[]
            {
                new { Label = "�w�o�e", Count = 1150, Percentage = 92.0 },
                new { Label = "�Ƶ{��", Count = 50, Percentage = 4.0 },
                new { Label = "����", Count = 50, Percentage = 4.0 }
            };

            // �����޹D�έp
            var channelStats = new[]
            {
                new { Label = "�q�l�l��", Count = 750, Percentage = 60.0 },
                new { Label = "�����q��", Count = 300, Percentage = 24.0 },
                new { Label = "²�T", Count = 150, Percentage = 12.0 },
                new { Label = "����", Count = 50, Percentage = 4.0 }
            };

            // �^�Ǽ������
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

        // ���U��k�G���o��������
        private string GetCategoryLabel(string category)
        {
            if (string.IsNullOrEmpty(category))
                return "������";

            var labelMap = new Dictionary<string, string>
            {
                { "order", "�q��" },
                { "payment", "�I��" },
                { "account", "�b��" },
                { "security", "�w��" },
                { "promotion", "�u�f" },
                { "system", "�t��" },
                { "test", "����" },
                { "restock", "�ɳf" }
            };

            return labelMap.ContainsKey(category.ToLower()) ? labelMap[category.ToLower()] : category;
        }

        // ���U��k�G���o���A����
        private string GetStatusLabel(string status)
        {
            if (string.IsNullOrEmpty(status))
                return "����";

            var labelMap = new Dictionary<string, string>
            {
                { "sent", "�w�o�e" },
                { "delivered", "�w�e�F" },
                { "failed", "����" },
                { "scheduled", "�Ƶ{��" },
                { "draft", "��Z" },
                { "pending", "�ݳB�z" },
                { "processing", "�B�z��" },
                { "canceled", "�w����" },
                { "immediate", "�Y��o�e" }
            };

            return labelMap.ContainsKey(status.ToLower()) ? labelMap[status.ToLower()] : status;
        }

        // ���U��k�G���o�޹D����
        private string GetChannelLabel(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return "����";

            var labelMap = new Dictionary<string, string>
            {
                { "email", "�q�l�l��" },
                { "sms", "²�T" },
                { "push", "����" },
                { "internal", "�����q��" },
                { "whatsapp", "WhatsApp" },
                { "line", "LINE" },
                { "wechat", "�L�H" }
            };

            return labelMap.ContainsKey(channel.ToLower()) ? labelMap[channel.ToLower()] : channel;
        }
    }
}