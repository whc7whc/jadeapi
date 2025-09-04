using System.ComponentModel.DataAnnotations;

namespace Team.API.Models.DTOs
{
    /// <summary>
    /// ��a����d�� DTO
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
    /// ��a�q��d�� DTO
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
    /// ��a����O�^�� DTO
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
    /// ������� DTO
    /// </summary>
    public class ReportPeriodDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>
    /// ��a�K�n�έp DTO
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
    /// ��a�����v DTO
    /// </summary>
    public class SellerGrowthRatesDto
    {
        public decimal RevenueGrowth { get; set; }
        public decimal OrderGrowth { get; set; }
        public decimal AvgOrderValueGrowth { get; set; }
    }

    /// <summary>
    /// ��a�q��^�� DTO
    /// </summary>
    public class SellerOrderResponseDto
    {
        public List<SellerOrderDto> Orders { get; set; } = new();
        public PaginationDto Pagination { get; set; } = new();
    }

    /// <summary>
    /// ��a�q�� DTO
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
    /// ��a�q��ӫ~ DTO
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
    /// ������T DTO
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
    /// ��a�έp�^�� DTO
    /// </summary>
    public class SellerStatisticsResponseDto
    {
        public string ReportType { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public SellerChartsDto Charts { get; set; } = new();
    }

    /// <summary>
    /// ��a�Ϫ��� DTO
    /// </summary>
    public class SellerChartsDto
    {
        public List<DailySalesDto> DailySales { get; set; } = new();
        public List<ProductPerformanceDto> ProductPerformance { get; set; } = new();
        public List<OrderStatusDto> OrderStatus { get; set; } = new();
    }

    /// <summary>
    /// �C��P�� DTO
    /// </summary>
    public class DailySalesDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    /// <summary>
    /// �ӫ~�Z�� DTO
    /// </summary>
    public class ProductPerformanceDto
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public int Quantity { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>
    /// �q�檬�A�έp DTO
    /// </summary>
    public class OrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// ��a�ӫ~���R�^�� DTO
    /// </summary>
    public class SellerProductAnalysisResponseDto
    {
        public List<SellerProductAnalysisDto> Products { get; set; } = new();
    }

    /// <summary>
    /// ��a�ӫ~���R DTO
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