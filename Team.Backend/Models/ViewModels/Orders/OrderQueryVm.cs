using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.ViewModels.Orders;

/// 清單頁的查詢參數（由 QueryString / 表單綁進來）
public class OrderQueryVm
{
    public string? Q { get; set; }                  // 關鍵字（訂單編號/姓名/電話）
    public string? Status { get; set; }             // 套用到 OrderStatus / PaymentStatus
    public DateTime? DateFrom { get; set; }         // 建立日期（起）
    public DateTime? DateTo { get; set; }           // 建立日期（迄）
    public int Page { get; set; } = 1;              // 第幾頁
    public int PageSize { get; set; } = 10;         // 每頁筆數
    public string? PaymentStatus { get; set; } // 付款狀態（可選，若有則套用到 PaymentStatus）
    public string? OrderStatus { get; set; } // 訂單狀態（可選，若有則套用到 OrderStatus）
    public string? SortBy { get; set; }             // 排序欄位
    public string? SortDirection { get; set; }      // 排序方向（asc/desc）
}
