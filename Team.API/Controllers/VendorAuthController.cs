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

                    return BadRequest(VendorLoginResponseDto.ErrorResult("��J���ҥ���", errors));
                }

                _logger.LogInformation("�t�ӵn�J����: {Email}", dto.Email);

                // �d�߷|�����
                var member = await _context.Members
                    .Include(m => m.Seller)
                        .ThenInclude(s => s.SellerBankAccounts)
                    .Include(m => m.Seller)
                        .ThenInclude(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(m => m.Email == dto.Email && m.IsActive);

                if (member == null)
                {
                    _logger.LogWarning("�t�ӵn�J���� - �䤣��|��: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("�b���αK�X���~"));
                }

                // ���ұK�X
                var passwordResult = _passwordHasher.VerifyHashedPassword(member, member.PasswordHash, dto.Password);
                if (passwordResult != PasswordVerificationResult.Success)
                {
                    _logger.LogWarning("�t�ӵn�J���� - �K�X���~: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("�b���αK�X���~"));
                }

                // �ˬd�O�_���t��
                var seller = member.Seller;
                if (seller == null)
                {
                    _logger.LogWarning("�t�ӵn�J���� - �D�t�ӱb��: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("���b���D�t�ӱb��"));
                }

                // �ˬd�t�Ӫ��A
                if (!seller.IsActive)
                {
                    _logger.LogWarning("�t�ӵn�J���� - �b���w����: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("�t�ӱb���w����"));
                }

                if (seller.ApplicationStatus != "approved")
                {
                    _logger.LogWarning("�t�ӵn�J���� - �ӽХ��q�L: {Email}, Status: {Status}", dto.Email, seller.ApplicationStatus);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult($"�t�ӥӽЪ��A�G{GetStatusLabel(seller.ApplicationStatus)}"));
                }

                // ����JWT Token
                var token = GenerateJwtToken(member, seller);
                var expiresAt = DateTime.UtcNow.AddDays(7); // Token���Ĵ�7��

                // ��s�̫�n�J�ɶ�
                member.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                var vendorInfo = seller.ToDto();
                vendorInfo.Email = member.Email;

                _logger.LogInformation("�t�ӵn�J���\: {Email}, SellerId: {SellerId}", dto.Email, seller.Id);

                return Ok(VendorLoginResponseDto.SuccessResult(token, seller.Id, vendorInfo, expiresAt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�t�ӵn�J�L�{�o�Ϳ��~: {Email}", dto.Email);
                return StatusCode(500, VendorLoginResponseDto.ErrorResult("�n�J�L�{�o�Ϳ��~�A�еy��A��"));
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

                    return BadRequest(ApiResponseDto<object>.ErrorResult("��J���ҥ���", errors));
                }

                _logger.LogInformation("�t�ӵ��U�ӽ�: {Email}", dto.Email);

                // �ˬdEmail�O�_�w�s�b
                var existingMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.Email == dto.Email);

                if (existingMember != null)
                {
                    return Conflict(ApiResponseDto<object>.ErrorResult("��Email�w�Q���U"));
                }

                // �ˬd�����Ҧr���O�_�w�s�b
                var existingSeller = await _context.Sellers
                    .FirstOrDefaultAsync(s => s.IdNumber == dto.IdNumber);

                if (existingSeller != null)
                {
                    return Conflict(ApiResponseDto<object>.ErrorResult("�������Ҧr���w�Q���U"));
                }

                // �إ߷|���b��
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

                // �إ߼t�ӥӽ�
                var seller = new Seller
                {
                    MembersId = member.Id,
                    RealName = dto.RealName,
                    IdNumber = dto.IdNumber,
                    ApplicationStatus = "pending", // �ݼf��
                    IsActive = false, // �w�]���ҥΡA���f�ֳq�L
                    AppliedAt = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Sellers.Add(seller);
                await _context.SaveChangesAsync();

                _logger.LogInformation("�t�ӵ��U�ӽЦ��\: {Email}, SellerId: {SellerId}", dto.Email, seller.Id);

                return Ok(ApiResponseDto<object>.SuccessResult(
                    new { sellerId = seller.Id },
                    "�t�ӥӽФw����A�е��Լf�ֵ��G"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�t�ӵ��U�L�{�o�Ϳ��~: {Email}", dto.Email);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("���U�L�{�o�Ϳ��~�A�еy��A��"));
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
                    return NotFound(ApiResponseDto<VendorInfoDto>.ErrorResult("�䤣��t�Ӹ��"));
                }

                var vendorInfo = seller.ToDto();
                vendorInfo.Email = seller.Members?.Email ?? string.Empty;

                return Ok(ApiResponseDto<VendorInfoDto>.SuccessResult(vendorInfo, "����t�Ӹ�Ʀ��\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "����t�Ӹ�ƥ���: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<VendorInfoDto>.ErrorResult("����t�Ӹ�ƥ���"));
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

                    return BadRequest(ApiResponseDto<VendorInfoDto>.ErrorResult("��J���ҥ���", errors));
                }

                var seller = await _context.Sellers
                    .Include(s => s.Members)
                    .Include(s => s.SellerBankAccounts)
                    .Include(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(s => s.Id == sellerId);

                if (seller == null)
                {
                    return NotFound(ApiResponseDto<VendorInfoDto>.ErrorResult("�䤣��t�Ӹ��"));
                }

                // ��s�򥻸��
                seller.RealName = dto.RealName;
                seller.UpdatedAt = DateTime.Now;

                // ��s�Ϋإ߻Ȧ�b����
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

                // ��s�Ϋإ߰h�f���
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

                // ���s���J��ƨæ^��
                await _context.Entry(seller).ReloadAsync();
                var updatedVendorInfo = seller.ToDto();
                updatedVendorInfo.Email = seller.Members?.Email ?? string.Empty;

                _logger.LogInformation("�t�Ӹ�Ƨ�s���\: SellerId: {SellerId}", sellerId);

                return Ok(ApiResponseDto<VendorInfoDto>.SuccessResult(updatedVendorInfo, "�t�Ӹ�Ƨ�s���\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "��s�t�Ӹ�ƥ���: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<VendorInfoDto>.ErrorResult("��s�t�Ӹ�ƥ���"));
            }
        }

        // POST: api/VendorAuth/logout
        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponseDto<object>>> Logout([FromQuery] int sellerId)
        {
            try
            {
                // �o�̥i�H�[�J�n�X�޿�A�p�G���ϥ� refresh token ����
                // �ثe�D�n�O�O���n�X�ƥ�
                _logger.LogInformation("�t�ӵn�X: SellerId: {SellerId}", sellerId);

                return Ok(ApiResponseDto<object>.SuccessResult(null, "�n�X���\"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�t�ӵn�X�L�{�o�Ϳ��~: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("�n�X�L�{�o�Ϳ��~"));
            }
        }

        // �p����k�G����JWT Token
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

        // �p����k�G���o���A����
        private static string GetStatusLabel(string status)
        {
            return status switch
            {
                "pending" => "�f�֤�",
                "approved" => "�w�q�L",
                "rejected" => "�w�ڵ�",
                _ => "�������A"
            };
        }
    }
}