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
        /// 獲取賣家儀表板統計資料
        /// </summary>
        /// <param name="sellerId">賣家ID</param>
        /// <param name="startDate">開始日期</param>
        /// <param name="endDate">結束日期</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/dashboard")]
        public async Task<ActionResult<ApiResponse<SellerDashboardResponseDto>>> GetSellerDashboard(
            int sellerId, 
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("獲取賣家 {SellerId} 的儀表板資料", sellerId);

                // 設定預設日期範圍（當月）
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var defaultEndDate = endDate ?? defaultStartDate.AddMonths(1).AddDays(-1);

                // 驗證賣家是否存在
                var seller = await _context.Sellers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sellerId && s.IsActive);

                if (seller == null)
                {
                    return NotFound(ApiResponse<SellerDashboardResponseDto>.ErrorResult("找不到該賣家或賣家未啟用"));
                }

                // 獲取當期訂單資料
                var currentOrders = await GetSellerOrdersInPeriod(sellerId, defaultStartDate, defaultEndDate);
                
                // 獲取上期訂單資料（用於計算成長率）
                var previousStartDate = defaultStartDate.AddMonths(-1);
                var previousEndDate = defaultStartDate.AddDays(-1);
                var previousOrders = await GetSellerOrdersInPeriod(sellerId, previousStartDate, previousEndDate);

                // 計算當期統計
                var currentStats = CalculateOrderStats(currentOrders);
                
                // 計算上期統計
                var previousStats = CalculateOrderStats(previousOrders);

                // 計算成長率
                var growthRates = CalculateGrowthRates(currentStats, previousStats);

                // 獲取商品總數
                var totalProducts = await _context.Products
                    .Where(p => p.SellersId == sellerId)
                    .CountAsync();

                var dashboard = new SellerDashboardResponseDto
                {
                    SellerId = sellerId,
                    SellerName = seller.RealName ?? "賣家",
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

                _logger.LogInformation("成功獲取賣家 {SellerId} 儀表板資料，營收：{Revenue}，訂單數：{Orders}", 
                    sellerId, currentStats.TotalRevenue, currentStats.TotalOrders);

                return Ok(ApiResponse<SellerDashboardResponseDto>.SuccessResult(dashboard, "獲取儀表板資料成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家 {SellerId} 儀表板資料失敗", sellerId);
                return StatusCode(500, ApiResponse<SellerDashboardResponseDto>.ErrorResult("獲取儀表板資料失敗：" + ex.Message));
            }
        }

        /// <summary>
        /// 獲取賣家訂單列表
        /// </summary>
        /// <param name="sellerId">賣家ID</param>
        /// <param name="query">查詢參數</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/orders")]
        public async Task<ActionResult<ApiResponse<SellerOrderResponseDto>>> GetSellerOrders(
            int sellerId, 
            [FromQuery] SellerOrderQueryDto query)
        {
            try
            {
                _logger.LogInformation("獲取賣家 {SellerId} 的訂單列表", sellerId);

                // 設定預設日期範圍
                var startDate = query.StartDate ?? DateTime.Today.AddDays(-30);
                var endDate = query.EndDate ?? DateTime.Today.AddDays(1);

                // 建立查詢
                var ordersQuery = _context.Orders
                    .Where(o => o.SellersId == sellerId &&
                               o.CreatedAt >= startDate &&
                               o.CreatedAt < endDate)
                    .Include(o => o.Member)
                    .ThenInclude(m => m.MemberProfile)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .AsNoTracking();

                // 狀態篩選
                if (!string.IsNullOrEmpty(query.Status) && query.Status != "all")
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderStatus == query.Status);
                }

                // 排序
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

                // 總數
                var totalCount = await ordersQuery.CountAsync();

                // 分頁
                var orders = await ordersQuery
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToListAsync();

                // 轉換為 DTO
                var orderDtos = orders.Select(order => new SellerOrderDto
                {
                    Id = order.Id,
                    OrderNumber = $"ORD{order.Id:D8}",
                    OrderDate = order.CreatedAt,
                    CustomerName = order.Member?.MemberProfile?.Name ?? "訪客",
                    CustomerEmail = order.Member?.Email ?? "無Email",
                    Status = order.OrderStatus ?? "unknown",
                    StatusLabel = GetStatusLabel(order.OrderStatus),
                    TotalAmount = order.TotalAmount,
                    ItemCount = order.OrderDetails?.Count ?? 0,
                    Products = order.OrderDetails?.Select(od => new SellerOrderProductDto
                    {
                        ProductName = od.Product?.Name ?? "未知商品",
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

                _logger.LogInformation("成功獲取賣家 {SellerId} 訂單列表，共 {Count} 筆", sellerId, totalCount);

                return Ok(ApiResponse<SellerOrderResponseDto>.SuccessResult(response, "獲取訂單列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家 {SellerId} 訂單列表失敗", sellerId);
                return StatusCode(500, ApiResponse<SellerOrderResponseDto>.ErrorResult("獲取訂單列表失敗：" + ex.Message));
            }
        }

        /// <summary>
        /// 獲取賣家統計資料
        /// </summary>
        /// <param name="sellerId">賣家ID</param>
        /// <param name="reportType">報表類型</param>
        /// <param name="year">年份</param>
        /// <param name="month">月份</param>
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
                _logger.LogInformation("獲取賣家 {SellerId} 統計資料，類型：{ReportType}", sellerId, reportType);

                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;

                DateTime startDate, endDate;
                string period;

                // 根據報表類型設定日期範圍
                switch (reportType.ToLower())
                {
                    case "daily":
                        startDate = DateTime.Today.AddDays(-6);
                        endDate = DateTime.Today.AddDays(1);
                        period = $"{DateTime.Today:yyyy年M月d日} 前7天";
                        break;
                    case "weekly":
                        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        startDate = startOfWeek.AddDays(-21); // 過去3週
                        endDate = startOfWeek.AddDays(7);
                        period = "過去4週";
                        break;
                    case "yearly":
                        startDate = new DateTime(selectedYear, 1, 1);
                        endDate = new DateTime(selectedYear + 1, 1, 1);
                        period = $"{selectedYear}年";
                        break;
                    default: // monthly
                        startDate = new DateTime(selectedYear, selectedMonth, 1);
                        endDate = startDate.AddMonths(1);
                        period = $"{selectedYear}年{selectedMonth}月";
                        break;
                }

                // 獲取期間內的訂單
                var orders = await _context.Orders
                    .Where(o => o.SellersId == sellerId &&
                               o.CreatedAt >= startDate &&
                               o.CreatedAt < endDate)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .AsNoTracking()
                    .ToListAsync();

                // 計算每日銷售
                var dailySales = CalculateDailySales(orders, startDate, endDate, reportType);

                // 計算商品績效
                var productPerformance = CalculateProductPerformance(orders);

                // 計算訂單狀態分布
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

                _logger.LogInformation("成功獲取賣家 {SellerId} 統計資料", sellerId);

                return Ok(ApiResponse<SellerStatisticsResponseDto>.SuccessResult(statistics, "獲取統計資料成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家 {SellerId} 統計資料失敗", sellerId);
                return StatusCode(500, ApiResponse<SellerStatisticsResponseDto>.ErrorResult("獲取統計資料失敗：" + ex.Message));
            }
        }

        /// <summary>
        /// 獲取賣家商品分析
        /// </summary>
        /// <param name="sellerId">賣家ID</param>
        /// <param name="startDate">開始日期</param>
        /// <param name="endDate">結束日期</param>
        /// <param name="sortBy">排序欄位</param>
        /// <param name="sortDirection">排序方向</param>
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
                _logger.LogInformation("獲取賣家 {SellerId} 商品分析", sellerId);

                // 設定預設日期範圍
                var defaultStartDate = startDate ?? DateTime.Today.AddDays(-30);
                var defaultEndDate = endDate ?? DateTime.Today.AddDays(1);

                // 獲取賣家的商品銷售資料
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

                // 轉換為 DTO 並排序
                var productAnalysis = productSales.Select(ps => new SellerProductAnalysisDto
                {
                    ProductId = ps.ProductId,
                    ProductName = ps.Product?.Name ?? "未知商品",
                    TotalSales = ps.TotalSales,
                    TotalQuantity = ps.TotalQuantity,
                    AveragePrice = ps.TotalQuantity > 0 ? ps.TotalSales / ps.TotalQuantity : 0,
                    OrderCount = ps.OrderCount,
                    ConversionRate = 0, // 需要更多資料計算轉換率
                    StockLevel = 0, // 需要庫存資料
                    LastOrderDate = ps.LastOrderDate
                }).ToList();

                // 排序
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

                _logger.LogInformation("成功獲取賣家 {SellerId} 商品分析，共 {Count} 個商品", sellerId, productAnalysis.Count);

                return Ok(ApiResponse<SellerProductAnalysisResponseDto>.SuccessResult(response, "獲取商品分析成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家 {SellerId} 商品分析失敗", sellerId);
                return StatusCode(500, ApiResponse<SellerProductAnalysisResponseDto>.ErrorResult("獲取商品分析失敗：" + ex.Message));
            }
        }

        #region 私有輔助方法

        /// <summary>
        /// 獲取特定期間的賣家訂單
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
        /// 計算訂單統計
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
        /// 計算成長率
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
        /// 計算每日銷售
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
                // 按天分組
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
        /// 計算商品績效
        /// </summary>
        private List<ProductPerformanceDto> CalculateProductPerformance(List<Order> orders)
        {
            // 這裡可以根據 OrderDetails 計算商品績效
            // 暫時返回空列表，可以後續完善
            return new List<ProductPerformanceDto>();
        }

        /// <summary>
        /// 計算訂單狀態分布
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
        /// 獲取狀態標籤
        /// </summary>
        private string GetStatusLabel(string? status)
        {
            return status switch
            {
                "completed" => "已完成",
                "pending" => "待處理",
                "processing" => "處理中",
                "cancelled" => "已取消",
                "shipped" => "已出貨",
                "delivered" => "已送達",
                _ => "未知"
            };
        }

        #endregion
    }
}