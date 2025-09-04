using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SellerOrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SellerOrdersController> _logger;

        public SellerOrdersController(AppDbContext context, ILogger<SellerOrdersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region 賣家端訂單管理

        /// <summary>
        /// 獲取賣家訂單列表
        /// </summary>
        /// <param name="vendorId">賣家ID</param>
        /// <param name="status">訂單狀態篩選</param>
        /// <param name="page">頁碼</param>
        /// <param name="pageSize">每頁筆數</param>
        /// <param name="search">搜尋關鍵字</param>
        /// <param name="startDate">開始日期</param>
        /// <param name="endDate">結束日期</param>
        /// <param name="memberId">會員ID篩選</param>
        [HttpGet("vendor/{vendorId}")]
        public async Task<ActionResult<ApiResponse<SellerOrderListResponseDto>>> GetVendorOrders(
            int vendorId,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? memberId = null)
        {
            try
            {
                var query = _context.Orders
                    .Where(o => o.OrderDetails.Any(od => od.Product.SellersId == vendorId))
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages) // 加載商品圖片
                    .Include(o => o.Member)
                        .ThenInclude(m => m.MemberProfile)
                    .AsQueryable();

                // 狀態篩選
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(o => o.OrderStatus == status);
                }

                // 日期篩選
                if (startDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt <= endDate.Value);
                }

                // 會員篩選
                if (memberId.HasValue)
                {
                    query = query.Where(o => o.MemberId == memberId.Value);
                }

                // 搜尋篩選
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(o =>
                        o.Id.ToString().Contains(search) ||
                        o.Member.Email.Contains(search) ||
                        (o.Member.MemberProfile != null && 
                         (o.Member.MemberProfile.Name.Contains(search) || 
                          o.Member.MemberProfile.MemberAccount.Contains(search))) ||
                        o.OrderDetails.Any(od => od.Product.Name.Contains(search))
                    );
                }

                // 分頁
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var orders = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(o => new SellerOrderSummaryDto
                    {
                        OrderNumber = o.Id.ToString(),
                        CreatedAt = o.CreatedAt,
                        Status = o.OrderStatus,
                        PaymentStatus = o.PaymentStatus ?? "pending",
                        TotalAmount = o.TotalAmount,
                        DeliveryMethod = o.DeliveryMethod ?? "",
                        PaymentMethod = o.PaymentMethod ?? "",
                        RecipientName = "", // 需要從 OrderAddress 獲取
                        PhoneNumber = "", // 需要從 OrderAddress 獲取
                        ShippingAddress = "", // 需要從 OrderAddress 獲取
                        TrackingNumber = "", // Order 模型中沒有此欄位
                        MemberInfo = new OrderMemberInfoDto
                        {
                            MemberId = o.Member.Id,
                            Username = o.Member.MemberProfile != null ? o.Member.MemberProfile.Name : o.Member.Email,
                            Email = o.Member.Email
                        },
                        VendorItems = o.OrderDetails
                            .Where(od => od.Product.SellersId == vendorId)
                            .Select(od => new SellerOrderItemDto
                            {
                                Id = od.Id,
                                ProductId = od.ProductId,
                                ProductName = od.Product.Name,
                                Specifications = "", // OrderDetail 中沒有規格欄位，先留空
                                Quantity = od.Quantity ?? 0,
                                UnitPrice = od.UnitPrice ?? 0,
                                Subtotal = (od.UnitPrice ?? 0) * (od.Quantity ?? 0),
                                ProductImage = od.Product.ProductImages != null && od.Product.ProductImages.Any() 
                                    ? od.Product.ProductImages.OrderBy(pi => pi.SortOrder).First().ImagesUrl 
                                    : "" // 取得第一張圖片，按 SortOrder 排序
                            }).ToList(),
                        VendorSubtotal = o.OrderDetails
                            .Where(od => od.Product.SellersId == vendorId)
                            .Sum(od => (od.UnitPrice ?? 0) * (od.Quantity ?? 0))
                    })
                    .ToListAsync();

                var response = new SellerOrderListResponseDto
                {
                    Orders = orders,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    Page = page,
                    PageSize = pageSize
                };

                return Ok(ApiResponse<SellerOrderListResponseDto>.SuccessResult(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家訂單列表失敗: VendorId={VendorId}", vendorId);
                return StatusCode(500, ApiResponse<SellerOrderListResponseDto>.ErrorResult("獲取訂單列表失敗"));
            }
        }

        /// <summary>
        /// 獲取訂單詳細資料
        /// </summary>
        /// <param name="orderNumber">訂單編號</param>
        /// <param name="vendorId">賣家ID</param>
        [HttpGet("{orderNumber}/vendor/{vendorId}")]
        public async Task<ActionResult<ApiResponse<SellerOrderDetailDto>>> GetOrderDetail(
            string orderNumber, 
            int vendorId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages) // 加載商品圖片
                    .Include(o => o.Member)
                        .ThenInclude(m => m.MemberProfile)
                    .FirstOrDefaultAsync(o => o.Id.ToString() == orderNumber &&
                                           o.OrderDetails.Any(od => od.Product.SellersId == vendorId));

                if (order == null)
                {
                    return NotFound(ApiResponse<SellerOrderDetailDto>.ErrorResult("找不到訂單或無權限查看"));
                }

                var vendorItems = order.OrderDetails.Where(od => od.Product.SellersId == vendorId).ToList();

                var orderDetail = new SellerOrderDetailDto
                {
                    OrderNumber = order.Id.ToString(),
                    CreatedAt = order.CreatedAt,
                    Status = order.OrderStatus,
                    PaymentStatus = order.PaymentStatus ?? "pending",
                    TotalAmount = order.TotalAmount,
                    DeliveryMethod = order.DeliveryMethod ?? "",
                    PaymentMethod = order.PaymentMethod ?? "",
                    RecipientName = "", // 需要從 OrderAddress 獲取
                    PhoneNumber = "", // 需要從 OrderAddress 獲取  
                    ShippingAddress = "", // 需要從 OrderAddress 獲取
                    TrackingNumber = "", // Order 模型中沒有此欄位
                    PaidAt = null, // 需要從 Payment 或其他地方獲取
                    ShippedAt = null, // Order 模型中沒有此欄位
                    DeliveredAt = null, // Order 模型中沒有此欄位
                    
                    MemberInfo = new OrderMemberInfoDto
                    {
                        MemberId = order.Member.Id,
                        Username = order.Member.MemberProfile?.Name ?? order.Member.Email,
                        Email = order.Member.Email
                    },

                    VendorItems = vendorItems.Select(od => new SellerOrderItemDto
                    {
                        Id = od.Id,
                        ProductId = od.ProductId,
                        ProductName = od.Product.Name,
                        Specifications = "",
                        Quantity = od.Quantity ?? 0,
                        UnitPrice = od.UnitPrice ?? 0,
                        Subtotal = (od.UnitPrice ?? 0) * (od.Quantity ?? 0),
                        ProductImage = od.Product.ProductImages != null && od.Product.ProductImages.Any() 
                            ? od.Product.ProductImages.OrderBy(pi => pi.SortOrder).First().ImagesUrl 
                            : ""
                    }).ToList(),

                    VendorSubtotal = vendorItems.Sum(od => (od.UnitPrice ?? 0) * (od.Quantity ?? 0))
                };

                return Ok(ApiResponse<SellerOrderDetailDto>.SuccessResult(orderDetail));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取訂單詳細資料失敗: OrderNumber={OrderNumber}", orderNumber);
                return StatusCode(500, ApiResponse<SellerOrderDetailDto>.ErrorResult("獲取訂單詳細資料失敗"));
            }
        }

        /// <summary>
        /// 更新出貨狀態
        /// </summary>
        /// <param name="orderNumber">訂單編號</param>
        /// <param name="request">更新請求</param>
        [HttpPut("{orderNumber}/shipping-status")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateShippingStatus(
            string orderNumber, 
            [FromBody] UpdateShippingStatusRequestDto request)
        {
            try
            {
                // 先查詢訂單是否存在
                var orderCheck = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                    .FirstOrDefaultAsync(o => o.Id.ToString() == orderNumber);

                if (orderCheck == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult("找不到訂單"));
                }

                // 檢查訂單商品的賣家 ID
                var productSellerIds = orderCheck.OrderDetails.Select(od => od.Product.SellersId).Distinct().ToList();
                
                if (!productSellerIds.Contains(request.VendorId))
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"無權限操作此訂單。此訂單的商品屬於賣家 ID: {string.Join(", ", productSellerIds)}，您的賣家 ID: {request.VendorId}"));
                }

                var order = orderCheck;
                // 確保 Shipments 也被加載
                await _context.Entry(order)
                    .Collection(o => o.Shipments)
                    .LoadAsync();

                if (order == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult("找不到訂單或無權限操作"));
                }

                // 檢查訂單狀態（允許狀態轉換：pending -> shipped -> delivered）
                if (request.Status == "shipped")
                {
                    // 出貨條件：訂單狀態必須是 pending 或 paid
                    if (!new[] { "pending", "paid" }.Contains(order.OrderStatus))
                    {
                        return BadRequest(ApiResponse<object>.ErrorResult("此訂單狀態不允許出貨"));
                    }
                }
                else if (request.Status == "delivered")
                {
                    // 送達條件：訂單狀態必須是 shipped
                    if (order.OrderStatus != "shipped")
                    {
                        return BadRequest(ApiResponse<object>.ErrorResult("只有已出貨的訂單才能標記為已送達"));
                    }
                    
                    // 檢查付款方式和付款狀態
                    // 如果是貨到付款 (COD)，送達時自動完成付款
                    if (order.PaymentMethod?.ToLower() == "cod")
                    {
                        order.PaymentStatus = "completed";
                        
                        // 更新或創建 Payment 記錄
                        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id);
                        if (payment != null)
                        {
                            payment.Status = "completed";
                            payment.TransactionTime = DateTime.Now;
                        }
                        else
                        {
                            // 如果沒有 Payment 記錄，創建一個
                            var newPayment = new Payment
                            {
                                OrderId = order.Id,
                                Status = "completed",
                                TransactionTime = DateTime.Now
                            };
                            _context.Payments.Add(newPayment);
                        }
                    }
                    // 如果不是貨到付款，必須先完成付款才能送達
                    else if (order.PaymentStatus != "completed")
                    {
                        return BadRequest(ApiResponse<object>.ErrorResult("非貨到付款訂單必須先完成付款才能標記為已送達"));
                    }
                }

                // 更新訂單狀態
                order.OrderStatus = request.Status; // 使用請求中的狀態
                order.UpdatedAt = DateTime.Now;

                // 創建或更新 Shipment 記錄
                var existingShipment = await _context.Shipments.FirstOrDefaultAsync(s => s.OrderId == order.Id);
                
                if (request.Status == "shipped")
                {
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
                    else
                    {
                        existingShipment.TrackingNumber = request.TrackingNumber;
                        existingShipment.ShippedAt = DateTime.Now;
                        existingShipment.Status = "shipped";
                    }
                }
                else if (request.Status == "delivered" && existingShipment != null)
                {
                    existingShipment.DeliveredAt = DateTime.Now;
                    existingShipment.Status = "delivered";
                    
                    // 如果提供了新的追蹤號碼，也一併更新
                    if (!string.IsNullOrWhiteSpace(request.TrackingNumber) && request.TrackingNumber != "string")
                    {
                        existingShipment.TrackingNumber = request.TrackingNumber;
                    }
                }
                
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResult(new { }, "物流狀態已更新"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新物流狀態失敗: OrderNumber={OrderNumber}", orderNumber);
                return StatusCode(500, ApiResponse<object>.ErrorResult("更新物流狀態失敗"));
            }
        }

        #endregion

        #region 賣家統計

        /// <summary>
        /// 獲取賣家訂單統計
        /// </summary>
        /// <param name="vendorId">賣家ID</param>
        /// <param name="days">統計天數（預設30天）</param>
        [HttpGet("vendor/{vendorId}/statistics")]
        public async Task<ActionResult<ApiResponse<SellerOrderStatisticsDto>>> GetVendorOrderStatistics(
            int vendorId,
            [FromQuery] int days = 30)
        {
            try
            {
                var startDate = DateTime.Now.AddDays(-days);
                var endDate = DateTime.Now;

                var orders = await _context.Orders
                    .Where(o => o.OrderDetails.Any(od => od.Product.SellersId == vendorId))
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                    .ToListAsync();

                var statistics = new SellerOrderStatisticsDto
                {
                    TotalOrders = orders.Count,
                    PendingOrders = orders.Count(o => o.OrderStatus == "pending"),
                    PaidOrders = orders.Count(o => o.OrderStatus == "paid"),
                    ShippedOrders = orders.Count(o => o.OrderStatus == "shipped"),
                    DeliveredOrders = orders.Count(o => o.OrderStatus == "delivered"),
                    CancelledOrders = orders.Count(o => o.OrderStatus == "cancelled"),
                    TotalRevenue = orders
                        .Where(o => o.OrderStatus != "cancelled")
                        .Sum(o => o.OrderDetails
                            .Where(od => od.Product.SellersId == vendorId)
                            .Sum(od => (od.UnitPrice ?? 0) * (od.Quantity ?? 0)))
                };

                return Ok(ApiResponse<SellerOrderStatisticsDto>.SuccessResult(statistics));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取賣家統計失敗: VendorId={VendorId}", vendorId);
                return StatusCode(500, ApiResponse<SellerOrderStatisticsDto>.ErrorResult("獲取統計資料失敗"));
            }
        }

        #endregion

        #region 商品級別出貨管理

        /// <summary>
        /// 更新單一商品項目的出貨狀態 (多賣家支援)
        /// </summary>
        /// <param name="orderDetailId">訂單明細ID</param>
        /// <param name="vendorId">賣家ID</param>
        /// <param name="request">出貨狀態更新請求</param>
        [HttpPut("detail/{orderDetailId}/vendor/{vendorId}/shipping-status")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateOrderDetailShippingStatus(
            int orderDetailId,
            int vendorId,
            [FromBody] UpdateShippingStatusRequestDto request)
        {
            try
            {
                // 驗證是否為該賣家的商品
                var orderDetail = await _context.OrderDetails
                    .Include(od => od.Product)
                    .Include(od => od.Order)
                    .FirstOrDefaultAsync(od => od.Id == orderDetailId && od.Product.SellersId == vendorId);

                if (orderDetail == null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult("找不到該商品訂單或您無權限操作"));
                }

                // 目前先用 Order 層級的狀態管理 (暫時方案)
                // 實際應該為 OrderDetail 增加獨立狀態欄位
                var currentOrder = orderDetail.Order;
                
                switch (request.Status.ToLower())
                {
                    case "shipped":
                        // 檢查是否可以出貨
                        if (!new[] { "pending", "paid" }.Contains(currentOrder.OrderStatus))
                        {
                            return BadRequest(ApiResponse<object>.ErrorResult("此商品項目狀態不允許出貨"));
                        }
                        
                        // 多賣家策略：暫時直接更新為出貨狀態
                        // 實際應該建立商品級別的出貨狀態追蹤表
                        
                        _logger.LogInformation("賣家 {VendorId} 標記商品 {OrderDetailId} 為已出貨", vendorId, orderDetailId);
                        
                        // 簡化版本：直接標記訂單為已出貨準備
                        // TODO: 實作商品級別的出貨狀態追蹤
                        currentOrder.UpdatedAt = DateTime.Now;
                        
                        break;
                        
                    case "delivered":
                        // 送達邏輯維持不變
                        currentOrder.OrderStatus = "delivered";
                        currentOrder.UpdatedAt = DateTime.Now;
                        
                        if (currentOrder.PaymentMethod == "COD" && currentOrder.PaymentStatus != "paid")
                        {
                            currentOrder.PaymentStatus = "paid";
                            _logger.LogInformation("COD 訂單 {OrderId} 送達後自動標記為已付款", currentOrder.Id);
                        }
                        
                        _logger.LogInformation("商品項目 {OrderDetailId} (賣家 {VendorId}) 已標記為送達", orderDetailId, vendorId);
                        break;
                        
                    default:
                        return BadRequest(ApiResponse<object>.ErrorResult("無效的狀態值"));
                }

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResult(new
                {
                    orderDetailId = orderDetailId,
                    orderId = currentOrder.Id,
                    vendorId = vendorId,
                    newStatus = request.Status,
                    updatedAt = DateTime.Now,
                    message = $"商品項目出貨狀態已更新為 {request.Status}"
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新商品出貨狀態失敗: OrderDetailId={OrderDetailId}, VendorId={VendorId}", orderDetailId, vendorId);
                return StatusCode(500, ApiResponse<object>.ErrorResult("更新商品出貨狀態失敗"));
            }
        }

        #endregion
    }
}
