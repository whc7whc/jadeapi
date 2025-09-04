using System;
using System.Collections.Generic;

namespace Team.Backend.Models.ViewModels
{
    /// <summary>
    /// 主儀表板視圖模型，包含各種統計數據
    /// </summary>
    public class MainDashboardViewModel
    {
        #region 基本統計數據
        
        // 訂單相關
        public int TotalOrders { get; set; }
        public int NewOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        
        // 會員相關
        public int TotalMembers { get; set; }
        public int NewMembers { get; set; }
        
        // 商品相關
        public int TotalProducts { get; set; }
        public int LowStockProducts { get; set; }
        
        // 通知相關
        public int TotalNotifications { get; set; }
        
        // 文章相關
        public int TotalArticles { get; set; }
        
        // 優惠券相關
        public int TotalCoupons { get; set; }

        // 廣告總數
        public int TotalAds { get; set; }
        
        #endregion
        
        #region 過濾條件
        
        // 日期範圍
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        #endregion
        
        #region 圖表數據
        
        // 銷售圖表數據 - 一般不會直接填入，使用 AJAX 請求
        public object SalesChartData { get; set; }
        
        // 分類佔比數據 - 一般不會直接填入，使用 AJAX 請求
        public object CategoryDistributionData { get; set; }
        
        #endregion
        
        #region 列表數據
        
        // 最近訂單列表 - 一般不會直接填入，使用 AJAX 請求
        public List<object> RecentOrders { get; set; }
        
        // 熱門商品列表 - 一般不會直接填入，使用 AJAX 請求
        public List<object> PopularProducts { get; set; }
        
        #endregion
        
        public MainDashboardViewModel()
        {
            // 初始化列表
            RecentOrders = new List<object>();
            PopularProducts = new List<object>();
        }
    }
}