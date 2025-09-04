
using System.ComponentModel.DataAnnotations;
namespace Team.Backend.Models.ViewModels;


public class LoginViewModel
{

        [Required(ErrorMessage = "請輸入信箱")]
    [EmailAddress(ErrorMessage = "請輸入有效的信箱格式")]
    [Display(Name = "信箱")]
    public string Email { get; set; } = string.Empty;
    
    [Display(Name = "密碼")]
    [Required(ErrorMessage = "請輸入密碼")]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,20}$",
        ErrorMessage = "密碼必須包含8-20個字元，且包含大寫字母、小寫字母、數字及特殊符號")]
    public string Password { get; set; } = string.Empty;
}

