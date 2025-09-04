using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.DTOs
{
    // 統一發送訂單的 DTO
    public class ProcessShipmentsDto
    {
        [Required]
        public List<int> OrderIds { get; set; } = new List<int>();
        
        public string? TrackingMethod { get; set; }
        public string? Notes { get; set; }
    }

    // 訂單賣家狀態 DTO
    public class OrderVendorStatusDto
    {
        public int OrderId { get; set; }
        public List<VendorStatusDto> Vendors { get; set; } = new List<VendorStatusDto>();
    }

    // 賣家狀態詳情 DTO
    public class VendorStatusDto
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // ready, pending, shipped
        public int ItemCount { get; set; }
        public List<VendorItemDto> Items { get; set; } = new List<VendorItemDto>();
        public DateTime? LastUpdated { get; set; }
    }

    // 賣家商品項目 DTO
    public class VendorItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string ShippingStatus { get; set; } = string.Empty;
        public DateTime? ShippedAt { get; set; }
    }

    // 待發送訂單資訊 DTO
    public class PendingShipmentDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public int VendorCount { get; set; }
        public int ReadyVendorCount { get; set; }
        public bool AllVendorsReady { get; set; }
    }
}
