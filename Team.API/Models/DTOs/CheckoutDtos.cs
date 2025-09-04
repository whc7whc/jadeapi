using System.ComponentModel.DataAnnotations;

namespace Team.API.Models.DTOs
{
    /// <summary>
    /// 結帳請求 DTO
    /// </summary>
    public class CheckoutRequestDto
    {
        [Required(ErrorMessage = "會員ID不能為空")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "收件人姓名不能為空")]
        [StringLength(100, ErrorMessage = "收件人姓名長度不能超過100字元")]
        public string RecipientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "聯絡電話不能為空")]
        [StringLength(20, ErrorMessage = "聯絡電話長度不能超過20字元")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "城市不能為空")]
        [StringLength(50, ErrorMessage = "城市長度不能超過50字元")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "行政區不能為空")]
        [StringLength(50, ErrorMessage = "行政區長度不能超過50字元")]
        public string District { get; set; } = string.Empty;

        [Required(ErrorMessage = "詳細地址不能為空")]
        [StringLength(255, ErrorMessage = "詳細地址長度不能超過255字元")]
        public string AddressDetail { get; set; } = string.Empty;

        [Required(ErrorMessage = "配送方式不能為空")]
        [StringLength(50, ErrorMessage = "配送方式長度不能超過50字元")]
        public string DeliveryMethod { get; set; } = string.Empty;

        [Required(ErrorMessage = "付款方式不能為空")]
        [StringLength(50, ErrorMessage = "付款方式長度不能超過50字元")]
        public string PaymentMethod { get; set; } = string.Empty;

        public string? CouponCode { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "使用點數不能為負數")]
        public int UsedPoints { get; set; } = 0;

        public string? Note { get; set; }

        // 是否使用儲存的地址
        public int? AddressId { get; set; }
    }

    /// <summary>
    /// 結帳回應 DTO
    /// </summary>
    public class CheckoutResponseDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public PaymentInfoDto? PaymentInfo { get; set; }
    }

    /// <summary>
    /// 付款資訊 DTO
    /// </summary>
    public class PaymentInfoDto
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentUrl { get; set; }
        public string? TransactionId { get; set; }
        public Dictionary<string, object>? AdditionalInfo { get; set; }
    }

    /// <summary>
    /// 訂單確認 DTO
    /// </summary>
    public class OrderConfirmationDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public string MemberEmail { get; set; } = string.Empty;
        
        // 收件資訊
        public string RecipientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty;

        // 金額資訊
        public decimal SubtotalAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PointsDeductAmount { get; set; }
        public decimal TotalAmount { get; set; }

        // 付款資訊
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;

        // 訂單商品
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();

        // 優惠券資訊
        public string? CouponCode { get; set; }
        public string? CouponTitle { get; set; }

        // 時間資訊
        public DateTime CreatedAt { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
    }

    /// <summary>
    /// 結帳前驗證 DTO
    /// </summary>
    public class CheckoutValidationDto
    {
        public bool IsValid { get; set; }
        public List<CheckoutValidationError> Errors { get; set; } = new List<CheckoutValidationError>();
        public CheckoutSummaryDto? Summary { get; set; }
    }

    /// <summary>
    /// 結帳驗證錯誤 DTO
    /// </summary>
    public class CheckoutValidationError
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    /// <summary>
    /// 結帳摘要 DTO
    /// </summary>
    public class CheckoutSummaryDto
    {
        public int ItemCount { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PointsDeductAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool FreeShipping { get; set; }
        public List<CheckoutItemDto> Items { get; set; } = new List<CheckoutItemDto>();
        public CouponInfoDto? AppliedCoupon { get; set; }
        public int AvailablePoints { get; set; }
        public decimal MaxPointsDeduction { get; set; }
    }

    /// <summary>
    /// 結帳商品項目 DTO
    /// </summary>
    public class CheckoutItemDto
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public int AttributeValueId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string AttributeValue { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int AvailableStock { get; set; }
    }

    /// <summary>
    /// 優惠券資訊 DTO
    /// </summary>
    public class CouponInfoDto
    {
        public int CouponId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public int DiscountAmount { get; set; }
        public int? MinSpend { get; set; }
        public decimal CalculatedDiscount { get; set; }
    }

    /// <summary>
    /// 快速結帳 DTO (一頁式結帳)
    /// </summary>
    public class QuickCheckoutDto
    {
        [Required(ErrorMessage = "會員ID不能為空")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "商品ID不能為空")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "屬性值ID不能為空")]
        public int AttributeValueId { get; set; }

        [Required(ErrorMessage = "數量不能為空")]
        [Range(1, 99, ErrorMessage = "數量必須在 1 到 99 之間")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "收件人資訊不能為空")]
        public CheckoutRequestDto DeliveryInfo { get; set; } = new CheckoutRequestDto();
    }

    /// <summary>
    /// 訂單狀態更新 DTO
    /// </summary>
    public class OrderStatusUpdateDto
    {
        [Required(ErrorMessage = "訂單狀態不能為空")]
        public string OrderStatus { get; set; } = string.Empty;

        [Required(ErrorMessage = "付款狀態不能為空")]
        public string PaymentStatus { get; set; } = string.Empty;

        public string? Note { get; set; }
        public string? TrackingNumber { get; set; }
    }

    /// <summary>
    /// 配送方式 DTO
    /// </summary>
    public class DeliveryMethodDto
    {
        public string Method { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Fee { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public int EstimatedDays { get; set; }
    }

    /// <summary>
    /// 付款方式 DTO
    /// </summary>
    public class PaymentMethodDto
    {
        public string Method { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public decimal? ProcessingFee { get; set; }
        public string? IconUrl { get; set; }
    }
}