using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SellerReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SellerReportsController> _logger;

        public SellerReportsController(AppDbContext context, ILogger<SellerReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get seller dashboard statistics
        /// </summary>
        /// <param name="sellerId">Seller ID</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/dashboard")]
        public async Task<ActionResult<ApiResponse<SellerDashboardResponseDto>>> GetSellerDashboard(
            int sellerId, 
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Getting dashboard data for seller {SellerId}", sellerId);

                // Set default date range (current month)
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var defaultEndDate = endDate ?? defaultStartDate.AddMonths(1).AddDays(-1);

                // Verify seller exists
                var seller = await _context.Sellers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sellerId && s.IsActive);

                if (seller == null)
                {
                    return NotFound(ApiResponse<SellerDashboardResponseDto>.ErrorResult("Seller not found or inactive"));
                }

                // Get current period order data
                var currentOrders = await GetSellerOrdersInPeriod(sellerId, defaultStartDate, defaultEndDate);
                
                // Get previous period order data (for calculating growth rates)
                var previousStartDate = defaultStartDate.AddMonths(-1);
                var previousEndDate = defaultStartDate.AddDays(-1);
                var previousOrders = await GetSellerOrdersInPeriod(sellerId, previousStartDate, previousEndDate);

                // Calculate current period statistics
                var currentStats = CalculateOrderStats(currentOrders);
                
                // Calculate previous period statistics
                var previousStats = CalculateOrderStats(previousOrders);

                // Calculate growth rates
                var growthRates = CalculateGrowthRates(currentStats, previousStats);

                // Get total product count
                var totalProducts = await _context.Products
                    .Where(p => p.SellersId == sellerId)
                    .CountAsync();

                var dashboard = new SellerDashboardResponseDto
                {
                    SellerId = sellerId,
                    SellerName = seller.RealName ?? "Seller",
                    ReportPeriod = new ReportPeriodDto
                    {
                        StartDate = defaultStartDate,
                        EndDate = defaultEndDate
                    },
                    Summary = new SellerSummaryDto
                    {
                        TotalRevenue = currentStats.TotalRevenue,
                        TotalOrders = currentStats.TotalOrders,
                        TotalProducts = totalProducts,
                        AverageOrderValue = currentStats.AverageOrderValue,
                        CompletionRate = currentStats.CompletionRate
                    },
                    GrowthRates = growthRates
                };

                _logger.LogInformation("Successfully retrieved dashboard data for seller {SellerId}, revenue: {Revenue}, orders: {Orders}", 
                    sellerId, currentStats.TotalRevenue, currentStats.TotalOrders);

                return Ok(ApiResponse<SellerDashboardResponseDto>.SuccessResult(dashboard, "Dashboard data retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get dashboard data for seller {SellerId}", sellerId);
                return StatusCode(500, ApiResponse<SellerDashboardResponseDto>.ErrorResult("Failed to get dashboard data: " + ex.Message));
            }
        }

        /// <summary>
        /// Get seller orders list
        /// </summary>
        /// <param name="sellerId">Seller ID</param>
        /// <param name="query">Query parameters</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/orders")]
        public async Task<ActionResult<ApiResponse<SellerOrderResponseDto>>> GetSellerOrders(
            int sellerId, 
            [FromQuery] SellerOrderQueryDto query)
        {
            try
            {
                _logger.LogInformation("Getting order list for seller {SellerId}", sellerId);

                // Set default date range
                var startDate = query.StartDate ?? DateTime.Today.AddDays(-30);
                var endDate = query.EndDate ?? DateTime.Today.AddDays(1);

                // Build query
                var ordersQuery = _context.Orders
                    .Where(o => o.SellersId == sellerId &&
                               o.CreatedAt >= startDate &&
                               o.CreatedAt < endDate)
                    .Include(o => o.Member)
                    .ThenInclude(m => m.MemberProfile)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .AsNoTracking();

                // Status filter
                if (!string.IsNullOrEmpty(query.Status) && query.Status != "all")
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderStatus == query.Status);
                }

                // Sorting
                ordersQuery = query.SortBy.ToLower() switch
                {
                    "totalamount" => query.SortDirection.ToLower() == "desc" 
                        ? ordersQuery.OrderByDescending(o => o.TotalAmount) 
                        : ordersQuery.OrderBy(o => o.TotalAmount),
                    "status" => query.SortDirection.ToLower() == "desc" 
                        ? ordersQuery.OrderByDescending(o => o.OrderStatus) 
                        : ordersQuery.OrderBy(o => o.OrderStatus),
                    _ => query.SortDirection.ToLower() == "desc" 
                        ? ordersQuery.OrderByDescending(o => o.CreatedAt) 
                        : ordersQuery.OrderBy(o => o.CreatedAt)
                };

                // Total count
                var totalCount = await ordersQuery.CountAsync();

                // Pagination
                var orders = await ordersQuery
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToListAsync();

                // Convert to DTO
                var orderDtos = orders.Select(order => new SellerOrderDto
                {
                    Id = order.Id,
                    OrderNumber = $"ORD{order.Id:D8}",
                    OrderDate = order.CreatedAt,
                    CustomerName = order.Member?.MemberProfile?.Name ?? "Guest",
                    CustomerEmail = order.Member?.Email ?? "No Email",
                    Status = order.OrderStatus ?? "unknown",
                    StatusLabel = GetStatusLabel(order.OrderStatus),
                    TotalAmount = order.TotalAmount,
                    ItemCount = order.OrderDetails?.Count ?? 0,
                    Products = order.OrderDetails?.Select(od => new SellerOrderProductDto
                    {
                        ProductName = od.Product?.Name ?? "Unknown Product",
                        Quantity = od.Quantity ?? 0,
                        UnitPrice = od.UnitPrice ?? 0,
                        Subtotal = (od.UnitPrice ?? 0) * (od.Quantity ?? 0)
                    }).ToList() ?? new List<SellerOrderProductDto>()
                }).ToList();

                var response = new SellerOrderResponseDto
                {
                    Orders = orderDtos,
                    Pagination = new PaginationDto
                    {
                        CurrentPage = query.Page,
                        TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize),
                        TotalCount = totalCount,
                        PageSize = query.PageSize
                    }
                };

                _logger.LogInformation("Successfully retrieved order list for seller {SellerId}, total: {Count}", sellerId, totalCount);

                return Ok(ApiResponse<SellerOrderResponseDto>.SuccessResult(response, "Order list retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order list for seller {SellerId}", sellerId);
                return StatusCode(500, ApiResponse<SellerOrderResponseDto>.ErrorResult("Failed to get order list: " + ex.Message));
            }
        }

        /// <summary>
        /// Get seller statistics
        /// </summary>
        /// <param name="sellerId">Seller ID</param>
        /// <param name="reportType">Report type</param>
        /// <param name="year">Year</param>
        /// <param name="month">Month</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/statistics")]
        public async Task<ActionResult<ApiResponse<SellerStatisticsResponseDto>>> GetSellerStatistics(
            int sellerId,
            [FromQuery] string reportType = "monthly",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null)
        {
            try
            {
                _logger.LogInformation("Getting statistics for seller {SellerId}, type: {ReportType}", sellerId, reportType);

                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;

                DateTime startDate, endDate;
                string period;

                // Set date range based on report type
                switch (reportType.ToLower())
                {
                    case "daily":
                        startDate = DateTime.Today.AddDays(-6);
                        endDate = DateTime.Today.AddDays(1);
                        period = $"{DateTime.Today:yyyy-MM-dd} Last 7 days";
                        break;
                    case "weekly":
                        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        startDate = startOfWeek.AddDays(-21); // Past 3 weeks
                        endDate = startOfWeek.AddDays(7);
                        period = "Past 4 weeks";
                        break;
                    case "yearly":
                        startDate = new DateTime(selectedYear, 1, 1);
                        endDate = new DateTime(selectedYear + 1, 1, 1);
                        period = $"Year {selectedYear}";
                        break;
                    default: // monthly
                        startDate = new DateTime(selectedYear, selectedMonth, 1);
                        endDate = startDate.AddMonths(1);
                        period = $"{selectedYear}-{selectedMonth:D2}";
                        break;
                }

                // Get orders in period
                var orders = await _context.Orders
                    .Where(o => o.SellersId == sellerId &&
                               o.CreatedAt >= startDate &&
                               o.CreatedAt < endDate)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .AsNoTracking()
                    .ToListAsync();

                // Calculate daily sales
                var dailySales = CalculateDailySales(orders, startDate, endDate, reportType);

                // Calculate product performance
                var productPerformance = CalculateProductPerformance(orders);

                // Calculate order status distribution
                var orderStatus = CalculateOrderStatusDistribution(orders);

                var statistics = new SellerStatisticsResponseDto
                {
                    ReportType = reportType,
                    Period = period,
                    Charts = new SellerChartsDto
                    {
                        DailySales = dailySales,
                        ProductPerformance = productPerformance,
                        OrderStatus = orderStatus
                    }
                };

                _logger.LogInformation("Successfully retrieved statistics for seller {SellerId}", sellerId);

                return Ok(ApiResponse<SellerStatisticsResponseDto>.SuccessResult(statistics, "Statistics retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get statistics for seller {SellerId}", sellerId);
                return StatusCode(500, ApiResponse<SellerStatisticsResponseDto>.ErrorResult("Failed to get statistics: " + ex.Message));
            }
        }

        /// <summary>
        /// Get seller product analysis
        /// </summary>
        /// <param name="sellerId">Seller ID</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <param name="sortBy">Sort field</param>
        /// <param name="sortDirection">Sort direction</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/products")]
        public async Task<ActionResult<ApiResponse<SellerProductAnalysisResponseDto>>> GetSellerProducts(
            int sellerId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string sortBy = "sales",
            [FromQuery] string sortDirection = "desc")
        {
            try
            {
                _logger.LogInformation("Getting product analysis for seller {SellerId}", sellerId);

                // Set default date range
                var defaultStartDate = startDate ?? DateTime.Today.AddDays(-30);
                var defaultEndDate = endDate ?? DateTime.Today.AddDays(1);

                // Get seller's product sales data
                var productSales = await _context.OrderDetails
                    .Where(od => od.Order.SellersId == sellerId &&
                                od.Order.CreatedAt >= defaultStartDate &&
                                od.Order.CreatedAt < defaultEndDate &&
                                od.Order.OrderStatus == "completed")
                    .Include(od => od.Product)
                    .Include(od => od.Order)
                    .GroupBy(od => od.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Product = g.First().Product,
                        TotalSales = g.Sum(od => (od.UnitPrice ?? 0) * (od.Quantity ?? 0)),
                        TotalQuantity = g.Sum(od => od.Quantity ?? 0),
                        OrderCount = g.Select(od => od.OrderId).Distinct().Count(),
                        LastOrderDate = g.Max(od => od.Order.CreatedAt)
                    })
                    .ToListAsync();

                // Convert to DTO and sort
                var productAnalysis = productSales.Select(ps => new SellerProductAnalysisDto
                {
                    ProductId = ps.ProductId,
                    ProductName = ps.Product?.Name ?? "Unknown Product",
                    TotalSales = ps.TotalSales,
                    TotalQuantity = ps.TotalQuantity,
                    AveragePrice = ps.TotalQuantity > 0 ? ps.TotalSales / ps.TotalQuantity : 0,
                    OrderCount = ps.OrderCount,
                    ConversionRate = 0, // Requires more data to calculate conversion rate
                    StockLevel = 0, // Requires inventory data
                    LastOrderDate = ps.LastOrderDate
                }).ToList();

                // Sorting
                productAnalysis = sortBy.ToLower() switch
                {
                    "quantity" => sortDirection.ToLower() == "desc"
                        ? productAnalysis.OrderByDescending(p => p.TotalQuantity).ToList()
                        : productAnalysis.OrderBy(p => p.TotalQuantity).ToList(),
                    "orders" => sortDirection.ToLower() == "desc"
                        ? productAnalysis.OrderByDescending(p => p.OrderCount).ToList()
                        : productAnalysis.OrderBy(p => p.OrderCount).ToList(),
                    "price" => sortDirection.ToLower() == "desc"
                        ? productAnalysis.OrderByDescending(p => p.AveragePrice).ToList()
                        : productAnalysis.OrderBy(p => p.AveragePrice).ToList(),
                    _ => sortDirection.ToLower() == "desc"
                        ? productAnalysis.OrderByDescending(p => p.TotalSales).ToList()
                        : productAnalysis.OrderBy(p => p.TotalSales).ToList()
                };

                var response = new SellerProductAnalysisResponseDto
                {
                    Products = productAnalysis
                };

                _logger.LogInformation("Successfully retrieved product analysis for seller {SellerId}, total: {Count} products", sellerId, productAnalysis.Count);

                return Ok(ApiResponse<SellerProductAnalysisResponseDto>.SuccessResult(response, "Product analysis retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get product analysis for seller {SellerId}", sellerId);
                return StatusCode(500, ApiResponse<SellerProductAnalysisResponseDto>.ErrorResult("Failed to get product analysis: " + ex.Message));
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Get seller orders in specific period
        /// </summary>
        private async Task<List<Order>> GetSellerOrdersInPeriod(int sellerId, DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .Where(o => o.SellersId == sellerId &&
                           o.CreatedAt >= startDate &&
                           o.CreatedAt < endDate)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Calculate order statistics
        /// </summary>
        private (decimal TotalRevenue, int TotalOrders, decimal AverageOrderValue, decimal CompletionRate) CalculateOrderStats(List<Order> orders)
        {
            var totalOrders = orders.Count;
            var completedOrders = orders.Count(o => o.OrderStatus == "completed");
            var totalRevenue = orders.Where(o => o.OrderStatus == "completed").Sum(o => o.TotalAmount);
            var averageOrderValue = completedOrders > 0 ? totalRevenue / completedOrders : 0;
            var completionRate = totalOrders > 0 ? (decimal)completedOrders / totalOrders * 100 : 0;

            return (totalRevenue, totalOrders, averageOrderValue, completionRate);
        }

        /// <summary>
        /// Calculate growth rates
        /// </summary>
        private SellerGrowthRatesDto CalculateGrowthRates(
            (decimal TotalRevenue, int TotalOrders, decimal AverageOrderValue, decimal CompletionRate) current,
            (decimal TotalRevenue, int TotalOrders, decimal AverageOrderValue, decimal CompletionRate) previous)
        {
            return new SellerGrowthRatesDto
            {
                RevenueGrowth = previous.TotalRevenue == 0 ? (current.TotalRevenue > 0 ? 100 : 0) :
                    (current.TotalRevenue - previous.TotalRevenue) / previous.TotalRevenue * 100,
                OrderGrowth = previous.TotalOrders == 0 ? (current.TotalOrders > 0 ? 100 : 0) :
                    (decimal)(current.TotalOrders - previous.TotalOrders) / previous.TotalOrders * 100,
                AvgOrderValueGrowth = previous.AverageOrderValue == 0 ? (current.AverageOrderValue > 0 ? 100 : 0) :
                    (current.AverageOrderValue - previous.AverageOrderValue) / previous.AverageOrderValue * 100
            };
        }

        /// <summary>
        /// Calculate daily sales
        /// </summary>
        private List<DailySalesDto> CalculateDailySales(List<Order> orders, DateTime startDate, DateTime endDate, string reportType)
        {
            var dailySales = new List<DailySalesDto>();
            var completedOrders = orders.Where(o => o.OrderStatus == "completed").ToList();

            if (reportType.ToLower() == "daily")
            {
                for (var date = startDate.Date; date < endDate.Date; date = date.AddDays(1))
                {
                    var dayOrders = completedOrders.Where(o => o.CreatedAt.Date == date).ToList();
                    dailySales.Add(new DailySalesDto
                    {
                        Date = date.ToString("M/d"),
                        Revenue = dayOrders.Sum(o => o.TotalAmount),
                        Orders = dayOrders.Count
                    });
                }
            }
            else
            {
                // Group by day
                var groupedOrders = completedOrders
                    .GroupBy(o => o.CreatedAt.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var group in groupedOrders)
                {
                    dailySales.Add(new DailySalesDto
                    {
                        Date = group.Key.ToString("M/d"),
                        Revenue = group.Sum(o => o.TotalAmount),
                        Orders = group.Count()
                    });
                }
            }

            return dailySales;
        }

        /// <summary>
        /// Calculate product performance
        /// </summary>
        private List<ProductPerformanceDto> CalculateProductPerformance(List<Order> orders)
        {
            // This can be calculated based on OrderDetails
            // Temporarily return empty list, can be improved later
            return new List<ProductPerformanceDto>();
        }

        /// <summary>
        /// Calculate order status distribution
        /// </summary>
        private List<OrderStatusDto> CalculateOrderStatusDistribution(List<Order> orders)
        {
            var totalOrders = orders.Count;
            if (totalOrders == 0) return new List<OrderStatusDto>();

            var statusGroups = orders
                .GroupBy(o => o.OrderStatus ?? "unknown")
                .Select(g => new OrderStatusDto
                {
                    Status = g.Key,
                    StatusLabel = GetStatusLabel(g.Key),
                    Count = g.Count(),
                    Percentage = (decimal)g.Count() / totalOrders * 100
                })
                .OrderByDescending(s => s.Count)
                .ToList();

            return statusGroups;
        }

        /// <summary>
        /// Get status label
        /// </summary>
        private string GetStatusLabel(string? status)
        {
            return status switch
            {
                "completed" => "Completed",
                "pending" => "Pending",
                "processing" => "Processing",
                "cancelled" => "Cancelled",
                "shipped" => "Shipped",
                "delivered" => "Delivered",
                _ => "Unknown"
            };
        }

        #endregion
    }
}