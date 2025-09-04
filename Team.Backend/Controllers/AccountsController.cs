using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
//新增
//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using System.Security.Claims;


namespace Team.Backend.Controllers  // <-- 建議加命名空間包起來
{
    public class AccountController : Controller
    {

        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AccountController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            //查詢符合輸入 Email，並且 IsActive 為 true

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email && u.IsActive == true);

            if (user == null)
            {
                ModelState.AddModelError(nameof(model.Password), "帳號或密碼錯誤");
                return View(model);
            }

            // 使用 PasswordHasher 驗證密碼 

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            // 如果驗證失敗
            if (result != PasswordVerificationResult.Success)
            {
                ModelState.AddModelError("", "帳號或密碼錯誤");
                return View(model);
            }

            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetInt32("RoleId", user.RoleId);

            //更新使用者最後登入時間 user.LastLoginAt = DateTime.Now;

            if (user.RoleId == 1)
            {
                return RedirectToAction("Dashboard", "Account");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult Dashboard()
        {
            return RedirectToAction("Index", "Home");
        }


    }
}


