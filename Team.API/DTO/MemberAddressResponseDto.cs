namespace Team.API.DTO
{
    public class MemberAddressResponseDto
    {
        public int Id { get; set; }
        public int MembersId { get; set; }
        public string RecipientName { get; set; }
        public string PhoneNumber { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string ZipCode { get; set; }
        public string StreetAddress { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 完整地址 (組合用)
        public string FullAddress => $"{City}{District}{StreetAddress}";
    }

}
