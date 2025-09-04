using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    public class PostStatusDto
    {
        [Required(ErrorMessage = "狀態為必填欄位")]
        [RegularExpression("^(draft|pending|published|rejected)$", ErrorMessage = "狀態值無效")]
        public string Status { get; set; } = "draft";

        public string? Reason { get; set; }
    }
}
