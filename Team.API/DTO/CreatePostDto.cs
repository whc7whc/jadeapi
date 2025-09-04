using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    public class CreatePostDto
    {
        [Required(ErrorMessage = "標題為必填欄位")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "標題長度必須在 2-100 字元之間")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "內容為必填欄位")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "內容長度必須在 10-2000 字元之間")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "會員ID為必填欄位")]
        [Range(1, int.MaxValue, ErrorMessage = "會員ID必須大於 0")]
        public int MembersId { get; set; }

        [RegularExpression("^(draft|pending|published|rejected)$", ErrorMessage = "狀態值無效")]
        public string? Status { get; set; } = "draft";
    }
}