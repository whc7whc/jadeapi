using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels
{
    public class EditAdminViewModel
    {
        public int UserId { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        public int Role { get; set; } // 1=超管, 2=一般

        public bool IsActive { get; set; }

        public string RoleName => Role == 1 ? "超級管理員" : "一般管理員";
    }

}
