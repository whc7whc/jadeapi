using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels
{
    public class CreatePopupBannerViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "廣告標題為必填項")]
        [StringLength(100, ErrorMessage = "標題不能超過100個字元")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "廣告內容為必填項")]
        [StringLength(500, ErrorMessage = "內容不能超過500個字元")]
        public string Description { get; set; } = string.Empty;

        // 🔥 改為 nullable，與 Banner 模型一致
        [Url(ErrorMessage = "請輸入有效的圖片網址")]
        public string? ImageUrl { get; set; }

        [Url(ErrorMessage = "請輸入有效的連結網址")]
        public string? LinkUrl { get; set; }

        [Required(ErrorMessage = "請選擇觸發頁面")]
        public string Page { get; set; } = "home";

        [Required(ErrorMessage = "請選擇彈窗類型")]
        public string Position { get; set; } = "popup";

        public bool IsActive { get; set; } = true;

        [Range(1, 999, ErrorMessage = "顯示順序必須在1-999之間")]
        public int DisplayOrder { get; set; } = 1;

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        // 用於編輯時判斷
        public bool IsEdit => Id > 0;
    }

}
