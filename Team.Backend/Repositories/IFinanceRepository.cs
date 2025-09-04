namespace Team.Backend.Repositories
{
    public interface IFinanceRepository
    {
        // 基本統計
        Task<int> GetTotalMembersAsync();
        Task<decimal> GetTotalRevenueAsync(int year, int month);
        Task<int> GetTotalPointsIssuedAsync(int year, int month);
        Task<int> GetTotalPointsUsedAsync();
        Task<int> GetMonthlyPointsIssuedAsync(int year, int month);

        // 關鍵指標
        Task<decimal> GetAverageOrderValueAsync(int year, int month);
        Task<int> GetActiveMembersThisMonthAsync(int year, int month);
        Task<decimal> GetGrowthRateAsync(int year, int month);
        Task<int> GetNewMembersThisMonthAsync(int year, int month);
        Task<decimal> GetRefundAmountAsync(int year, int month);
        Task<int> GetCouponUsageAsync(int year, int month);
        Task<decimal> GetCouponDiscountAsync(int year, int month);

        // 趨勢分析
        Task<List<(string Month, decimal Revenue, int OrderCount)>> GetMonthlyRevenuesAsync(int year);
        Task<List<(string CategoryName, decimal Sales, int ProductCount)>> GetCategorySalesAsync(int year, int month);

        // 營運效率
        Task<decimal> GetProfitMarginAsync(int year, int month);
        Task<int> GetTopSellingProductsCountAsync(int year, int month);
        Task<decimal> GetMemberRetentionRateAsync(int year, int month);

        // 即時營收方法
        Task<decimal> GetTodayRevenueAsync();
        Task<decimal> GetWeekRevenueAsync();
        Task<decimal> GetYearRevenueAsync(int year);
        Task<decimal> GetYesterdayRevenueAsync();
        Task<decimal> GetLastWeekRevenueAsync();

        // 訂單統計方法
        Task<int> GetTodayOrdersCountAsync();
        Task<int> GetPendingOrdersCountAsync();
        Task<decimal> GetOrderCompletionRateAsync(int year, int month);
        Task<List<(string Date, int OrderCount, int CompletedCount)>> GetDailyOrderTrendAsync(int days = 7);
        Task<List<(string Status, int Count, decimal Percentage)>> GetOrderStatusDistributionAsync(int year, int month);
        Task<List<(string Hour, int OrderCount)>> GetOrderTimeDistributionAsync(int year, int month);
        Task<decimal> GetReturnRateAsync(int year, int month);

        // 點數管理方法 - 只保留真實數據相關的方法
        Task<int> GetTotalPointsBalanceAsync();
        Task<int> GetTodayPointsChangeAsync();
        Task<List<(string MemberName, string Email, int Points, string Level)>> GetTopPointsHoldersAsync(int top = 10);
        Task<List<(DateTime Date, string MemberEmail, string Type, int Amount, int Balance, string Description)>> GetRecentPointsLogsAsync(int count = 20);

        // 報表數據方法
        Task<object> GetRevenueReportDataAsync(DateTime startDate, DateTime endDate);
        Task<object> GetOrderReportDataAsync(DateTime startDate, DateTime endDate);
        Task<object> GetMemberReportDataAsync(DateTime startDate, DateTime endDate);
        Task<object> GetPointsReportDataAsync(DateTime startDate, DateTime endDate);
        Task<object> GetComprehensiveReportDataAsync(DateTime startDate, DateTime endDate);
    }
}
