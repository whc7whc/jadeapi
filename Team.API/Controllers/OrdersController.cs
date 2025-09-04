using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.Services;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly ICheckoutService _checkoutService;

        public OrdersController(AppDbContext context, ILogger<OrdersController> logger, ICheckoutService checkoutService)
        {
            _context = context;
            _logger = logger;
            _checkoutService = checkoutService;
        }

        #region 顧客端訂單查詢

        /// <summary>
        /// 獲取會員的訂單列表
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="status">訂單狀態篩選</param>
        /// <param name="page">頁碼</param>
        /// <param name="pageSize">每頁數量</param>
        /// <param name="search">搜尋關鍵字</param>
        /// <param name="days">日期範圍（天數）</param>
        [HttpGet("member/{memberId}")]
        public async Task<ActionResult<ApiResponse<OrderListResponseDto>>> GetMemberOrders(
            int memberId,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] int? days = null)
        {
            try
            {
                var query = _context.Orders
                    .Where(o => o.MemberId == memberId)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(o => o.Shipments)
                    .Include(o => o.Payments)
                    .AsQueryable();

                // 狀態篩選
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(o => o.OrderStatus == status);
                }

                // 日期篩選
                if (days.HasValue)
                {
                    var cutoffDate = DateTime.Now.AddDays(-days.Value);
                    query = query.Where(o => o.CreatedAt >= cutoffDate);
                }

                // 搜尋篩選
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(o => 
                        o.Id.ToString().Contains(search) ||
                        o.OrderDetails.Any(od => od.Product.Name.Contains(search))
                    );
                }

                // 總數計算
                var totalCount = await query.CountAsync();

                // 分頁和排序
                var orders = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(o => new OrderSummaryDto
                    {
                        Id = o.Id,
                        OrderNumber = "ORD" + o.Id.ToString("D8"), // 生成訂單編號
                        CreatedAt = o.CreatedAt,
                        Status = o.OrderStatus ?? "pending",
                        TotalAmount = o.TotalAmount,
                        Subtotal = o.SubtotalAmount,
                        ShippingFee = o.ShippingFee,
                        Discount = o.DiscountAmount ?? 0,
                        PaymentMethod = o.PaymentMethod ?? "",
                        DeliveryMethod = o.DeliveryMethod ?? "",
                        RecipientName = o.RecipientName ?? "",
                        PhoneNumber = o.PhoneNumber ?? "",
                        ShippingAddress = $"{o.City}{o.District}{o.AddressDetail}",
                        TrackingNumber = o.Shipments.FirstOrDefault() != null ? o.Shipments.FirstOrDefault().TrackingNumber : "",
                        PaymentDeadline = o.CreatedAt.AddHours(48), // 48小時付款期限
                        PaidAt = o.Payments.Where(p => p.Status == "completed").OrderBy(p => p.TransactionTime).FirstOrDefault() != null ? 
                                o.Payments.Where(p => p.Status == "completed").OrderBy(p => p.TransactionTime).FirstOrDefault().TransactionTime : null,
                        ShippedAt = o.Shipments.Where(s => s.ShippedAt != null).OrderBy(s => s.ShippedAt).FirstOrDefault() != null ?
                                   o.Shipments.Where(s => s.ShippedAt != null).OrderBy(s => s.ShippedAt).FirstOrDefault().ShippedAt : null,
                        DeliveredAt = o.Shipments.Where(s => s.DeliveredAt != null).OrderBy(s => s.DeliveredAt).FirstOrDefault() != null ?
                                     o.Shipments.Where(s => s.DeliveredAt != null).OrderBy(s => s.DeliveredAt).FirstOrDefault().DeliveredAt : null,
                        CompletedAt = o.OrderStatus == "completed" ? o.UpdatedAt : null,
                        Items = o.OrderDetails.Select(od => new OrderItemDto
                        {
                            Id = od.Id,
                            OrderDetailId = od.Id,
                            ProductId = od.ProductId,
                            ProductName = od.Product.Name ?? "",
                            ProductImage = od.Product.ProductImages.OrderBy(pi => pi.SortOrder).FirstOrDefault().ImagesUrl ?? "/images/default-product.png", // 取得第一張產品圖片
                            Specifications = "", // 需要從 AttributeValue 取得
                            UnitPrice = od.UnitPrice ?? 0,
                            Price = od.UnitPrice ?? 0, // 兼容舊屬性名
                            Quantity = od.Quantity ?? 0,
                            Subtotal = od.Subtotal ?? 0
                        }).ToList()
                    })
                    .ToListAsync();

                var response = new OrderListResponseDto
                {
                    Orders = orders,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                return Ok(ApiResponse<OrderListResponseDto>.SuccessResult(response, "成功獲取訂單列表"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取會員訂單列表失敗: MemberId={MemberId}", memberId);
                return StatusCode(500, ApiResponse<OrderListResponseDto>.ErrorResult("獲取訂單列表失敗"));
            }
        }

        /// <summary>
        /// 獲取訂單詳情
        /// </summary>
        /// <param name="orderNumber">訂單編號</param>
        /// <param name="memberId">會員ID</param>
        [HttpGet("{orderNumber}")]
        public async Task<ActionResult<ApiResponse<OrderDetailDto>>> GetOrderDetail(
            string orderNumber, 
            [FromQuery] int memberId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(o => o.Shipments)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id.ToString() == orderNumber && o.MemberId == memberId);

                if (order == null)
                {
                    return NotFound(ApiResponse<OrderDetailDto>.ErrorResult("找不到訂單或無權限查看"));
                }

                var orderDetail = new OrderDetailDto
                {
                    OrderNumber = "ORD" + order.Id.ToString("D8"),
                    CreatedAt = order.CreatedAt,
                    Status = order.OrderStatus ?? "pending",
                    TotalAmount = order.TotalAmount,
                    Subtotal = order.SubtotalAmount,
                    ShippingFee = order.ShippingFee,
                    Discount = order.DiscountAmount ?? 0,
                    ProcessingFee = await _checkoutService.GetPaymentProcessingFeeAsync(order.PaymentMethod ?? ""), // 添加付款手續費計算
                    PaymentMethod = order.PaymentMethod ?? "",
                    DeliveryMethod = order.DeliveryMethod ?? "",
                    RecipientName = order.RecipientName ?? "",
                    PhoneNumber = order.PhoneNumber ?? "",
                    ShippingAddress = $"{order.City}{order.District}{order.AddressDetail}",
                    TrackingNumber = order.Shipments.FirstOrDefault() != null ? order.Shipments.FirstOrDefault().TrackingNumber : "",
                    PaymentDeadline = order.CreatedAt.AddHours(48), // 48小時付款期限
                    PaidAt = order.Payments.Where(p => p.Status == "completed").OrderBy(p => p.TransactionTime).FirstOrDefault() != null ? 
                            order.Payments.Where(p => p.Status == "completed").OrderBy(p => p.TransactionTime).FirstOrDefault().TransactionTime : null,
                    ShippedAt = order.Shipments.Where(s => s.ShippedAt != null).OrderBy(s => s.ShippedAt).FirstOrDefault() != null ?
                               order.Shipments.Where(s => s.ShippedAt != null).OrderBy(s => s.ShippedAt).FirstOrDefault().ShippedAt : null,
                    DeliveredAt = order.Shipments.Where(s => s.DeliveredAt != null).OrderBy(s => s.DeliveredAt).FirstOrDefault() != null ?
                                 order.Shipments.Where(s => s.DeliveredAt != null).OrderBy(s => s.DeliveredAt).FirstOrDefault().DeliveredAt : null,
                    CompletedAt = order.OrderStatus == "completed" ? order.UpdatedAt : null,
                    Items = order.OrderDetails.Select(od => new OrderItemDto
                    {
                        Id = od.Id,
                        OrderDetailId = od.Id,
                        ProductId = od.ProductId,
                        ProductName = od.Product.Name ?? "",
                        ProductImage = od.Product.ProductImages.OrderBy(pi => pi.SortOrder).FirstOrDefault().ImagesUrl ?? "/images/default-product.png", // 取得第一張產品圖片
                        Specifications = "", // 需要從 AttributeValue 取得
                        UnitPrice = od.UnitPrice ?? 0,
                        Price = od.UnitPrice ?? 0, // 兼容舊屬性名
                        Quantity = od.Quantity ?? 0,
                        Subtotal = od.Subtotal ?? 0
                    }).ToList()
                };

                return Ok(ApiResponse<OrderDetailDto>.SuccessResult(orderDetail, "成功獲取訂單詳情"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取訂單詳情失敗: OrderNumber={OrderNumber}", orderNumber);
                return StatusCode(500, ApiResponse<OrderDetailDto>.ErrorResult("獲取訂單詳情失敗"));
            }
        }

        /// <summary>
        /// 取消訂單
        /// </summary>
        /// <param name="orderNumber">訂單編號</param>
        /// <param name="request">取消訂單請求</param>
        [HttpPost("{orderNumber}/cancel")]
        public async Task<ActionResult<ApiResponse<object>>> CancelOrder(
            string orderNumber, 
            [FromBody] CancelOrderRequestDto request)
        {
            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id.ToString() == orderNumber && o.MemberId == request.MemberId);

                if (order == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult("找不到訂單或無權限操作"));
                }

                // 檢查訂單狀態是否可以取消
                if (order.OrderStatus != "pending" && order.OrderStatus != "paid")
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("此訂單狀態不允許取消"));
                }

                // 更新訂單狀態
                order.OrderStatus = "cancelled";
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResult(new { }, "訂單已成功取消"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消訂單失敗: OrderNumber={OrderNumber}", orderNumber);
                return StatusCode(500, ApiResponse<object>.ErrorResult("取消訂單失敗"));
            }
        }

        /// <summary>
        /// 確認收貨
        /// </summary>
        /// <param name="orderNumber">訂單編號</param>
        /// <param name="request">確認收貨請求</param>
        [HttpPost("{orderNumber}/confirm-delivery")]
        public async Task<ActionResult<ApiResponse<object>>> ConfirmDelivery(
            string orderNumber, 
            [FromBody] ConfirmDeliveryRequestDto request)
        {
            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id.ToString() == orderNumber && o.MemberId == request.MemberId);

                if (order == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult("找不到訂單或無權限操作"));
                }

                // 檢查訂單狀態
                if (order.OrderStatus != "shipped")
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("此訂單狀態不允許確認收貨"));
                }

                // 更新訂單狀態
                order.OrderStatus = "delivered";
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResult(new { }, "已確認收貨"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "確認收貨失敗: OrderNumber={OrderNumber}", orderNumber);
                return StatusCode(500, ApiResponse<object>.ErrorResult("確認收貨失敗"));
            }
        }

        #endregion

        #region 測試用 API（開發環境）

        /// <summary>
        /// 更新訂單狀態（僅用於測試）
        /// </summary>
        /// <param name="orderNumber">訂單編號</param>
        /// <param name="request">更新請求</param>
        [HttpPut("{orderNumber}/status")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateOrderStatus(
            string orderNumber,
            [FromBody] UpdateOrderStatusRequestDto request)
        {
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id.ToString() == orderNumber);

                if (order == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult("找不到訂單"));
                }

                // 更新訂單狀態
                order.OrderStatus = request.Status;
                order.UpdatedAt = DateTime.Now;

                // 如果是已出貨狀態，創建 Shipment 記錄
                if (request.Status == "shipped" && !string.IsNullOrEmpty(request.TrackingNumber))
                {
                    var existingShipment = await _context.Shipments.FirstOrDefaultAsync(s => s.OrderId == order.Id);
                    if (existingShipment == null)
                    {
                        var shipment = new Shipment
                        {
                            OrderId = order.Id,
                            TrackingNumber = request.TrackingNumber,
                            ShippedAt = DateTime.Now,
                            Status = "shipped"
                        };
                        _context.Shipments.Add(shipment);
                    }
                }

                // 如果是已送達狀態，更新 Shipment 的送達時間
                if (request.Status == "delivered")
                {
                    var shipment = await _context.Shipments.FirstOrDefaultAsync(s => s.OrderId == order.Id);
                    if (shipment != null)
                    {
                        shipment.DeliveredAt = DateTime.Now;
                    }
                }

                // 如果是已付款狀態，創建 Payment 記錄
                if (request.Status == "paid")
                {
                    var existingPayment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id && p.Status == "completed");
                    if (existingPayment == null)
                    {
                        var payment = new Payment
                        {
                            OrderId = order.Id,
                            Status = "completed",
                            TransactionTime = DateTime.Now
                        };
                        _context.Payments.Add(payment);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResult(new { Status = order.OrderStatus }, "訂單狀態已更新"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新訂單狀態失敗: OrderNumber={OrderNumber}", orderNumber);
                return StatusCode(500, ApiResponse<object>.ErrorResult("更新訂單狀態失敗"));
            }
        }

        #endregion
    }
}
