using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Team.API.DTO;
using Team.API.Models.EfModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;


namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly Cloudinary _cloudinary;
        private readonly JwtService _jwtService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext context, IEmailService emailService, Cloudinary cloudinary, JwtService jwtService, ILogger<AuthController> logger)
        {
            _context = context;
            _emailService = emailService;
            _cloudinary = cloudinary;
            _jwtService = jwtService;
            _logger = logger;
        }
        // 1. 寄送驗證碼
        [HttpPost("send-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] EmailDto dto)
        {
            // Email 格式檢查
            if (!new EmailAddressAttribute().IsValid(dto.Email))
                return BadRequest("Email 格式不正確");

            // 檢查是否已註冊
            if (_context.Members.Any(m => m.Email == dto.Email))
                return BadRequest("Email 已被使用");

            // 防濫用：檢查 1 分鐘內是否已寄送
            var recentCode = await _context.VerificationCodes
                .Where(v => v.ContactInfo == dto.Email && v.CreatedAt > DateTime.Now.AddMinutes(-1))
                .FirstOrDefaultAsync();

            if (recentCode != null)
                return BadRequest("請稍後再試，1 分鐘內只能寄送一次驗證碼");

            // 產生驗證碼並寄信
            await GenerateAndSendVerificationCode(null, dto.Email);

            return Ok("驗證碼已寄出");
        }

        // 2. 註冊（驗證碼 + 密碼）
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterWithCodeDto dto)
        {
            // ✅ 檢查密碼一致性
            if (dto.Password != dto.ConfirmPassword)
                return BadRequest("密碼與確認密碼不一致");

            // 檢查是否已註冊
            if (_context.Members.Any(m => m.Email == dto.Email))
                return BadRequest("Email 已被註冊");

            // 驗證驗證碼
            var codeRecord = await _context.VerificationCodes
                .Where(v => v.ContactInfo == dto.Email && v.Code == dto.Code && !v.IsUsed && v.ExpiresAt > DateTime.Now)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (codeRecord == null)
                return BadRequest("驗證碼無效或已過期");

            // 建立會員
            var member = new Member
            {
                Email = dto.Email,
                PasswordHash = HashPassword(dto.Password),
                RegisteredVia = "email",

                IsEmailVerified = true,
                IsActive = true,
                Level = 1,
                Role = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync(); // 先保存會員以獲得ID

            // 創建對應的MemberStat記錄
            var memberStat = new MemberStat
            {
                MemberId = member.Id,
                CurrentLevelId = 1, // 預設銅牌會員
                TotalSpent = 0,
                TotalPoints = 0,
                UpdatedAt = DateTime.Now
            };
            _context.MemberStats.Add(memberStat);

            // 驗證碼標記為已使用
            codeRecord.IsUsed = true;
            await _context.SaveChangesAsync();
            // 修改這裡，回傳與登入 API 一致的資料結構
            return Ok(new
            {
                Message = "註冊成功",
                MemberId = member.Id,
                Email = member.Email,
                Role = member.Role
            });
        }


        // 建立及修改會員資料
        [HttpPost("{memberId}/profile")]
        public async Task<IActionResult> UpdateProfile(int memberId, [FromForm] UpdateProfileWithFileDto dto)
        {
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            if (!member.IsEmailVerified)
                return BadRequest("請先完成 Email 驗證");

            // 計算年齡
            var birthDate = dto.BirthDate;
            var age = DateTime.Today.Year - birthDate.Year;
            if (birthDate > DateTime.Today.AddYears(-age)) age--;
            if (age < 12)
                return BadRequest("申辦資格需年滿12歲");

            // 找出或建立會員資料
            var profile = await _context.MemberProfiles.FirstOrDefaultAsync(p => p.MembersId == memberId);
            if (profile == null)
            {
                profile = new MemberProfile
                {
                    MembersId = memberId,
                    CreatedAt = DateTime.Now
                };
                _context.MemberProfiles.Add(profile);
            }

            profile.Name = dto.Name;
            profile.Gender = dto.Gender;
            profile.BirthDate = DateOnly.FromDateTime(dto.BirthDate);
            profile.UpdatedAt = DateTime.Now;

            // ✅ 儲存圖片
            if (dto.ProfileImgFile != null && dto.ProfileImgFile.Length > 0)
            {
                var ext = Path.GetExtension(dto.ProfileImgFile.FileName).ToLower();
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic" };
                if (!allowedExts.Contains(ext))
                    return BadRequest("只允許上傳 JPG、PNG、GIF 圖片");

                if (dto.ProfileImgFile.Length > 2 * 1024 * 1024)
                    return BadRequest("圖片不能超過 2MB");

                using var stream = dto.ProfileImgFile.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(dto.ProfileImgFile.FileName, stream),
                    Transformation = new Transformation().Width(300).Height(300).Crop("fill").Quality("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    profile.ProfileImg = uploadResult.SecureUrl.ToString();
                }
                else
                {
                    return StatusCode(500, "圖片上傳 Cloudinary 失敗");
                }
            }

            await _context.SaveChangesAsync();
            return Ok("會員資料更新成功");
        }


        //檢視會員資料
        // GET: api/Auth/{memberId}/profile
        [HttpGet("{memberId}/profile")]
        public async Task<IActionResult> GetProfile(int memberId)
        {
            try
            {
                _logger.LogInformation("開始查詢會員 {MemberId} 的資料", memberId);

                // 使用不變文化進行資料庫查詢
                var originalCulture = CultureInfo.CurrentCulture;
                var originalUICulture = CultureInfo.CurrentUICulture;

                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

                    var member = await _context.Members.FindAsync(memberId);
                    if (member == null)
                    {
                        _logger.LogWarning("找不到會員 {MemberId}", memberId);
                        return NotFound("找不到會員");
                    }

                    var profile = await _context.MemberProfiles
                        .FirstOrDefaultAsync(p => p.MembersId == memberId);

                    if (profile == null)
                    {
                        _logger.LogWarning("找不到會員 {MemberId} 的資料", memberId);
                        return NotFound("找不到會員資料");
                    }

                    // 安全地轉換 BirthDate，避免文化相關問題
                    DateTime birthDateTime;
                    try
                    {
                        birthDateTime = profile.BirthDate.ToDateTime(TimeOnly.MinValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "轉換生日日期時發生錯誤，使用預設值");
                        birthDateTime = DateTime.MinValue;
                    }

                    // 回傳前端需要的資料，可以用 DTO 包裝
                    var result = new MemberProfileDto
                    {
                        Email = member.Email ?? "",
                        IsEmailVerified = member.IsEmailVerified,
                        Name = profile.Name ?? "",
                        Gender = profile.Gender ?? "",
                        BirthDate = birthDateTime,
                        ProfileImg = profile.ProfileImg ?? "",
                        Level = member.Level
                    };

                    _logger.LogInformation("成功取得會員 {MemberId} 的資料", memberId);
                    return Ok(result);
                }
                finally
                {
                    // 恢復原始文化設定
                    try
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUICulture;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "恢復文化設定時發生錯誤");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢會員 {MemberId} 資料時發生錯誤", memberId);
                return StatusCode(500, new { error = "查詢會員資料時發生錯誤", detail = ex.Message });
            }
        }


        // --- 輔助方法 ---
        private async Task GenerateAndSendVerificationCode(int? memberId, string email)
        {
            var code = new Random().Next(100000, 999999).ToString();

            var verificationCode = new VerificationCode
            {
                MembersId = memberId,
                ContactInfo = email,
                Type = "email_verification",
                Code = code,
                ExpiresAt = DateTime.Now.AddMinutes(30),
                IsUsed = false,
                CreatedAt = DateTime.Now
            };

            _context.VerificationCodes.Add(verificationCode);
            await _context.SaveChangesAsync();

            string subject = "【驗證碼通知】請完成您的 Email 驗證";
            string body = $"您的驗證碼是：{code}\n請在 30 分鐘內完成驗證。";

            await _emailService.SendEmailAsync(email, subject, body);
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        //登入
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == dto.Email);

            if (member == null)
                return Unauthorized("帳號或密碼錯誤");

            var hashedPassword = HashPassword(dto.Password);
            if (member.PasswordHash != hashedPassword)
                return Unauthorized("帳號或密碼錯誤");

            if (!member.IsActive)
                return BadRequest("帳號尚未啟用");

            if (!member.IsEmailVerified)
                return BadRequest("請先完成 Email 驗證");
            // ✅ 使用 JwtService 產生 JWT
            var token = _jwtService.GenerateToken(member);

            return Ok(new
            {
                Message = "登入成功",
                Token = token,
                MemberId = member.Id,
                Email = member.Email,
                Role = member.Role
            });
        }
        //. 寄送重設密碼驗證碼
        [HttpPost("send-reset-code")]
        public async Task<IActionResult> SendPasswordResetCode([FromBody] SendResetPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == dto.Email);
            if (member == null) return BadRequest("找不到此 Email 的帳號");

            // 檢查 1 分鐘內是否已寄送
            var recent = await _context.PasswordResetCodes
                .Where(p => p.ContactInfo == dto.Email && p.CreatedAt > DateTime.Now.AddMinutes(-1))
                .FirstOrDefaultAsync();
            if (recent != null) return BadRequest("請稍後再試，1 分鐘內只能寄送一次");

            var code = new Random().Next(100000, 999999).ToString();
            var resetCode = new PasswordResetCode
            {
                MembersId = member.Id,
                ContactInfo = dto.Email,
                Code = code,
                ExpiresAt = DateTime.Now.AddMinutes(30),
                IsUsed = false,
                CreatedAt = DateTime.Now
            };
            _context.PasswordResetCodes.Add(resetCode);
            await _context.SaveChangesAsync();

            // 寄送 Email
            string subject = "【密碼重設驗證碼】";
            string body = $"您的重設密碼驗證碼為：{code}\n請在 30 分鐘內使用此驗證碼完成密碼重設。";
            await _emailService.SendEmailAsync(dto.Email, subject, body);

            return Ok("驗證碼已寄出");
        }
        //驗證碼 + 修改密碼
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest("新密碼與確認密碼不一致");

            var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == dto.Email);
            if (member == null) return BadRequest("找不到此帳號");

            var resetCode = await _context.PasswordResetCodes
                .Where(p => p.ContactInfo == dto.Email && p.Code == dto.Code && !p.IsUsed && p.ExpiresAt > DateTime.Now)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetCode == null) return BadRequest("驗證碼錯誤或已過期");

            // 更新密碼
            member.PasswordHash = HashPassword(dto.NewPassword);
            member.UpdatedAt = DateTime.Now;

            // 標記驗證碼已使用
            resetCode.IsUsed = true;

            await _context.SaveChangesAsync();
            return Ok("密碼已重設成功");
        }
        // POST: api/Auth/send-password-change-code
        [HttpPost("send-password-change-code")]
        public async Task<IActionResult> SendPasswordChangeCode([FromBody] MemberIdDto dto)
        {
            var member = await _context.Members.FindAsync(dto.MemberId);
            if (member == null)
                return NotFound("找不到該會員");

            if (member.RegisteredVia != "email")
                return BadRequest("非 Email 註冊帳號無法使用此功能");

            // 防濫用：1 分鐘內只寄一次
            var recent = await _context.PasswordResetCodes
                .Where(p => p.ContactInfo == member.Email && p.CreatedAt > DateTime.Now.AddMinutes(-1))
                .FirstOrDefaultAsync();

            if (recent != null)
                return BadRequest("請稍後再試，1 分鐘內只能寄送一次");

            var code = new Random().Next(100000, 999999).ToString();
            var resetCode = new PasswordResetCode
            {
                MembersId = member.Id,
                ContactInfo = member.Email,
                Code = code,
                ExpiresAt = DateTime.Now.AddMinutes(30),
                IsUsed = false,
                CreatedAt = DateTime.Now
            };

            _context.PasswordResetCodes.Add(resetCode);
            await _context.SaveChangesAsync();

            // 寄送 Email 實作
            string subject = "【變更密碼驗證碼】";
            string body = $"您的密碼變更驗證碼為：{code}\n請在 30 分鐘內使用此驗證碼完成密碼變更。";
            await _emailService.SendEmailAsync(member.Email, subject, body);

            return Ok("驗證碼已寄出");
        }

        // POST: api/Auth/set-new-password
        [HttpPost("set-new-password")]
        public async Task<IActionResult> SetNewPassword([FromBody] NewPasswordDto dto)
        {
            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest("新密碼與確認密碼不一致");

            var member = await _context.Members.FindAsync(dto.MemberId);
            if (member == null)
                return NotFound("找不到該帳號");

            if (member.RegisteredVia != "email")
                return BadRequest("非 Email 註冊帳號無法使用此功能");

            var resetCode = await _context.PasswordResetCodes
                .Where(p => p.ContactInfo == member.Email
                            && p.Code == dto.VerificationCode
                            && !p.IsUsed
                            && p.ExpiresAt > DateTime.Now)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetCode == null)
                return BadRequest("驗證碼錯誤或已過期");

            // 更新密碼
            member.PasswordHash = HashPassword(dto.NewPassword);
            member.UpdatedAt = DateTime.Now;

            // 標記驗證碼為使用過
            resetCode.IsUsed = true;
            await _context.SaveChangesAsync();

            return Ok("密碼已變更成功");
        }


    }

}