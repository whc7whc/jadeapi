namespace Team.API.DTO
{
    public class ApplySellerDto
    {
        // 基本資料
        public required string RealName { get; set; }
        public string IdNumber { get; set; }

        // 銀行帳戶
        public required string BankName { get; set; }
        public required string BankCode { get; set; }
        public required string AccountName { get; set; }
        public required string AccountNumber { get; set; }

        // 退貨資料
        public required string ContactName { get; set; }
        public required string ContactPhone { get; set; }
        public required string ReturnAddress { get; set; }
        public required string City { get; set; }
        public required string District { get; set; }
        public required string ZipCode { get; set; }

        // 證件圖片
        public IFormFile frontDoc { get; set; }
        public IFormFile backDoc { get; set; }
        public IFormFile BankPhoto { get; set; }
    }
}
