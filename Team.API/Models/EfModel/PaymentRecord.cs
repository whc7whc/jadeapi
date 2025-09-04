namespace Team.API.Models.EfModel
{
    public partial class PaymentRecord
    {
        public long Id { get; set; }
        public string MerchantTradeNo { get; set; } = default!;
        public string? TradeNo { get; set; }
        public int TradeAmt { get; set; }
        public int RtnCode { get; set; }
        public string? RtnMsg { get; set; }
        public string? PaymentType { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal? PaymentTypeChargeFee { get; set; }
        public DateTime? TradeDate { get; set; }
        public bool? SimulatePaid { get; set; }
        public int? OrderId { get; set; }
        public int? MemberId { get; set; }
        public string? RawReturn { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
