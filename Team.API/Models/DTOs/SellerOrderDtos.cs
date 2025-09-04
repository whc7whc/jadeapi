namespace Team.API.Models.DTOs
{
    /// <summary>
    /// 賣家訂單列表回應 DTO
    /// </summary>
    public class SellerOrderListResponseDto
    {
        public List<SellerOrderSummaryDto> Orders { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// 賣家訂單摘要 DTO
    /// </summary>
    public class SellerOrderSummaryDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public OrderMemberInfoDto MemberInfo { get; set; } = new();
        public List<SellerOrderItemDto> VendorItems { get; set; } = new();
        public decimal VendorSubtotal { get; set; }
    }

    /// <summary>
    /// 賣家訂單詳情 DTO
    /// </summary>
    public class SellerOrderDetailDto : SellerOrderSummaryDto
    {
        public DateTime? PaidAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    /// <summary>
    /// 賣家訂單項目 DTO
    /// </summary>
    public class SellerOrderItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Specifications { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? ProductImage { get; set; }
    }

    /// <summary>
    /// 訂單會員資訊 DTO
    /// </summary>
    public class OrderMemberInfoDto
    {
        public int MemberId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新物流狀態請求 DTO
    /// </summary>
    public class UpdateShippingStatusRequestDto
    {
        public int VendorId { get; set; }
        public string Status { get; set; } = string.Empty; // "shipped" or "delivered"
        public string? TrackingNumber { get; set; }
    }

    /// <summary>
    /// 賣家訂單統計 DTO
    /// </summary>
    public class SellerOrderStatisticsDto
    {
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int PaidOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
