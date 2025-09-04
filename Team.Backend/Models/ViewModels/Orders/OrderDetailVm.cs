namespace Team.Backend.Models.ViewModels.Orders;

/// 明細中的商品項目
public class OrderDetailItemVm
{
    public string Name { get; set; } = ""; // 產品名稱 + 規格（例如 商品A/黑/M）
    public int Qty { get; set; }
    public decimal Price { get; set; }     // 單價
    public decimal Subtotal { get; set; }  // 小計
    public string SellerName { get; set; } = ""; // 新增：賣家名稱
}

/// 賣家訂單群組
public class VendorOrderGroup
{
    public int SellerId { get; set; }
    public string VendorName { get; set; } = "";
    public string OrderCode { get; set; } = "";        // 訂單編號
    public string OrderStatus { get; set; } = "";
    public decimal SubTotal { get; set; }              // 商品小計
    public decimal ShippingFee { get; set; }           // 運費
    public decimal Total { get; set; }                 // 該賣家總計
    public DateTime? ShippedAt { get; set; }
    public string TrackingNumber { get; set; } = "";
    public List<OrderDetailItemVm> Items { get; set; } = new();
}

/// 明細畫面／Modal 要用的欄位
public class OrderDetailVm
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";       // 組好的完整地址字串
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PaymentStatus { get; set; } = "";
    public string OverallStatus { get; set; } = "";

    // 改為按賣家分組顯示
    public List<VendorOrderGroup> VendorOrderGroups { get; set; } = new();
    
    // 保留向後兼容，但標記為過時
    [Obsolete("請使用 VendorOrderGroups")]
    public List<OrderDetailItemVm> Items { get; set; } = new();
}
