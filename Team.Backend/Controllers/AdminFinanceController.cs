using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using Team.Backend.Services;

namespace Team.Backend.Controllers
{
    public class AdminFinanceController : BaseController
    {
        private readonly AdminFinanceService _financeService;
        private readonly ILogger<AdminFinanceController> _logger;

        public AdminFinanceController(AdminFinanceService financeService, AppDbContext context, ILogger<AdminFinanceController> logger)
            : base(context, logger)
        {
            _financeService = financeService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? year, int? month)
        {
            // 如果沒有傳入參數，則預設為當前年份和月份
            var selectedYear = year ?? DateTime.Now.Year;
            var selectedMonth = month ?? DateTime.Now.Month;

            // 將選中的年份和月份存入 ViewBag，以便在 View 中使用
            ViewBag.SelectedYear = selectedYear;
            ViewBag.SelectedMonth = selectedMonth;

            // 添加調試日誌
            _logger.LogInformation("AdminFinance Index called with Year: {Year}, Month: {Month}", selectedYear, selectedMonth);

            try 
            {
                // 呼叫服務層的方法，並傳入年份和月份
                var model = await _financeService.GetDashboardAsync(selectedYear, selectedMonth);
                
                // 添加數據調試日誌
                _logger.LogInformation("Dashboard data - TotalRevenue: {TotalRevenue}, TotalMembers: {TotalMembers}, MonthlyRevenues Count: {Count}", 
                    model.TotalRevenue, model.TotalMembers, model.MonthlyRevenues?.Count ?? 0);
                
                if (model.MonthlyRevenues?.Any() == true)
                {
                    var sampleRevenue = model.MonthlyRevenues.First();
                    _logger.LogInformation("Sample MonthlyRevenue - Month: {Month}, Revenue: {Revenue}", 
                        sampleRevenue.Month, sampleRevenue.Revenue);
                }
                
                if (model.CategorySales?.Any() == true)
                {
                    var sampleCategory = model.CategorySales.First();
                    _logger.LogInformation("Sample CategorySales - Name: {Name}, Sales: {Sales}", 
                        sampleCategory.CategoryName, sampleCategory.Sales);
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading AdminFinance dashboard");
                return View(new FinanceDashboardViewModel());
            }
        }

        /// <summary>
        /// 營收分析頁面
        /// </summary>
        public async Task<IActionResult> Revenue()
        {
            var model = await _financeService.GetRevenueAnalysisAsync();
            return View(model);
        }

        /// <summary>
        /// 訂單統計頁面
        /// </summary>
        public async Task<IActionResult> Orders()
        {
            var model = await _financeService.GetOrderStatisticsAsync();
            return View(model);
        }

        /// <summary>
        /// 點數管理頁面
        /// </summary>
        public async Task<IActionResult> Points()
        {
            try 
            {
                var model = await _financeService.GetPointsManagementAsync();
                _logger.LogInformation("點數管理頁面載入成功，TopPointsHolders數量: {Count}", model.TopPointsHolders?.Count ?? 0);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入點數管理頁面時發生錯誤");
                return View(new PointsManagementViewModel());
            }
        }

        /// <summary>
        /// 資料庫診斷 API - 檢查數據狀態
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiagnoseData()
        {
            try
            {
                _logger.LogInformation("開始資料庫診斷...");

                var diagnosis = new
                {
                    // 基本統計
                    TotalMembers = await _context.Members.CountAsync(),
                    TotalOrders = await _context.Orders.CountAsync(),
                    TotalOrderDetails = await _context.OrderDetails.CountAsync(),
                    TotalProducts = await _context.Products.CountAsync(),
                    
                    // 訂單狀態分析
                    OrdersByStatus = await _context.Orders
                        .GroupBy(o => o.OrderStatus)
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .ToListAsync(),
                    
                    // 最近的訂單
                    RecentOrders = await _context.Orders
                        .OrderByDescending(o => o.CreatedAt)
                        .Take(5)
                        .Select(o => new
                        {
                            o.Id,
                            o.OrderStatus,
                            o.TotalAmount,
                            o.CreatedAt,
                            Year = o.CreatedAt.Year,
                            Month = o.CreatedAt.Month
                        })
                        .ToListAsync(),
                    
                    // 當前年月的訂單
                    CurrentYearMonthOrders = await _context.Orders
                        .Where(o => o.CreatedAt.Year == DateTime.Now.Year && 
                                   o.CreatedAt.Month == DateTime.Now.Month)
                        .Select(o => new
                        {
                            o.Id,
                            o.OrderStatus,
                            o.TotalAmount,
                            o.CreatedAt
                        })
                        .ToListAsync(),
                    
                    // 完成狀態的訂單
                    CompletedOrders = await _context.Orders
                        .Where(o => o.OrderStatus == "Completed" || o.OrderStatus == "已完成")
                        .Take(5)
                        .Select(o => new
                        {
                            o.Id,
                            o.OrderStatus,
                            o.TotalAmount,
                            o.CreatedAt
                        })
                        .ToListAsync(),
                    
                    // 產品分類狀況
                    CategoryStructure = await _context.Products
                        .Include(p => p.SubCategory)
                        .ThenInclude(sc => sc.Category)
                        .Where(p => p.SubCategory != null && p.SubCategory.Category != null)
                        .GroupBy(p => p.SubCategory.Category.Name)
                        .Select(g => new { CategoryName = g.Key, ProductCount = g.Count() })
                        .ToListAsync(),
                    
                    DiagnoseTime = DateTime.Now
                };

                _logger.LogInformation("診斷完成 - 總訂單數: {TotalOrders}, 完成訂單數: {CompletedCount}", 
                    diagnosis.TotalOrders, diagnosis.CompletedOrders.Count);

                return Json(diagnosis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "資料庫診斷失敗");
                return Json(new { error = ex.Message });
            }
        }
    }
}
