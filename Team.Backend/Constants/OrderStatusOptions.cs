namespace Team.Backend.Constants;

// 訂單狀態
public static class OrderStatusOptions
{
    public static readonly string[] All = ["Pending", "Processing", "Shipping", "Completed", "Canceled"];
    public static readonly HashSet<string> Set = new(All, StringComparer.OrdinalIgnoreCase);

    // 顯示文字
    private static readonly Dictionary<string, string> _text = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pending"] = "待處理",
        ["Processing"] = "處理中",
        ["Shipping"] = "配送中",
        ["Completed"] = "已完成",
        ["Canceled"] = "已取消"
    };

    public static string Text(string? code) =>
        code != null && _text.TryGetValue(code.Trim(), out var zh) ? zh : (code ?? "");
}

// 付款狀態
public static class PaymentStatusOptions
{
    public static readonly string[] All = ["Paid", "Unpaid", "Failed", "Refunded"];
    public static readonly HashSet<string> Set = new(All, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> _text = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Paid"] = "已付款",
        ["Unpaid"] = "未付款",
        ["Failed"] = "付款失敗",
        ["Refunded"] = "已退款"
    };

    public static string Text(string? code) =>
        code != null && _text.TryGetValue(code.Trim(), out var zh) ? zh : (code ?? "");
}
