using Team.Backend.Models.EfModel;


namespace Team.Backend.Models.ViewModels
{
    public class AdminFullViewModel
    {
        public int UserId { get; set; }
        public required string UserEmail { get; set; }  // 管理員的 Email
        public int RoleId { get; set; }  // 管理員的 RoleId
        public bool UserIsActive { get; set; }  // 管理員是否啟用
        public required string UserLastLoginAt { get; set; }  // 最後登入時間（轉為字串）
        public required string RoleName { get; set; }  // 角色名稱
        public required string UserDescription { get; set; }  // 角色描述



    }
}
