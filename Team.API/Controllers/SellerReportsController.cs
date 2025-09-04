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
        /// �����a����O�έp���
        /// </summary>
        /// <param name="sellerId">��aID</param>
        /// <param name="startDate">�}�l���</param>
        /// <param name="endDate">�������</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/dashboard")]
        public async Task<ActionResult<ApiResponse<SellerDashboardResponseDto>>> GetSellerDashboard(
            int sellerId, 
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("�����a {SellerId} ������O���", sellerId);

                // �]�w�w�]����d��]���^
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var defaultEndDate = endDate ?? defaultStartDate.AddMonths(1).AddDays(-1);

                // ���ҽ�a�O�_�s�b
                var seller = await _context.Sellers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sellerId && s.IsActive);

                if (seller == null)
                {
                    return NotFound(ApiResponse<SellerDashboardResponseDto>.ErrorResult("�䤣��ӽ�a�ν�a���ҥ�"));
                }

                // �������q����
                var currentOrders = await GetSellerOrdersInPeriod(sellerId, defaultStartDate, defaultEndDate);
                
                // ����W���q���ơ]�Ω�p�⦨���v�^
                var previousStartDate = defaultStartDate.AddMonths(-1);
                var previousEndDate = defaultStartDate.AddDays(-1);
                var previousOrders = await GetSellerOrdersInPeriod(sellerId, previousStartDate, previousEndDate);

                // �p�����έp
                var currentStats = CalculateOrderStats(currentOrders);
                
                // �p��W���έp
                var previousStats = CalculateOrderStats(previousOrders);

                // �p�⦨���v
                var growthRates = CalculateGrowthRates(currentStats, previousStats);

                // ����ӫ~�`��
                var totalProducts = await _context.Products
                    .Where(p => p.SellersId == sellerId)
                    .CountAsync();

                var dashboard = new SellerDashboardResponseDto
                {
                    SellerId = sellerId,
                    SellerName = seller.RealName ?? "��a",
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

                _logger.LogInformation("���\�����a {SellerId} ����O��ơA�禬�G{Revenue}�A�q��ơG{Orders}", 
                    sellerId, currentStats.TotalRevenue, currentStats.TotalOrders);

                return Ok(ApiResponse<SellerDashboardResponseDto>.SuccessResult(dashboard, "�������O��Ʀ��\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�����a {SellerId} ����O��ƥ���", sellerId);
                return StatusCode(500, ApiResponse<SellerDashboardResponseDto>.ErrorResult("�������O��ƥ��ѡG" + ex.Message));
            }
        }

        /// <summary>
        /// �����a�q��C��
        /// </summary>
        /// <param name="sellerId">��aID</param>
        /// <param name="query">�d�߰Ѽ�</param>
        /// <returns></returns>
        [HttpGet("{sellerId}/orders")]
        public async Task<ActionResult<ApiResponse<SellerOrderResponseDto>>> GetSellerOrders(
            int sellerId, 
            [FromQuery] SellerOrderQueryDto query)
        {
            try
            {
                _logger.LogInformation("�����a {SellerId} ���q��C��", sellerId);

                // �]�w�w�]����d��
                var startDate = query.StartDate ?? DateTime.Today.AddDays(-30);
                var endDate = query.EndDate ?? DateTime.Today.AddDays(1);

                // �إ߬d��
                var ordersQuery = _context.Orders
                    .Where(o => o.SellersId == sellerId &&
                               o.CreatedAt >= startDate &&
                               o.CreatedAt < endDate)
                    .Include(o => o.Member)
                    .ThenInclude(m => m.MemberProfile)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .AsNoTracking();

                // ���A�z��
                if (!string.IsNullOrEmpty(query.Status) && query.Status != "all")
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderStatus == query.Status);
                }

                // �Ƨ�
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

                // �`��
                var totalCount = await ordersQuery.CountAsync();

                // ����
                var orders = await ordersQuery
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToListAsync();

                // �ഫ�� DTO
                var orderDtos = orders.Select(order => new SellerOrderDto
                {
                    Id = order.Id,
                    OrderNumber = $"ORD{order.Id:D8}",
                    OrderDate = order.CreatedAt,
                    CustomerName = order.Member?.MemberProfile?.Name ?? "�X��",
                    CustomerEmail = order.Member?.Email ?? "�LEmail",
                    Status = order.OrderStatus ?? "unknown",
                    StatusLabel = GetStatusLabel(order.OrderStatus),
                    TotalAmount = order.TotalAmount,
                    ItemCount = order.OrderDetails?.Count ?? 0,
                    Products = order.OrderDetails?.Select(od => new SellerOrderProductDto
                    {
                        ProductName = od.Product?.Name ?? "�����ӫ~",
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

                _logger.LogInformation("���\�����a {SellerId} �q��C��A�@ {Count} ��", sellerId, totalCount);

                return Ok(ApiResponse<SellerOrderResponseDto>.SuccessResult(response, "����q��C���\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�����a {SellerId} �q��C����", sellerId);
                return StatusCode(500, ApiResponse<SellerOrderResponseDto>.ErrorResult("����q��C���ѡG" + ex.Message));
            }
        }

        /// <summary>
        /// �����a�έp���
        /// </summary>
        /// <param name="sellerId">��aID</param>
        /// <param name="reportType">��������</param>
        /// <param name="year">�~��</param>
        /// <param name="month">���</param>
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
                _logger.LogInformation("�����a {SellerId} �έp��ơA�����G{ReportType}", sellerId, reportType);

                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;

                DateTime startDate, endDate;
                string period;

                // �ھڳ��������]�w����d��
                switch (reportType.ToLower())
                {
                    case "daily":
                        startDate = DateTime.Today.AddDays(-6);
                        endDate = DateTime.Today.AddDays(1);
                        period = $"{DateTime.Today:yyyy�~M��d��} �e7��";
                        break;
                    case "weekly":
                        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        startDate = startOfWeek.AddDays(-21); // �L�h3�g
                        endDate = startOfWeek.AddDays(7);
                        period = "�L�h4�g";
                        break;
                    case "yearly":
                        startDate = new DateTime(selectedYear, 1, 1);
                        endDate = new DateTime(selectedYear + 1, 1, 1);
                        period = $"{selectedYear}�~";
                        break;
                    default: // monthly
                        startDate = new DateTime(selectedYear, selectedMonth, 1);
                        endDate = startDate.AddMonths(1);
                        period = $"{selectedYear}�~{selectedMonth}��";
                        break;
                }

                // ������������q��
                var orders = await _context.Orders
                    .Where(o => o.SellersId == sellerId &&
                               o.CreatedAt >= startDate &&
                               o.CreatedAt < endDate)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .AsNoTracking()
                    .ToListAsync();

                // �p��C��P��
                var dailySales = CalculateDailySales(orders, startDate, endDate, reportType);

                // �p��ӫ~�Z��
                var productPerformance = CalculateProductPerformance(orders);

                // �p��q�檬�A����
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

                _logger.LogInformation("���\�����a {SellerId} �έp���", sellerId);

                return Ok(ApiResponse<SellerStatisticsResponseDto>.SuccessResult(statistics, "����έp��Ʀ��\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�����a {SellerId} �έp��ƥ���", sellerId);
                return StatusCode(500, ApiResponse<SellerStatisticsResponseDto>.ErrorResult("����έp��ƥ��ѡG" + ex.Message));
            }
        }

        /// <summary>
        /// �����a�ӫ~���R
        /// </summary>
        /// <param name="sellerId">��aID</param>
        /// <param name="startDate">�}�l���</param>
        /// <param name="endDate">�������</param>
        /// <param name="sortBy">�Ƨ����</param>
        /// <param name="sortDirection">�ƧǤ�V</param>
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
                _logger.LogInformation("�����a {SellerId} �ӫ~���R", sellerId);

                // �]�w�w�]����d��
                var defaultStartDate = startDate ?? DateTime.Today.AddDays(-30);
                var defaultEndDate = endDate ?? DateTime.Today.AddDays(1);

                // �����a���ӫ~�P����
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

                // �ഫ�� DTO �ñƧ�
                var productAnalysis = productSales.Select(ps => new SellerProductAnalysisDto
                {
                    ProductId = ps.ProductId,
                    ProductName = ps.Product?.Name ?? "�����ӫ~",
                    TotalSales = ps.TotalSales,
                    TotalQuantity = ps.TotalQuantity,
                    AveragePrice = ps.TotalQuantity > 0 ? ps.TotalSales / ps.TotalQuantity : 0,
                    OrderCount = ps.OrderCount,
                    ConversionRate = 0, // �ݭn��h��ƭp���ഫ�v
                    StockLevel = 0, // �ݭn�w�s���
                    LastOrderDate = ps.LastOrderDate
                }).ToList();

                // �Ƨ�
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

                _logger.LogInformation("���\�����a {SellerId} �ӫ~���R�A�@ {Count} �Ӱӫ~", sellerId, productAnalysis.Count);

                return Ok(ApiResponse<SellerProductAnalysisResponseDto>.SuccessResult(response, "����ӫ~���R���\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�����a {SellerId} �ӫ~���R����", sellerId);
                return StatusCode(500, ApiResponse<SellerProductAnalysisResponseDto>.ErrorResult("����ӫ~���R���ѡG" + ex.Message));
            }
        }

        #region �p�����U��k

        /// <summary>
        /// ����S�w��������a�q��
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
        /// �p��q��έp
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
        /// �p�⦨���v
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
        /// �p��C��P��
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
                // ���Ѥ���
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
        /// �p��ӫ~�Z��
        /// </summary>
        private List<ProductPerformanceDto> CalculateProductPerformance(List<Order> orders)
        {
            // �o�̥i�H�ھ� OrderDetails �p��ӫ~�Z��
            // �Ȯɪ�^�ŦC��A�i�H���򧹵�
            return new List<ProductPerformanceDto>();
        }

        /// <summary>
        /// �p��q�檬�A����
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
        /// ������A����
        /// </summary>
        private string GetStatusLabel(string? status)
        {
            return status switch
            {
                "completed" => "�w����",
                "pending" => "�ݳB�z",
                "processing" => "�B�z��",
                "cancelled" => "�w����",
                "shipped" => "�w�X�f",
                "delivered" => "�w�e�F",
                _ => "����"
            };
        }

        #endregion
    }
}