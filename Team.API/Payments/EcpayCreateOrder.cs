namespace Team.API.Payments
{
    public class EcpayCreateOrder
    {
        public string MerchantTradeNo { get; set; } = default!;         // <= 20, 唯一
        public string MerchantTradeDate { get; set; } = default!;       // yyyy/MM/dd HH:mm:ss
        public int TotalAmount { get; set; }
        public string TradeDesc { get; set; } = "shopping";
        public string ItemName { get; set; } = default!;                // 多品項用 # 分隔
        public string ChoosePayment { get; set; } = "ALL";              // or Credit
    }
}
