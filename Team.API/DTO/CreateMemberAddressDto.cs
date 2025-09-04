using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    // 新增地址用的 DTO
    public class CreateMemberAddressDto
    {
        [Required(ErrorMessage = "收件人姓名為必填")]
        [MaxLength(50, ErrorMessage = "收件人姓名不可超過50字")]
        public string RecipientName { get; set; }

        [Required(ErrorMessage = "電話號碼為必填")]
        [Phone(ErrorMessage = "電話號碼格式不正確")]
        [MaxLength(20, ErrorMessage = "電話號碼不可超過20字")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "城市為必填")]
        [MaxLength(10, ErrorMessage = "城市名稱不可超過10字")]
        public string City { get; set; }

        [Required(ErrorMessage = "區域為必填")]
        [MaxLength(10, ErrorMessage = "區域名稱不可超過10字")]
        public string District { get; set; }

        [Required(ErrorMessage = "郵遞區號為必填")]
        [MaxLength(10, ErrorMessage = "郵遞區號不可超過10字")]
        public string ZipCode { get; set; }

        [Required(ErrorMessage = "街道地址為必填")]
        [MaxLength(200, ErrorMessage = "街道地址不可超過200字")]
        public string StreetAddress { get; set; }

        public bool IsDefault { get; set; } = false;
    }
}