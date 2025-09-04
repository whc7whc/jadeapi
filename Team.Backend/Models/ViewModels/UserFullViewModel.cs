using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.ViewModels
{
    public class UserFullViewModel
    {
        // 來自 Users 資料表
        public int UserId { get; set; }
        public required string UserEmail { get; set; }

        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // 來自 User_Roles 資料表
        public int RoleId { get; set; }
        public required string RoleName { get; set; }
        public required string RoleDescription { get; set; }

        // 來自 AdminInvitations 資料表
        public int? InvitationId { get; set; }
        public string? InvitationToken { get; set; }
        public bool? InvitationUsed { get; set; }
        public DateTime? InvitationExpiresAt { get; set; }

        // 顯示用途
        public string LastLoginText => LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "尚未登入";
        public string StatusText => IsActive ? "啟用" : "停用";
    }

}

