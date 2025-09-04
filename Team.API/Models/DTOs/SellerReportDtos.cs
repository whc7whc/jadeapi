using System.ComponentModel.DataAnnotations;

namespace Team.API.Models.DTOs
{
    /// <summary>
    /// 賣家報表查詢 DTO
    /// </summary>
    public class SellerReportQueryDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string ReportType { get; set; } = "monthly"; // daily, weekly, monthly, yearly
        public string Status { get; set; } = "all"; // all, completed, pending, cancelled, processing
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// 賣家訂單查詢 DTO
    /// </summary>
    public class SellerOrderQueryDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "all";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "createdAt";
        public string SortDirection { get; set; } = "desc";
    }

    /// <summary>
    /// 賣家儀表板回應 DTO
    /// </summary>
    public class SellerDashboardResponseDto
    {
        public int SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public ReportPeriodDto ReportPeriod { get; set; } = new();
        public SellerSummaryDto Summary { get; set; } = new();
        public SellerGrowthRatesDto GrowthRates { get; set; } = new();
    }

    /// <summary>
    /// 報表期間 DTO
    /// </summary>
    public class ReportPeriodDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>
    /// 賣家摘要統計 DTO
    /// </summary>
    public class SellerSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProducts { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal CompletionRate { get; set; }
    }

    /// <summary>
    /// 賣家成長率 DTO
    /// </summary>
    public class SellerGrowthRatesDto
    {
        public decimal RevenueGrowth { get; set; }
        public decimal OrderGrowth { get; set; }
        public decimal AvgOrderValueGrowth { get; set; }
    }

    /// <summary>
    /// 賣家訂單回應 DTO
    /// </summary>
    public class SellerOrderResponseDto
    {
        public List<SellerOrderDto> Orders { get; set; } = new();
        public PaginationDto Pagination { get; set; } = new();
    }

    /// <summary>
    /// 賣家訂單 DTO
    /// </summary>
    public class SellerOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
        public List<SellerOrderProductDto> Products { get; set; } = new();
    }

    /// <summary>
    /// 賣家訂單商品 DTO
    /// </summary>
    public class SellerOrderProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    /// <summary>
    /// 分頁資訊 DTO
    /// </summary>
    public class PaginationDto
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    /// <summary>
    /// 賣家統計回應 DTO
    /// </summary>
    public class SellerStatisticsResponseDto
    {
        public string ReportType { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public SellerChartsDto Charts { get; set; } = new();
    }

    /// <summary>
    /// 賣家圖表資料 DTO
    /// </summary>
    public class SellerChartsDto
    {
        public List<DailySalesDto> DailySales { get; set; } = new();
        public List<ProductPerformanceDto> ProductPerformance { get; set; } = new();
        public List<OrderStatusDto> OrderStatus { get; set; } = new();
    }

    /// <summary>
    /// 每日銷售 DTO
    /// </summary>
    public class DailySalesDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    /// <summary>
    /// 商品績效 DTO
    /// </summary>
    public class ProductPerformanceDto
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public int Quantity { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>
    /// 訂單狀態統計 DTO
    /// </summary>
    public class OrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// 賣家商品分析回應 DTO
    /// </summary>
    public class SellerProductAnalysisResponseDto
    {
        public List<SellerProductAnalysisDto> Products { get; set; } = new();
    }

    /// <summary>
    /// 賣家商品分析 DTO
    /// </summary>
    public class SellerProductAnalysisDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int TotalQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public int OrderCount { get; set; }
        public decimal ConversionRate { get; set; }
        public int StockLevel { get; set; }
        public DateTime? LastOrderDate { get; set; }
    }
}