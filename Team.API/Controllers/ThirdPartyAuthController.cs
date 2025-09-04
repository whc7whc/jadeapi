using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.DTO;
using Team.API.Models.EfModel;


namespace Team.API.Controllers
{
    [Route("api/third-party-auth")]
    [ApiController]
    public class ThirdPartyAuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly JwtService _jwtService;

        public ThirdPartyAuthController(AppDbContext context, IConfiguration config, JwtService jwtService)
        {
            _context = context;
            _config = config;
            _jwtService = jwtService;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto dto)
        {
            if (string.IsNullOrEmpty(dto.IdToken))
            {
                return BadRequest("缺少 IdToken");
            }

            try
            {
                var clientId = _config["Google:ClientId"];
                if (string.IsNullOrEmpty(clientId))
                {
                    return StatusCode(500, "後端未設定 Google:ClientId");
                }

                // 🔍 印出接收到的 token
                Console.WriteLine("收到 idToken: " + dto.IdToken);

                // 🔐 驗證 token
                var payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });

                Console.WriteLine($"驗證成功！Email: {payload.Email}, Sub: {payload.Subject}");

                var email = payload.Email;
                var name = payload.Name ?? "Google 使用者";
                var providerUserId = payload.Subject;

                var socialLogin = await _context.SocialLogins
                    .FirstOrDefaultAsync(s => s.Provider == "google" && s.ProviderUserId == providerUserId);

                Member member;

                if (socialLogin != null)
                {
                    member = await _context.Members.FindAsync(socialLogin.MembersId);
                }
                else
                {
                    member = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);
                    if (member != null && member.RegisteredVia == "email")
                    {
                        return BadRequest("此 Email 已被註冊，請使用密碼登入。");
                    }
                    {
                        member = new Member
                        {
                            Email = email,
                            PasswordHash = null,
                            RegisteredVia = "google",
                            IsEmailVerified = true,
                            IsActive = true,
                            Level = 1,
                            Role = false,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.Members.Add(member);
                        await _context.SaveChangesAsync();

                        var profile = new MemberProfile
                        {
                            MembersId = member.Id,
                            Name = name,
                            Gender = "",
                            BirthDate = DateOnly.FromDateTime(DateTime.Parse("1900-01-01")),
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.MemberProfiles.Add(profile);
                        await _context.SaveChangesAsync();
                    }

                    var existingSocial = await _context.SocialLogins
             .FirstOrDefaultAsync(s => s.Provider == "google" && s.ProviderUserId == providerUserId);

                    if (existingSocial == null)
                    {
                        var newSocial = new SocialLogin
                        {
                            MembersId = member.Id,
                            Provider = "google",
                            ProviderUserId = providerUserId,
                            CreatedAt = DateTime.Now
                        };
                        _context.SocialLogins.Add(newSocial);
                        await _context.SaveChangesAsync();
                    }

                }

                // ✅ 登入成功後產生 JWT（可選）
                string token = _jwtService.GenerateToken(member);

                return Ok(new
                {
                    message = "登入成功",
                    memberId = member.Id,
                    email = member.Email,
                    token,
                    Role = member.Role,
                    isNewUser = socialLogin == null
                });
            }
            catch (InvalidJwtException ex)
            {
                Console.WriteLine("JWT 驗證失敗：" + ex.ToString());
                return BadRequest("Google Token 驗證失敗：" + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("例外錯誤：" + ex.ToString());
                return StatusCode(500, "系統錯誤：" + ex.Message);
            }
        }
    }
}
