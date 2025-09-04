namespace Team.API.DTO
{
    /// <summary>
    /// 購物車結帳請求
    /// </summary>
    public class CartCheckoutRequest
    {
        /// <summary>
        /// 付款方式 (credit: 信用卡, transfer: 轉帳, cod: 貨到付款)
        /// </summary>
        public string? PaymentMethod { get; set; }
        
        /// <summary>
        /// 總金額
        /// </summary>
        public int TotalAmount { get; set; }
        
        /// <summary>
        /// 商品名稱
        /// </summary>
        public string? ItemName { get; set; }
        
        /// <summary>
        /// 會員ID (選填)
        /// </summary>
        public int? MemberId { get; set; }
        
        /// <summary>
        /// 收件地址 (選填)
        /// </summary>
        public string? ShippingAddress { get; set; }
        
        /// <summary>
        /// 備註 (選填)
        /// </summary>
        public string? Note { get; set; }
    }
}
