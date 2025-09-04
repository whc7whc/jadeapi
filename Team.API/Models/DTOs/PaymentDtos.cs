namespace Team.API.Models.DTOs
{
    public class CreatePaymentOrderDto
    {
        public int OrderId { get; set; }
        public int MemberId { get; set; }
        public int TotalAmount { get; set; }
        public List<PaymentItemDto> Items { get; set; } = new();
        public string PaymentMethod { get; set; } = "Credit"; // ALL, Credit, WebATM, etc.
    }

    public class PaymentItemDto
    {
        public string Name { get; set; } = default!;
        public int Price { get; set; }
        public int Quantity { get; set; }
    }

    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = default!;
        public string? Html { get; set; }
        public string? MerchantTradeNo { get; set; }
    }

    public class PaymentCallbackDto
    {
        public string MerchantTradeNo { get; set; } = default!;
        public string? TradeNo { get; set; }
        public int RtnCode { get; set; }
        public string? RtnMsg { get; set; }
        public int TradeAmt { get; set; }
        public string? PaymentDate { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentTypeChargeFee { get; set; }
        public string? TradeDate { get; set; }
        public bool? SimulatePaid { get; set; }
        public string CheckMacValue { get; set; } = default!;
    }

    public class CheckoutToPaymentDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class OrderPaymentDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<PaymentOrderItemDto> Items { get; set; } = new List<PaymentOrderItemDto>();
    }

    public class PaymentOrderItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
    }
}
