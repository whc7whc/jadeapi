namespace Team.API.Payments
{
    public class EcpayOptions
    {
        public string MerchantID { get; set; } = default!;
        public string HashKey { get; set; } = default!;
        public string HashIV { get; set; } = default!;
        public string AioCheckOutUrl { get; set; } = default!;
        public string ReturnURL { get; set; } = default!;
        public string? OrderResultURL { get; set; }
    }
}
