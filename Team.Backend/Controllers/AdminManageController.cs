using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Team.Backend.Services;
using Team.Backend.Controllers;

public class AdminManageController : BaseController
{
    private readonly AppDbContext _context;
    private readonly IUserEmailSender _emailSender;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILogger<AdminManageController> _logger;


    public AdminManageController(AppDbContext context, IUserEmailSender emailSender, IPasswordHasher<User> passwordHasher, ILogger<AdminManageController> logger)
        : base(context, logger)
    {
        _context = context;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // 檢視所有管理員
    public async Task<IActionResult> AdminList(string email, string role, string active)
    {
        var query = _context.Users.Include(u => u.Role).AsQueryable();


        if (!string.IsNullOrEmpty(email))
            query = query.Where(u => u.Email.Contains(email));

        if (!string.IsNullOrEmpty(role))
        {
            if (int.TryParse(role, out int roleId))
            {
                query = query.Where(u => u.RoleId == roleId);
            }
        }

        if (!string.IsNullOrEmpty(active))
        {
            bool isActive = bool.Parse(active);
            query = query.Where(u => u.IsActive == isActive);
        }

        var admins = await query.ToListAsync();

        var viewModels = admins.Select(admin => new AdminFullViewModel
        {
            UserId = admin.Id,
            UserEmail = admin.Email,
            RoleId = admin.RoleId,
            UserIsActive = admin.IsActive,
            UserLastLoginAt = admin.LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未登入",
            RoleName = admin.Role.RoleName,
            UserDescription = admin.Role.Description
        }).ToList();

        return View(viewModels);
    }
    [HttpGet]
    public async Task<IActionResult> AdminDetail(int id)
    {

        if (id == 0)
        {
            return BadRequest("❌ 無效的管理員 ID");
        }

        var admin = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (admin == null)
        {
            return NotFound("❌ 找不到這位管理員");
        }

        var viewModel = new AdminFullViewModel
        {
            UserEmail = admin.Email,
            RoleId = admin.RoleId,
            UserIsActive = admin.IsActive,
            UserLastLoginAt = admin.LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未登入",
            RoleName = admin.Role?.RoleName ?? "未知",
            UserDescription = admin.Role?.Description ?? "無描述",
            UserId = admin.Id
        };

        // 確保有這個 View: Views/AdminManage/_AdminDetailPartial.cshtml
        return PartialView("_AdminDetailPartial", viewModel);
    }
    [HttpPost]
    public async Task<IActionResult> AddAdmin(string email, int role)
    {
        Console.WriteLine($"AddAdmin called with email={email}, role={role}");

        if (string.IsNullOrEmpty(email) || role == 0)
            return Json(new { success = false, message = "請填寫完整資料" });

        // 1. 檢查該 Email 是否已經有註冊過使用者
        var userExists = await _context.Users.AnyAsync(u => u.Email == email);
        Console.WriteLine($"User exists: {userExists}");
        if (userExists)
            return Json(new { success = false, message = "此 Email 已存在" });

        // 2. 檢查該 Email 是否已經有有效的管理員邀請尚未使用（避免重複邀請）
        var invitationExists = await _context.AdminInvitations.AnyAsync(i => i.Email == email && i.ExpiresAt > DateTime.Now);
        Console.WriteLine($"Invitation exists and valid: {invitationExists}");
        if (invitationExists)
            return Json(new { success = false, message = "此 Email 已申請成為管理員，請檢查您的信箱" });

        // 3. 新增使用者
        var user = new User
        {
            Email = email,
            RoleId = role,
            IsActive = true,
            MustSetPassword = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 4. 產生邀請 Token
        var token = Guid.NewGuid().ToString();

        // TODO: 改成動態抓登入者ID
        var currentUserId = 1;

        var invitation = new AdminInvitation
        {
            Email = email,
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.Now.AddDays(2),
            CreatedBy = currentUserId,
            CreatedAt = DateTime.Now
        };
        _context.AdminInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        // 5. 產生註冊連結
        var registerUrl = Url.Action("Register", "AdminManage", new { token }, Request.Scheme);
        if (string.IsNullOrEmpty(registerUrl))
            return Json(new { success = false, message = "邀請連結產生失敗" });

        var emailBody = $@"
    <h3>您被邀請成為管理員</h3>
    <p>請點擊以下連結完成註冊並設定密碼：</p>
    <a href='{registerUrl}'>點此註冊</a>
    <p>連結一小時內有效</p>
    ";

        try
        {
            await _emailSender.SendEmailAsync(email, "管理員邀請註冊通知", emailBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine("邀請信發送失敗：" + ex.Message);
            return Json(new { success = false, message = "邀請信發送失敗: " + ex.Message });
        }

        return Json(new { success = true, message = "邀請信已發送" });
    }

    [HttpGet]
    public async Task<IActionResult> Register(string token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("無效的邀請連結");

        var invitation = await _context.AdminInvitations.FirstOrDefaultAsync(i => i.Token == token && !i.IsUsed && i.ExpiresAt > DateTime.Now);
        if (invitation == null)
            return BadRequest("邀請連結無效或已過期");

        var vm = new RegisterViewModel { Token = token, Email = invitation.Email };
        return View(vm);
    }
    private string PasswordHash(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var invitation = await _context.AdminInvitations.FirstOrDefaultAsync(i => i.Token == model.Token && !i.IsUsed && i.ExpiresAt > DateTime.Now);
        if (invitation == null)
        {
            ModelState.AddModelError("", "邀請連結無效或已過期");
            return View(model);
        }

        var user = await _context.Users.FindAsync(invitation.UserId);
        if (user == null)
        {
            ModelState.AddModelError("", "找不到使用者");
            return View(model);
        }

        // 使用注入的密碼雜湊器
        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
        user.MustSetPassword = false;
        user.UpdatedAt = DateTime.Now;

        invitation.IsUsed = true;

        await _context.SaveChangesAsync();

        return RedirectToAction("Login", "Account");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendPasswordInvite(int userId)
    {
        if (userId == 0)
            return Json(new { success = false, message = "無效的使用者 ID" });

        // 找使用者
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Json(new { success = false, message = "找不到使用者" });

        // 檢查是否有未使用且有效的邀請
        var existingInvitation = await _context.AdminInvitations
            .FirstOrDefaultAsync(i => i.UserId == userId && !i.IsUsed && i.ExpiresAt > DateTime.Now);

        string token;
        if (existingInvitation != null)
        {
            token = existingInvitation.Token;
        }
        else
        {
            // 產生新 Token
            token = Guid.NewGuid().ToString();

            // TODO: 改成動態抓登入者ID
            var currentUserId = 1;

            var invitation = new AdminInvitation
            {
                Email = user.Email,
                UserId = userId,
                Token = token,
                ExpiresAt = DateTime.Now.AddDays(2),
                CreatedBy = currentUserId,
                CreatedAt = DateTime.Now
            };
            _context.AdminInvitations.Add(invitation);
            await _context.SaveChangesAsync();
        }

        // 產生邀請連結（假設是 Register action）
        var inviteUrl = Url.Action("Register", "AdminManage", new { token }, Request.Scheme);

        var emailBody = $@"
        <h3>您被重新邀請成為管理員</h3>
        <p>請點擊以下連結完成設定新密碼：</p>
        <a href='{inviteUrl}'>點此設定密碼</a>
        <p>連結有效期限為兩天</p>
    ";

        try
        {
            await _emailSender.SendEmailAsync(user.Email, "管理員密碼重設邀請", emailBody);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "邀請信發送失敗：" + ex.Message });
        }

        return Json(new { success = true, message = "密碼重設邀請已寄出" });
    }



}



