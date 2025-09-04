using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
      public class SetDefaultAddressDto
    {
        [Required(ErrorMessage = "地址ID為必填")]
        public int AddressId { get; set; }
    }
}

