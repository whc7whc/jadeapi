using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// 更新出貨狀態請求 DTO
    /// </summary>
    public class UpdateShippingStatusRequestDto
    {
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } // shipped, delivered

        public int VendorId { get; set; }

        [MaxLength(100)]
        public string? TrackingNumber { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
