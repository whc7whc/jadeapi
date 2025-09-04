using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Team.Backend.Models.EfModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Team.Backend.Controllers
{
    public class AccountManageController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccountManageController> _logger;

        public AccountManageController(AppDbContext context, ILogger<AccountManageController> logger)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> MemberInfo(string? search)
        {
            var query = _context.Members
                .Include(m => m.Profile)
                .Include(m => m.MemberAddresses)
                .Include(m => m.Sessions)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowered = search.ToLower().Trim();

                // 處理中文性別輸入
                if (lowered == "男") lowered = "male";
                else if (lowered == "女") lowered = "female";
                else if (lowered == "其他") lowered = "other";

                query = query.Where(m =>
                    m.Email.ToLower().Contains(lowered) ||
                    m.Profile.Name.ToLower().Contains(lowered) ||
                    m.Profile.MemberAccount.ToLower().Contains(lowered) ||
                    m.Profile.Gender.ToLower().Contains(lowered) ||
                    m.RegisteredVia.ToLower().Contains(lowered) ||                        // 註冊方式
                    (m.IsActive ? "啟用" : "停用").Contains(search) ||                    // 狀態（用原始 search 保留中文）
                    (m.Role ? "賣家" : "一般會員").Contains(search) ||                    // 角色
           m.Level.ToString().Contains(search) ||
(m.Level == 1 ? "銅" : m.Level == 2 ? "銀" : m.Level == 3 ? "金" : "未知").Contains(search)
                );
            }

            var members = await query.ToListAsync();

            var viewModels = members.Select(member => new MemberFullViewModel
            {
                Member = member,
                Profile = member.Profile!,
                Addresses = member.MemberAddresses.ToList(),
                Sessions = member.Sessions.ToList()
            }).ToList();

            ViewBag.CurrentSearch = search;

            return View(viewModels);
        }



        public async Task<IActionResult> MemberDetail(int id)
        {
            var member = await _context.Members
                .Include(m => m.Profile)
                .Include(m => m.MemberAddresses)
                .Include(m => m.Sessions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (member == null)
            {
                return NotFound();
            }

            var viewModel = new MemberFullViewModel
            {
                Member = member,
                Profile = member.Profile!,
                Addresses = member.MemberAddresses.ToList(),
                Sessions = member.Sessions.ToList()
            };

            return PartialView("_MemberDetailPartial", viewModel);
        }
        // GET 編輯會員的 PartialView
        public async Task<IActionResult> EditPartial(int id)
        {
            var member = await _context.Members
                .Include(m => m.Profile)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (member == null)
            {
                return NotFound();
            }

            var viewModel = new MemberFullViewModel
            {
                Member = member,
                Profile = member.Profile!,
                Addresses = new List<MemberAddress>(),

                Sessions = new List<Session>()
            };
            ViewBag.GenderOptions = new List<SelectListItem>
    {
        new SelectListItem { Value = "male", Text = "男" },
        new SelectListItem { Value = "female", Text = "女" },
        new SelectListItem { Value = "other", Text = "其他" }
    };

            return PartialView("_EditMemberPartial", viewModel);
        }

        // POST 編輯會員，接收 AJAX 表單送出
        [HttpPost]
        public async Task<IActionResult> EditPartial(MemberFullViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // 驗證錯誤，回傳部分 View，讓前端顯示錯誤訊息
                return PartialView("_EditMemberPartial", model);
            }

            var memberToUpdate = await _context.Members
                .Include(m => m.Profile)
                .FirstOrDefaultAsync(m => m.Id == model.Member.Id);

            if (memberToUpdate == null)
            {
                return NotFound();
            }


            // 更新會員資料（依照前端順序）
            memberToUpdate.Profile.Name = model.Profile.Name;
            memberToUpdate.Profile.MemberAccount = model.Profile.MemberAccount;
            memberToUpdate.Profile.BirthDate = model.Profile.BirthDate;
            memberToUpdate.Profile.Gender = model.Profile.Gender;

            memberToUpdate.IsActive = model.Member.IsActive;
            memberToUpdate.Level = model.Member.Level;
            memberToUpdate.Role = model.Member.Role;
            memberToUpdate.Email = model.Member.Email;

            await _context.SaveChangesAsync();

            // 編輯成功後用 JSON 回傳結果
            return Json(new { success = true });
        }
    }

}