namespace Team.API.DTO
{
    public class MemberProfileDto
    {
        public string Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public DateTime BirthDate { get; set; }
        public string ProfileImg { get; set; }
        public int Level { get; set; }  // 新增 Level

        public   bool Role { get; set; } // 新增 Role
    }
}
