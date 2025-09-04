namespace Team.Backend.Models.ViewModels.Orders;

/// 清單頁整體 ViewModel（查詢參數 + 清單 + 分頁 + KPI）
public class OrderIndexVm
{
    public OrderQueryVm Query { get; set; } = new();
    public IEnumerable<OrderListItemVm> Items { get; set; } = Enumerable.Empty<OrderListItemVm>();

    public int TotalCount { get; set; }
    public int Page => Query.Page;
    public int PageSize => Query.PageSize;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    // 移除所有統計相關屬性
    // public decimal PageAmountSum => Items.Sum(x => x.Total);

    public bool CanConnect { get; set; }   // 新增：給畫面判斷是否顯示警示

}
