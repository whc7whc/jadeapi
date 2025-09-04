using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.DTOs
{
    // 會員等級列表項目 DTO
    public class MembershipLevelListItemDto
    {
        public int Id { get; set; }
        public string LevelName { get; set; }
        public int RequiredAmount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // 新增會員等級 DTO
    public class CreateMembershipLevelDto
    {
        [Required(ErrorMessage = "等級名稱不能為空")]
        [MaxLength(50, ErrorMessage = "等級名稱長度不能超過 50 字元")]
        public string LevelName { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "需要的金額必須為正數")]
        public int RequiredAmount { get; set; }



        public bool IsActive { get; set; } = true;
    }

    // 更新會員等級 DTO
    public class UpdateMembershipLevelDto
    {
        [Required(ErrorMessage = "等級名稱不能為空")]
        [MaxLength(50, ErrorMessage = "等級名稱長度不能超過 50 字元")]
        public string LevelName { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "需要的金額必須為正數")]
        public int RequiredAmount { get; set; }



        public bool IsActive { get; set; } = true;
    }

}