namespace Team.Backend.Models.ViewModels
{
    public class FinanceDashboardViewModel
    {
        // 基礎統計
        public int TotalMembers { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalPointsIssued { get; set; }
        public int TotalPointsUsed { get; set; }

        // 新增：趨勢分析
        public List<MonthlyRevenueData> MonthlyRevenues { get; set; } = new();
        public List<CategorySalesData> CategorySales { get; set; } = new();
        public List<PointsFlowData> PointsFlow { get; set; } = new();

        // 新增：關鍵指標
        public decimal AverageOrderValue { get; set; }
        public int ActiveMembersThisMonth { get; set; }
        public decimal GrowthRate { get; set; }
        public int NewMembersThisMonth { get; set; }
        public decimal RefundAmount { get; set; }
        public int CouponUsage { get; set; }
        public decimal CouponDiscount { get; set; }

        // 新增：營運效率
        public decimal ProfitMargin { get; set; }
        public int TopSellingProducts { get; set; }
        public decimal MemberRetentionRate { get; set; }
    }

    // 新增：營收分析 ViewModel
    public class RevenueAnalysisViewModel
    {
        // 基本營收數據
        public decimal TodayRevenue { get; set; }
        public decimal YesterdayRevenue { get; set; }
        public decimal WeekRevenue { get; set; }
        public decimal LastWeekRevenue { get; set; }
        public decimal MonthRevenue { get; set; }
        public decimal LastMonthRevenue { get; set; }
        public decimal YearRevenue { get; set; }
        public decimal LastYearRevenue { get; set; }

        // 成長率計算
        public decimal DailyGrowthRate => YesterdayRevenue == 0 ? (TodayRevenue > 0 ? 100 : 0) : 
            ((TodayRevenue - YesterdayRevenue) / YesterdayRevenue) * 100;
        
        public decimal WeeklyGrowthRate => LastWeekRevenue == 0 ? (WeekRevenue > 0 ? 100 : 0) : 
            ((WeekRevenue - LastWeekRevenue) / LastWeekRevenue) * 100;
        
        public decimal MonthlyGrowthRate => LastMonthRevenue == 0 ? (MonthRevenue > 0 ? 100 : 0) : 
            ((MonthRevenue - LastMonthRevenue) / LastMonthRevenue) * 100;
        
        public decimal YearlyGrowthRate => LastYearRevenue == 0 ? (YearRevenue > 0 ? 100 : 0) : 
            ((YearRevenue - LastYearRevenue) / LastYearRevenue) * 100;

        // 圖表數據
        public List<CategorySalesData> CategorySales { get; set; } = new();
        public List<MonthlyRevenueData> DailyRevenues { get; set; } = new();
    }

    // 新增：訂單統計 ViewModel
    public class OrderStatisticsViewModel
    {
        // 基本統計
        public int TodayOrdersCount { get; set; }
        public int PendingOrdersCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal OrderCompletionRate { get; set; }

        // 趨勢數據
        public List<DailyOrderTrendData> DailyOrderTrend { get; set; } = new();
        public List<OrderStatusData> OrderStatusDistribution { get; set; } = new();
        public List<OrderTimeData> OrderTimeDistribution { get; set; } = new();
        
        // 退貨分析
        public decimal ReturnRate { get; set; }
        
        // 統計摘要
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int CompletedOrders { get; set; }
        public int CanceledOrders { get; set; }
    }

    // 新增：點數管理 ViewModel
    public class PointsManagementViewModel
    {
        // 基本統計 - 真實的 PointsLogs 數據
        public int TotalPointsIssued { get; set; }
        public int TotalPointsUsed { get; set; }
        public int TotalPointsBalance { get; set; }
        public int TodayPointsChange { get; set; }
        
        // 真實數據 - 會員排行榜
        public List<TopPointsHolderData> TopPointsHolders { get; set; } = new();
        
        // 真實數據 - 最近記錄
        public List<RecentPointsLogData> RecentPointsLogs { get; set; } = new();
    }

    // 輔助數據類別
    public class DailyOrderTrendData
    {
        public string Date { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public int CompletedCount { get; set; }
    }

    public class OrderStatusData
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class OrderTimeData
    {
        public string Hour { get; set; } = string.Empty;
        public int OrderCount { get; set; }
    }

    public class TopPointsHolderData
    {
        public string MemberName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Points { get; set; }
        public string Level { get; set; } = string.Empty;
        public string LevelColor { get; set; } = string.Empty;
    }

    public class RecentPointsLogData
    {
        public DateTime Date { get; set; }
        public string MemberEmail { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Amount { get; set; }
        public int Balance { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TypeColor { get; set; } = string.Empty;
    }

    // 月營收數據
    public class MonthlyRevenueData
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    // 分類銷售數據
    public class CategorySalesData
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public int ProductCount { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    // 點數流動數據
    public class PointsFlowData
    {
        public string Date { get; set; } = string.Empty;
        public int Earned { get; set; }
        public int Used { get; set; }
        public int Balance { get; set; }
    }
}
