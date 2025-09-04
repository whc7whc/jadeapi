using System.ComponentModel.DataAnnotations;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.ViewModels
{
    public class OfficialPostViewModel
    {
        public int Id { get; set; }

        [Display(Name = "標題")]
        [Required(ErrorMessage = "請輸入文章標題")]
        public string Title { get; set; } = "";

        [Display(Name = "內容")]
        [Required(ErrorMessage = "請輸入文章內容")]
        public string Content { get; set; } = "";

        [Display(Name = "封面圖片")]
        public string? CoverImage { get; set; }

        [Display(Name = "分類")]
        public string? Category { get; set; }

        [Display(Name = "狀態")]
        public string Status { get; set; } = "draft";

        [Display(Name = "發布時間")]
        public DateTime? PublishedAt { get; set; }

        [Display(Name = "創建者")]
        public int CreatedBy { get; set; } 

        [Display(Name = "創建時間")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新時間")]
        public DateTime? UpdatedAt { get; set; }

        // ✅ 新增排程相關屬性
        public string PublishType { get; set; } // "immediate" 或 "scheduled"
        public DateTime? ScheduledTime { get; set; }

        public List<IFormFile>? UploadedImages { get; set; }
        public List<OfficialPostImageViewModel>? ExistingImages { get; set; }
        public List<string>? UploadedImageUrls { get; set; } = new List<string>();
    }
}