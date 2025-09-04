namespace Team.API.DTO
{
    public class UpdateBankInfoDto
    {
        public string BankName { get; set; }
        public string BankCode { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public IFormFile? BankPhoto { get; set; }
    }
}
