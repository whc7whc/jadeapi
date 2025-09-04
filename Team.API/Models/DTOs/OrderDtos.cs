namespace Team.API.Models.DTOs
{
    /// <summary>
    /// 訂單列表回應 DTO
    /// </summary>
    public class OrderListResponseDto
    {
        public List<OrderSummaryDto> Orders { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// 訂單摘要 DTO
    /// </summary>
    public class OrderSummaryDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public decimal ProcessingFee { get; set; } // 新增付款手續費字段
        public string PaymentMethod { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public DateTime? PaymentDeadline { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// 訂單詳情 DTO
    /// </summary>
    public class OrderDetailDto : OrderSummaryDto
    {
        // 繼承 OrderSummaryDto 的所有屬性
        // 可以在這裡添加額外的詳情字段
        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// 訂單項目 DTO
    /// </summary>
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int OrderDetailId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public string? ProductSku { get; set; }
        public string? Specifications { get; set; }
        public int AttributeValueId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string AttributeValue { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
    }

    /// <summary>
    /// 取消訂單請求 DTO
    /// </summary>
    public class CancelOrderRequestDto
    {
        public int MemberId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 確認收貨請求 DTO
    /// </summary>
    public class ConfirmDeliveryRequestDto
    {
        public int MemberId { get; set; }
    }

    /// <summary>
    /// 申請退款請求 DTO
    /// </summary>
    public class RefundRequestDto
    {
        public int MemberId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<RefundItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// 退款項目 DTO
    /// </summary>
    public class RefundItemDto
    {
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新訂單狀態請求 DTO（測試用）
    /// </summary>
    public class UpdateOrderStatusRequestDto
    {
        public string Status { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
    }
}
