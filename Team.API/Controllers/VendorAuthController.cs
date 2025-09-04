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

                    return BadRequest(VendorLoginResponseDto.ErrorResult("Input validation failed", errors));
                }

                _logger.LogInformation("Vendor login attempt: {Email}", dto.Email);

                // Find member
                var member = await _context.Members
                    .Include(m => m.Seller)
                        .ThenInclude(s => s.SellerBankAccounts)
                    .Include(m => m.Seller)
                        .ThenInclude(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(m => m.Email == dto.Email && m.IsActive);

                if (member == null)
                {
                    _logger.LogWarning("Vendor login failed - member not found: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("Invalid email or password"));
                }

                // Verify password
                var passwordResult = _passwordHasher.VerifyHashedPassword(member, member.PasswordHash, dto.Password);
                if (passwordResult != PasswordVerificationResult.Success)
                {
                    _logger.LogWarning("Vendor login failed - invalid password: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("Invalid email or password"));
                }

                // Check if vendor account exists
                var seller = member.Seller;
                if (seller == null)
                {
                    _logger.LogWarning("Vendor login failed - not a vendor account: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("This account is not a vendor account"));
                }

                // Check vendor status
                if (!seller.IsActive)
                {
                    _logger.LogWarning("Vendor login failed - account disabled: {Email}", dto.Email);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult("Vendor account is disabled"));
                }

                if (seller.ApplicationStatus != "approved")
                {
                    _logger.LogWarning("Vendor login failed - application not approved: {Email}, Status: {Status}", dto.Email, seller.ApplicationStatus);
                    return Unauthorized(VendorLoginResponseDto.ErrorResult($"Vendor application status: {GetStatusLabel(seller.ApplicationStatus)}"));
                }

                // Generate JWT Token
                var token = GenerateJwtToken(member, seller);
                var expiresAt = DateTime.UtcNow.AddDays(7); // Token expires in 7 days

                // Update last login time
                member.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                var vendorInfo = seller.ToDto();
                vendorInfo.Email = member.Email;

                _logger.LogInformation("Vendor login successful: {Email}, SellerId: {SellerId}", dto.Email, seller.Id);

                return Ok(VendorLoginResponseDto.SuccessResult(token, seller.Id, vendorInfo, expiresAt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vendor login process error: {Email}", dto.Email);
                return StatusCode(500, VendorLoginResponseDto.ErrorResult("Login process error, please try again"));
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

                    return BadRequest(ApiResponseDto<object>.ErrorResult("Input validation failed", errors));
                }

                _logger.LogInformation("Vendor registration application: {Email}", dto.Email);

                // Check if email already exists
                var existingMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.Email == dto.Email);

                if (existingMember != null)
                {
                    return Conflict(ApiResponseDto<object>.ErrorResult("Email already registered"));
                }

                // Check if ID number already exists
                var existingSeller = await _context.Sellers
                    .FirstOrDefaultAsync(s => s.IdNumber == dto.IdNumber);

                if (existingSeller != null)
                {
                    return Conflict(ApiResponseDto<object>.ErrorResult("ID number already registered"));
                }

                // Create member account
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

                // Create vendor application
                var seller = new Seller
                {
                    MembersId = member.Id,
                    RealName = dto.RealName,
                    IdNumber = dto.IdNumber,
                    ApplicationStatus = "pending", // Pending review
                    IsActive = false, // Default disabled, activate after approval
                    AppliedAt = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Sellers.Add(seller);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Vendor registration application successful: {Email}, SellerId: {SellerId}", dto.Email, seller.Id);

                return Ok(ApiResponseDto<object>.SuccessResult(
                    new { sellerId = seller.Id },
                    "Vendor application submitted, please wait for review"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vendor registration process error: {Email}", dto.Email);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("Registration process error, please try again"));
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
                    return NotFound(ApiResponseDto<VendorInfoDto>.ErrorResult("Vendor not found"));
                }

                var vendorInfo = seller.ToDto();
                vendorInfo.Email = seller.Members?.Email ?? string.Empty;

                return Ok(ApiResponseDto<VendorInfoDto>.SuccessResult(vendorInfo, "Get vendor info successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get vendor info failed: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<VendorInfoDto>.ErrorResult("Get vendor info failed"));
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

                    return BadRequest(ApiResponseDto<VendorInfoDto>.ErrorResult("Input validation failed", errors));
                }

                var seller = await _context.Sellers
                    .Include(s => s.Members)
                    .Include(s => s.SellerBankAccounts)
                    .Include(s => s.SellerReturnInfos)
                    .FirstOrDefaultAsync(s => s.Id == sellerId);

                if (seller == null)
                {
                    return NotFound(ApiResponseDto<VendorInfoDto>.ErrorResult("Vendor not found"));
                }

                // Update basic info
                seller.RealName = dto.RealName;
                seller.UpdatedAt = DateTime.Now;

                // Update or create bank account
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

                // Update or create return info
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

                // Reload updated data and return
                await _context.Entry(seller).ReloadAsync();
                var updatedVendorInfo = seller.ToDto();
                updatedVendorInfo.Email = seller.Members?.Email ?? string.Empty;

                _logger.LogInformation("Vendor info update successful: SellerId: {SellerId}", sellerId);

                return Ok(ApiResponseDto<VendorInfoDto>.SuccessResult(updatedVendorInfo, "Vendor info update successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update vendor info failed: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<VendorInfoDto>.ErrorResult("Update vendor info failed"));
            }
        }

        // POST: api/VendorAuth/logout
        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponseDto<object>>> Logout([FromQuery] int sellerId)
        {
            try
            {
                // Here can add logout logic, such as invalidating refresh token
                // Currently just record logout log
                _logger.LogInformation("Vendor logout: SellerId: {SellerId}", sellerId);

                return Ok(ApiResponseDto<object>.SuccessResult(null, "Logout successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vendor logout process error: SellerId: {SellerId}", sellerId);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("Logout process error"));
            }
        }

        // Private method: Generate JWT Token
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

        // Private method: Get status label
        private static string GetStatusLabel(string status)
        {
            return status switch
            {
                "pending" => "Pending Review",
                "approved" => "Approved",
                "rejected" => "Rejected",
                _ => "Unknown Status"
            };
        }
    }
}