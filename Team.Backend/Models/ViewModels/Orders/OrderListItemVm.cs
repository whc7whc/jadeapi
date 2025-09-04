namespace Team.Backend.Models.ViewModels.Orders;

/// 清單頁每一列要顯示的欄位
public class OrderListItemVm
{
    public int Id { get; set; }
    public string Code { get; set; } = "";          // 顯示用編號（例如 "#" + Id）
    public string MemberName { get; set; } = "";    // 會員姓名（或收件人）
    public decimal Total { get; set; }              // 訂單金額（含或不含運費，依你 Service 投影）
    public string PaymentStatus { get; set; } = ""; // Paid / Unpaid / Refunded / Failed ...
    public string OrderStatus { get; set; } = "";   // Pending / Shipping / Completed / Canceled ...
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }        // 第一筆出貨時間（可為 null）
    
    // 新增：賣家訂單摘要
    public List<VendorOrderSummary> VendorSummary { get; set; } = new List<VendorOrderSummary>();
}

/// <summary>
/// 賣家訂單摘要
/// </summary>
public class VendorOrderSummary
{
    public int SellerId { get; set; }
    public string VendorName { get; set; } = "";
    public string OrderStatus { get; set; } = "";
    public decimal Amount { get; set; }
    public int ItemCount { get; set; }
}
