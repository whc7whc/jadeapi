using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VendorAuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<VendorAuthController> _logger;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<Member> _passwordHasher;

        public VendorAuthController(
            AppDbContext context, 
            ILogger<VendorAuthController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<Member>();
        }

        // POST: api/VendorAuth/login
        [HttpPost("login")]
        public async Task<ActionResult<VendorLoginResponseDto>> Login([FromBody] VendorLoginDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(VendorLoginResponseDto.ErrorResult("輸入驗證失敗", errors));
                }

                _logger.LogInformation("廠商登入嘗試: {Email}", dto.Email);

                // 查詢會員資料
                var member = await _context.Members
                    .Include(m => m.Seller)
                        .ThenInclude(s => s.SellerBankAccounts)
                    .Include(m => m.Seller)
                        .ThenInclude(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(m => m.Email == dto.Email && m.IsActive);

                if (member == null)
                {
                    _logger.LogWarning("廠商登入失敗 - 找不到會員: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("帳號或密碼錯誤"));
                }

                // 驗證密碼
                var passwordResult = _passwordHasher.VerifyHashedPassword(member, member.PasswordHash, dto.Password);
                if (passwordResult != PasswordVerificationResult.Success)
                {
                    _logger.LogWarning("廠商登入失敗 - 密碼錯誤: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("帳號或密碼錯誤"));
                }

                // 檢查是否為廠商
                var seller = member.Seller;
                if (seller == null)
                {
                    _logger.LogWarning("廠商登入失敗 - 非廠商帳號: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("此帳號非廠商帳號"));
                }

                // 檢查廠商狀態
                if (!seller.IsActive)
                {
                    _logger.LogWarning("廠商登入失敗 - 帳號已停用: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("廠商帳號已停用"));
                }

                if (seller.ApplicationStatus != "approved")
                {
                    _logger.LogWarning("廠商登入失敗 - 申請未通過: {Email}, Status: {Status}", dto.Email, seller.ApplicationStatus);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult($"廠商申請狀態：{GetStatusLabel(seller.ApplicationStatus)}"));
                }

                // 產生JWT Token
                var token = GenerateJwtToken(member, seller);
                var expiresAt = DateTime.UtcNow.AddDays(7); // Token有效期7天

                // 更新最後登入時間
                member.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                var vendorInfo = seller.ToDto();
                vendorInfo.Email = member.Email;

                _logger.LogInformation("廠商登入成功: {Email}, SellerId: {SellerId}", dto.Email, seller.Id);

                return Ok(VendorLoginResponseDto.SuccessResult(token, seller.Id, vendorInfo, expiresAt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "廠商登入過程發生錯誤: {Email}", dto.Email);
                return StatusCode(500, VendorLoginResponseDto.ErrorResult("登入過程發生錯誤，請稍後再試"));
            }
        }

        // POST: api/VendorAuth/register
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponseDto<object>>> Register([FromBody] VendorRegisterDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                _logger.LogInformation("廠商註冊申請: {Email}", dto.Email);

                // 檢查Email是否已存在
                var existingMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.Email == dto.Email);

                if (existingMember != null)
                {
                    return Conflict(ApiResponseDto<object>.ErrorResult("此Email已被註冊"));
                }

                // 檢查身分證字號是否已存在
                var existingSeller = await _context.Sellers
                    .FirstOrDefaultAsync(s => s.IdNumber == dto.IdNumber);

                if (existingSeller != null)
                {
                    return Conflict(ApiResponseDto<object>.ErrorResult("此身分證字號已被註冊"));
                }

                // 建立會員帳號
                var member = new Member
                {
                    Email = dto.Email,
                    PasswordHash = _passwordHasher.HashPassword(null, dto.Password),
                    IsActive = true,
                    IsEmailVerified = false,
                    RegisteredVia = "direct",
                    Level = 1,
                    Role = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Members.Add(member);
                await _context.SaveChangesAsync();

                // 建立廠商申請
                var seller = new Seller
                {
                    MembersId = member.Id,
                    RealName = dto.RealName,
                    IdNumber = dto.IdNumber,
                    ApplicationStatus = "pending", // 待審核
                    IsActive = false, // 預設不啟用，等審核通過
                    AppliedAt = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Sellers.Add(seller);
                await _context.SaveChangesAsync();

                _logger.LogInformation("廠商註冊申請成功: {Email}, SellerId: {SellerId}", dto.Email, seller.Id);

                return Ok(ApiResponseDto<object>.SuccessResult(
                    new { sellerId = seller.Id },
                    "廠商申請已提交，請等候審核結果"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "廠商註冊過程發生錯誤: {Email}", dto.Email);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("註冊過程發生錯誤，請稍後再試"));
            }
        }

        // GET: api/VendorAuth/profile
        [HttpGet("profile")]
        public async Task<ActionResult<ApiResponseDto<VendorInfoDto>>> GetProfile([FromQuery] int sellerId)
        {
            try
            {
                var seller = await _context.Sellers
                    .Include(s => s.Members)
                    .Include(s => s.SellerBankAccounts)
                    .Include(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(s => s.Id == sellerId);

                if (seller == null)
                {
                    return NotFound(ApiResponseDto<VendorInfoDto>.ErrorResult("找不到廠商資料"));
                }

                var vendorInfo = seller.ToDto();
                vendorInfo.Email = seller.Members?.Email ?? string.Empty;

                return Ok(ApiResponseDto<VendorInfoDto>.SuccessResult(vendorInfo, "獲取廠商資料成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取廠商資料失敗: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<VendorInfoDto>.ErrorResult("獲取廠商資料失敗"));
            }
        }

        // PUT: api/VendorAuth/profile
        [HttpPut("profile")]
        public async Task<ActionResult<ApiResponseDto<VendorInfoDto>>> UpdateProfile(
            [FromQuery] int sellerId, 
            [FromBody] UpdateVendorInfoDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(ApiResponseDto<VendorInfoDto>.ErrorResult("輸入驗證失敗", errors));
                }

                var seller = await _context.Sellers
                    .Include(s => s.Members)
                    .Include(s => s.SellerBankAccounts)
                    .Include(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(s => s.Id == sellerId);

                if (seller == null)
                {
                    return NotFound(ApiResponseDto<VendorInfoDto>.ErrorResult("找不到廠商資料"));
                }

                // 更新基本資料
                seller.RealName = dto.RealName;
                seller.UpdatedAt = DateTime.Now;

                // 更新或建立銀行帳戶資料
                if (!string.IsNullOrEmpty(dto.BankName) || !string.IsNullOrEmpty(dto.AccountNumber))
                {
                    var bankAccount = seller.SellerBankAccounts?.FirstOrDefault();
                    if (bankAccount == null)
                    {
                        bankAccount = new SellerBankAccount 
                        { 
                            SellersId = sellerId,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.SellerBankAccounts.Add(bankAccount);
                    }

                    bankAccount.BankName = dto.BankName ?? bankAccount.BankName;
                    bankAccount.BankCode = dto.BankCode ?? bankAccount.BankCode;
                    bankAccount.AccountName = dto.AccountName ?? bankAccount.AccountName;
                    bankAccount.AccountNumber = dto.AccountNumber ?? bankAccount.AccountNumber;
                    bankAccount.UpdatedAt = DateTime.Now;
                }

                // 更新或建立退貨資料
                if (!string.IsNullOrEmpty(dto.ContactName) || !string.IsNullOrEmpty(dto.ReturnAddress))
                {
                    var returnInfo = seller.SellerReturnInfos?.FirstOrDefault();
                    if (returnInfo == null)
                    {
                        returnInfo = new SellerReturnInfo 
                        { 
                            SellersId = sellerId,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.SellerReturnInfos.Add(returnInfo);
                    }

                    returnInfo.ContactName = dto.ContactName ?? returnInfo.ContactName;
                    returnInfo.ContactPhone = dto.ContactPhone ?? returnInfo.ContactPhone;
                    returnInfo.ReturnAddress = dto.ReturnAddress ?? returnInfo.ReturnAddress;
                    returnInfo.City = dto.City ?? returnInfo.City;
                    returnInfo.District = dto.District ?? returnInfo.District;
                    returnInfo.ZipCode = dto.ZipCode ?? returnInfo.ZipCode;
                    returnInfo.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // 重新載入資料並回傳
                await _context.Entry(seller).ReloadAsync();
                var updatedVendorInfo = seller.ToDto();
                updatedVendorInfo.Email = seller.Members?.Email ?? string.Empty;

                _logger.LogInformation("廠商資料更新成功: SellerId: {SellerId}", sellerId);

                return Ok(ApiResponseDto<VendorInfoDto>.SuccessResult(updatedVendorInfo, "廠商資料更新成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新廠商資料失敗: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<VendorInfoDto>.ErrorResult("更新廠商資料失敗"));
            }
        }

        // POST: api/VendorAuth/logout
        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponseDto<object>>> Logout([FromQuery] int sellerId)
        {
            try
            {
                // 這裡可以加入登出邏輯，如果有使用 refresh token 的話
                // 目前主要是記錄登出事件
                _logger.LogInformation("廠商登出: SellerId: {SellerId}", sellerId);

                return Ok(ApiResponseDto<object>.SuccessResult(null, "登出成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "廠商登出過程發生錯誤: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("登出過程發生錯誤"));
            }
        }

        // 私有方法：產生JWT Token
        private string GenerateJwtToken(Member member, Seller seller)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "your-super-secret-key-for-jwt-token-generation-minimum-32-characters";
            var issuer = jwtSettings["Issuer"] ?? "YourAppName";
            var audience = jwtSettings["Audience"] ?? "YourAppUsers";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, member.Id.ToString()),
                new Claim(ClaimTypes.Email, member.Email),
                new Claim(ClaimTypes.Name, seller.RealName ?? string.Empty),
                new Claim("SellerId", seller.Id.ToString()),
                new Claim("SellerStatus", seller.ApplicationStatus ?? "pending"),
                new Claim(ClaimTypes.Role, "Vendor")
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 私有方法：取得狀態標籤
        private static string GetStatusLabel(string status)
        {
            return status switch
            {
                "pending" => "審核中",
                "approved" => "已通過",
                "rejected" => "已拒絕",
                _ => "未知狀態"
            };
        }
    }
}