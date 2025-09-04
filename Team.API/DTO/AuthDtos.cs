namespace Team.API.DTO
{
    public class MemberIdDto
    {
        public int MemberId { get; set; }
    }

    public class NewPasswordDto
    {
        public int MemberId { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
        public string VerificationCode { get; set; }
    }
}
