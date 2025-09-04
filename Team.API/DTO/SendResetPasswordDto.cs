using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    public class SendResetPasswordDto
    {
        [EmailAddress]
        public required string Email { get; set; }
    }
}
