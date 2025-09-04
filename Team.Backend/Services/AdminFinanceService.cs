using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using Team.Backend.Repositories;
using static Team.Backend.Constants.FinanceConstants;

namespace Team.Backend.Services
{
    public class AdminFinanceService
    {
        private readonly IFinanceRepository _financeRepository;

        public AdminFinanceService(IFinanceRepository financeRepository)
        {
            _financeRepository = financeRepository;
        }

        public async Task<FinanceDashboardViewModel> GetDashboardAsync(int year, int month)
        {
            try
            {
                var model = new FinanceDashboardViewModel
                {
                    TotalMembers = await _financeRepository.GetTotalMembersAsync(),
                    TotalRevenue = await _financeRepository.GetTotalRevenueAsync(year, month),
                    // 修正：改為顯示歷史總發放點數（語意統一）
                    TotalPointsIssued = await _financeRepository.GetTotalPointsIssuedAsync(0, 0), // 使用 0,0 代表全部歷史
                    TotalPointsUsed = await _financeRepository.GetTotalPointsUsedAsync(),

                    // 關鍵指標
                    AverageOrderValue = await _financeRepository.GetAverageOrderValueAsync(year, month),
                    ActiveMembersThisMonth = await _financeRepository.GetActiveMembersThisMonthAsync(year, month),
                    GrowthRate = await _financeRepository.GetGrowthRateAsync(year, month),
                    NewMembersThisMonth = await _financeRepository.GetNewMembersThisMonthAsync(year, month),
                    RefundAmount = await _financeRepository.GetRefundAmountAsync(year, month),
                    CouponUsage = await _financeRepository.GetCouponUsageAsync(year, month),
                    CouponDiscount = await _financeRepository.GetCouponDiscountAsync(year, month),

                    // 營運效率
                    ProfitMargin = await _financeRepository.GetProfitMarginAsync(year, month),
                    TopSellingProducts = await _financeRepository.GetTopSellingProductsCountAsync(year, month),
                    MemberRetentionRate = await _financeRepository.GetMemberRetentionRateAsync(year, month)
                };

                // 修正：加入 null 防護 - 趨勢分析數據
                var monthlyRevenues = (await _financeRepository.GetMonthlyRevenuesAsync(year))
                    ?? new List<(string Month, decimal Revenue, int OrderCount)>();
                model.MonthlyRevenues = monthlyRevenues.Select(mr => new MonthlyRevenueData
                {
                    Month = mr.Month,
                    Revenue = mr.Revenue,
                    OrderCount = mr.OrderCount
                }).ToList();

                var categorySales = (await _financeRepository.GetCategorySalesAsync(year, month))
                    ?? new List<(string CategoryName, decimal Sales, int ProductCount)>();
                var colors = new[] { "#4e73df", "#1cc88a", "#36b9cc", "#f6c23e", "#e74a3b" };
                model.CategorySales = categorySales.Select((cs, index) => new CategorySalesData
                {
                    CategoryName = cs.CategoryName,
                    Sales = cs.Sales,
                    ProductCount = cs.ProductCount,
                    Color = colors[index % colors.Length]
                }).ToList();

                // 移除點數流動數據 - 不再需要虛假數據
                model.PointsFlow = new List<PointsFlowData>();

                return model;
            }
            catch (Exception ex)
            {
                // 記錄錯誤並返回空數據
                Console.WriteLine($"Error in GetDashboardAsync: {ex.Message}");
                
                return new FinanceDashboardViewModel(); // 返回空的模型
            }
        }

        // 營收分析數據
        public async Task<RevenueAnalysisViewModel> GetRevenueAnalysisAsync()
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;
                var lastYear = currentYear - 1;
                var lastMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                var lastMonthYear = currentMonth == 1 ? lastYear : currentYear;

                var model = new RevenueAnalysisViewModel
                {
                    // 基本營收數據
                    TodayRevenue = await _financeRepository.GetTodayRevenueAsync(),
                    YesterdayRevenue = await _financeRepository.GetYesterdayRevenueAsync(),
                    WeekRevenue = await _financeRepository.GetWeekRevenueAsync(),
                    LastWeekRevenue = await _financeRepository.GetLastWeekRevenueAsync(),
                    MonthRevenue = await _financeRepository.GetTotalRevenueAsync(currentYear, currentMonth),
                    LastMonthRevenue = await _financeRepository.GetTotalRevenueAsync(lastMonthYear, lastMonth),
                    YearRevenue = await _financeRepository.GetYearRevenueAsync(currentYear),
                    LastYearRevenue = await _financeRepository.GetYearRevenueAsync(lastYear)
                };

