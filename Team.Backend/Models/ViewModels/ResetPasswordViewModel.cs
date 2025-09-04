using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels
{
    public class ResetPasswordViewModel
    {
        public string Token { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "信箱")]
        public string Email { get; set; }
        [Display(Name = "密碼")]
        [Required(ErrorMessage = "密碼為必填")]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,20}$",
               ErrorMessage = "密碼必須包含8-20個字元，且包含大寫字母、小寫字母、數字及特殊符號")]
        public string Password { get; set; }

        [Required(ErrorMessage = "請再次輸入密碼")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "密碼與確認密碼不符")]
        [Display(Name = "確認密碼")]
        public string ConfirmPassword { get; set; }
    }

}
