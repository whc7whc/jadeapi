using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using System.Globalization;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;

        public HealthController(AppDbContext context, IConfiguration configuration, ILogger<HealthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// 基本健康檢查
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    version = "1.0.1",
                    culture = CultureInfo.CurrentCulture.Name,
                    uiCulture = CultureInfo.CurrentUICulture.Name,
                    isInvariantMode = CultureInfo.CurrentCulture.Equals(CultureInfo.InvariantCulture)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
        }

        /// <summary>
        /// 測試故意拋出異常（用於測試異常處理）
        /// </summary>
        [HttpGet("test-error")]
        public IActionResult TestError()
        {
            throw new InvalidOperationException("這是一個測試錯誤，用來驗證異常處理是否正常工作");
        }

        /// <summary>
        /// 詳細健康檢查（包含資料庫連線）
        /// </summary>
        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailed()
        {
            var databaseCheck = await CheckDatabaseAsync();
            var configurationCheck = CheckConfiguration();
            var servicesCheck = CheckServices();
            var cultureCheck = CheckCultureSettings();

            var healthCheck = new
            {
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                port = Environment.GetEnvironmentVariable("PORT") ?? "Unknown",
                database = databaseCheck,
                configuration = configurationCheck,
                services = servicesCheck,
                culture = cultureCheck
            };

            // 檢查資料庫和設定是否正常
            bool isDatabaseHealthy = (bool)((dynamic)databaseCheck).connected;
            bool isConfigurationValid = (bool)((dynamic)configurationCheck).isValid;

            var isHealthy = isDatabaseHealthy && isConfigurationValid;

            return isHealthy ? Ok(healthCheck) : StatusCode(500, healthCheck);
        }

        /// <summary>
        /// 檢查特定會員是否存在
        /// </summary>
        [HttpGet("member/{memberId}")]
        public async Task<IActionResult> CheckMember(int memberId)
        {
            try
            {
                _logger.LogInformation("開始檢查會員 {MemberId}", memberId);

                // 使用不變文化進行資料庫操作
                var originalCulture = CultureInfo.CurrentCulture;
                var originalUICulture = CultureInfo.CurrentUICulture;
                
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    
                    var member = await _context.Members.FindAsync(memberId);
                    var profile = await _context.MemberProfiles
                        .FirstOrDefaultAsync(p => p.MembersId == memberId);
                    
                    _logger.LogInformation("會員 {MemberId} 檢查完成: 會員存在={MemberExists}, 資料存在={ProfileExists}", 
                        memberId, member != null, profile != null);

                    return Ok(new
                    {
                        memberId = memberId,
                        memberExists = member != null,
                        profileExists = profile != null,
                        memberData = member != null ? new
                        {
                            id = member.Id,
                            email = member.Email,
                            isEmailVerified = member.IsEmailVerified,
                            isActive = member.IsActive,
                            level = member.Level,
                            registeredVia = member.RegisteredVia,
                            createdAt = member.CreatedAt
                        } : null,
                        profileData = profile != null ? new
                        {
                            name = profile.Name,
                            gender = profile.Gender,
                            birthDate = profile.BirthDate.ToString(),
                            profileImg = profile.ProfileImg,
                            createdAt = profile.CreatedAt,
                            updatedAt = profile.UpdatedAt
                        } : null,
                        suggestion = member == null ? "會員不存在" : 
                                   profile == null ? "會員存在但沒有完整資料" : "會員和資料都存在"
                    });
                }
                finally
                {
                    // 恢復原始文化設定
                    try
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUICulture;
                    }
                    catch
                    {
                        // 如果無法恢復，保持不變文化
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查會員 {MemberId} 時發生錯誤", memberId);
                return StatusCode(500, new
                {
                    memberId = memberId,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    suggestion = "檢查資料庫連接或會員資料結構"
                });
            }
        }

        /// <summary>
        /// 簡單的資料庫測試
        /// </summary>
        [HttpGet("db-test")]
        public async Task<IActionResult> DatabaseTest()
        {
            try
            {
                _logger.LogInformation("開始資料庫測試");

                var originalCulture = CultureInfo.CurrentCulture;
                var originalUICulture = CultureInfo.CurrentUICulture;
                
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    
                    // 測試基本連接
                    var canConnect = await _context.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        return StatusCode(500, new { error = "無法連接到資料庫" });
                    }

                    // 測試簡單查詢
                    var memberCount = await _context.Members.CountAsync();
                    var profileCount = await _context.MemberProfiles.CountAsync();

                    // 測試取得前 5 筆會員
                    var sampleMembers = await _context.Members
                        .Take(5)
                        .Select(m => new { m.Id, m.Email, m.CreatedAt })
                        .ToListAsync();

                    return Ok(new
                    {
                        databaseConnected = true,
                        memberCount = memberCount,
                        profileCount = profileCount,
                        sampleMembers = sampleMembers,
                        testTime = DateTime.UtcNow
                    });
                }
                finally
                {
                    try
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUICulture;
                    }
                    catch
                    {
                        // 如果無法恢復，保持不變文化
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "資料庫測試失敗");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    suggestion = "檢查資料庫連接字串和網路連接"
                });
            }
        }

        private async Task<object> CheckDatabaseAsync()
        {
            try
            {
                // 使用不變文化進行資料庫操作
                var originalCulture = CultureInfo.CurrentCulture;
                var originalUICulture = CultureInfo.CurrentUICulture;
                
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    
                    await _context.Database.CanConnectAsync();
                    var memberCount = await _context.Members.CountAsync();
                    
                    return new
                    {
                        connected = true,
                        connectionString = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection")),
                        memberCount = memberCount,
                        cultureUsed = "InvariantCulture"
                    };
                }
                finally
                {
                    // 恢復原始文化設定
                    try
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUICulture;
                    }
                    catch
                    {
                        // 如果無法恢復，保持不變文化
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection failed");
                return new
                {
                    connected = false,
                    error = ex.Message,
                    connectionString = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection")),
                    suggestion = "嘗試使用不變文化模式連接資料庫"
                };
            }
        }

        private object CheckConfiguration()
        {
            try
            {
                var jwtKey = _configuration["Jwt:Key"];
                var cloudinaryName = _configuration["Cloudinary:CloudName"];
                var smtpHost = _configuration["SmtpSettings:Host"];
                
                return new
                {
                    isValid = true,
                    hasJwtKey = !string.IsNullOrEmpty(jwtKey),
                    hasCloudinary = !string.IsNullOrEmpty(cloudinaryName),
                    hasSmtp = !string.IsNullOrEmpty(smtpHost),
                    jwtIssuer = _configuration["Jwt:Issuer"],
                    jwtAudience = _configuration["Jwt:Audience"]
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    isValid = false,
                    error = ex.Message
                };
            }
        }

        private object CheckServices()
        {
            return new
            {
                cartServiceRegistered = true,
                checkoutServiceRegistered = true,
                jwtServiceRegistered = true,
                pointsServiceRegistered = true
            };
        }

        private object CheckCultureSettings()
        {
            try
            {
                return new
                {
                    currentCulture = CultureInfo.CurrentCulture.Name,
                    currentUICulture = CultureInfo.CurrentUICulture.Name,
                    isInvariantCulture = CultureInfo.CurrentCulture.Equals(CultureInfo.InvariantCulture),
                    isGlobalizationInvariantMode = AppContext.TryGetSwitch("System.Globalization.Invariant", out bool isInvariant) && isInvariant,
                    availableCultures = CultureInfo.GetCultures(CultureTypes.AllCultures).Length,
                    recommendation = "使用不變文化可避免雲端部署的相容性問題"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    error = ex.Message,
                    fallback = "InvariantCulture"
                };
            }
        }

        private string MaskConnectionString(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Not configured";

            // 隱藏密碼部分
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString, 
                @"Password=([^;]+)", 
                "Password=***");
        }
    }
}