                // 修正：加入 null 防護 - 圖表數據
                var categorySales = (await _financeRepository.GetCategorySalesAsync(currentYear, currentMonth))
                    ?? new List<(string CategoryName, decimal Sales, int ProductCount)>();
                var colors = new[] { "#4e73df", "#1cc88a", "#36b9cc", "#f6c23e", "#e74a3b" };
                model.CategorySales = categorySales.Select((cs, index) => new CategorySalesData
                {
                    CategoryName = cs.CategoryName,
                    Sales = cs.Sales,
                    ProductCount = cs.ProductCount,
                    Color = colors[index % colors.Length]
                }).ToList();

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRevenueAnalysisAsync: {ex.Message}");
                return new RevenueAnalysisViewModel();
            }
        }

        // 修正：訂單統計數據加入 null 防護
        public async Task<OrderStatisticsViewModel> GetOrderStatisticsAsync()
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                var model = new OrderStatisticsViewModel
                {
                    TodayOrdersCount = await _financeRepository.GetTodayOrdersCountAsync(),
                    PendingOrdersCount = await _financeRepository.GetPendingOrdersCountAsync(),
                    AverageOrderValue = await _financeRepository.GetAverageOrderValueAsync(currentYear, currentMonth),
                    OrderCompletionRate = await _financeRepository.GetOrderCompletionRateAsync(currentYear, currentMonth),
                    ReturnRate = await _financeRepository.GetReturnRateAsync(currentYear, currentMonth),
                    TotalRevenue = await _financeRepository.GetTotalRevenueAsync(currentYear, currentMonth)
                };

                // 修正：加入 null 防護 - 趨勢數據
                var dailyTrend = (await _financeRepository.GetDailyOrderTrendAsync(7))
                    ?? new List<(string Date, int OrderCount, int CompletedCount)>();
                model.DailyOrderTrend = dailyTrend.Select(dt => new DailyOrderTrendData
                {
                    Date = dt.Date,
                    OrderCount = dt.OrderCount,
                    CompletedCount = dt.CompletedCount
                }).ToList();

                // 修正：加入 null 防護 - 訂單狀態分布
                var statusDistribution = (await _financeRepository.GetOrderStatusDistributionAsync(currentYear, currentMonth))
                    ?? new List<(string Status, int Count, decimal Percentage)>();
                var statusColors = new[] { "#1cc88a", "#f6c23e", "#e74a3b", "#36b9cc", "#4e73df" };
                model.OrderStatusDistribution = statusDistribution.Select((sd, index) => new OrderStatusData
                {
                    Status = sd.Status,
                    Count = sd.Count,
                    Percentage = sd.Percentage,
                    Color = statusColors[index % statusColors.Length]
                }).ToList();

                // 修正：加入 null 防護 - 時間分布
                var timeDistribution = (await _financeRepository.GetOrderTimeDistributionAsync(currentYear, currentMonth))
                    ?? new List<(string Hour, int OrderCount)>();
                model.OrderTimeDistribution = timeDistribution.Select(td => new OrderTimeData
                {
                    Hour = td.Hour,
                    OrderCount = td.OrderCount
                }).ToList();

                // 計算統計摘要
                model.TotalOrders = model.DailyOrderTrend.Sum(dt => dt.OrderCount);
                model.CompletedOrders = model.DailyOrderTrend.Sum(dt => dt.CompletedCount);
                // 修正：改用更穩定的狀態匹配邏輯
                model.CanceledOrders = model.OrderStatusDistribution
                    .Where(os => os.Status.Contains("取消") || os.Status.Contains("Canceled") || os.Status.Contains("已取消"))
                    .Sum(os => os.Count);

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetOrderStatisticsAsync: {ex.Message}");
                return new OrderStatisticsViewModel();
            }
        }

        // 新增：點數管理數據
        public async Task<PointsManagementViewModel> GetPointsManagementAsync()
        {
            try
            {
                var model = new PointsManagementViewModel
                {
                    // 基本統計 - 使用真實的 PointsLogs 表數據
                    TotalPointsIssued = await _financeRepository.GetTotalPointsIssuedAsync(0, 0), // 全部歷史數據
                    TotalPointsUsed = Math.Abs(await _financeRepository.GetTotalPointsUsedAsync()),
                    TotalPointsBalance = await _financeRepository.GetTotalPointsBalanceAsync(),
                    TodayPointsChange = await _financeRepository.GetTodayPointsChangeAsync()
                };

                // 修正：加入 null 防護 - 會員點數排行榜
                var topHolders = (await _financeRepository.GetTopPointsHoldersAsync(5))
                    ?? new List<(string MemberName, string Email, int Points, string Level)>();
                var levelColors = new Dictionary<string, string>
                {
                    { "VIP", "#f6c23e" },
                    { "金卡", "#4e73df" },
                    { "銀卡", "#6c757d" },
                    { "銅卡", "#36b9cc" },
                    { "一般", "#36b9cc" }
                };
                model.TopPointsHolders = topHolders.Select(th => new TopPointsHolderData
                {
                    MemberName = th.MemberName,
                    Email = th.Email,
                    Points = th.Points,
                    Level = th.Level,
                    LevelColor = levelColors.ContainsKey(th.Level) ? levelColors[th.Level] : "#6c757d"
                }).ToList();

                // 修正：加入 null 防護 - 最近點數記錄
                var recentLogs = (await _financeRepository.GetRecentPointsLogsAsync(20))
                    ?? new List<(DateTime Date, string MemberEmail, string Type, int Amount, int Balance, string Description)>();
                var typeColors = new Dictionary<string, string>
                {
                    { "獲得", "#1cc88a" },
                    { "使用", "#e74a3b" }
                };
                model.RecentPointsLogs = recentLogs.Select(rl => new RecentPointsLogData
                {
                    Date = rl.Date,
                    MemberEmail = rl.MemberEmail,
                    Type = rl.Type,
                    Amount = Math.Abs(rl.Amount),
                    Balance = rl.Balance,
                    Description = rl.Description,
                    TypeColor = typeColors.ContainsKey(rl.Type) ? typeColors[rl.Type] : "#6c757d"
                }).ToList();

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPointsManagementAsync: {ex.Message}");
                return new PointsManagementViewModel();
            }
        }
    }
}
