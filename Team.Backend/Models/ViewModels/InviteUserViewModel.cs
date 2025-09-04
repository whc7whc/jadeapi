using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels
{
    public class InviteUserViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
