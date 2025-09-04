using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels
{
    public class MemberPostViewModel
    {
        public int Id { get; set; }

        [Display(Name = "標題")]
        public string Title { get; set; }

        [Display(Name = "內容")]
        public string Content { get; set; }

        [Display(Name = "會員ID")]
        public int MembersId { get; set; }

        [Display(Name = "會員名稱")]
        public string MemberName { get; set; }

        [Display(Name = "會員信箱")]
        public string MemberEmail { get; set; }

        [Display(Name = "圖片")]
        public string? Image { get; set; }

        [Display(Name = "狀態")]
        public string Status { get; set; }

        [Display(Name = "建立時間")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "更新時間")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "發布時間")]
        public DateTime? PublishedAt { get; set; }

        [Display(Name = "審核時間")]
        public DateTime? ReviewedAt { get; set; }

        [Display(Name = "退回原因")]
        public string? RejectedReason { get; set; }
    }
}
