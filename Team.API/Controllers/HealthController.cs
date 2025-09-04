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
        /// �򥻰��d�ˬd
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
        /// ���լG�N�ߥX���`�]�Ω���ղ��`�B�z�^
        /// </summary>
        [HttpGet("test-error")]
        public IActionResult TestError()
        {
            throw new InvalidOperationException("�o�O�@�Ӵ��տ��~�A�Ψ����Ҳ��`�B�z�O�_���`�u�@");
        }

        /// <summary>
        /// �ԲӰ��d�ˬd�]�]�t��Ʈw�s�u�^
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

            // �ˬd��Ʈw�M�]�w�O�_���`
            bool isDatabaseHealthy = (bool)((dynamic)databaseCheck).connected;
            bool isConfigurationValid = (bool)((dynamic)configurationCheck).isValid;

            var isHealthy = isDatabaseHealthy && isConfigurationValid;

            return isHealthy ? Ok(healthCheck) : StatusCode(500, healthCheck);
        }

        /// <summary>
        /// �ˬd�S�w�|���O�_�s�b
        /// </summary>
        [HttpGet("member/{memberId}")]
        public async Task<IActionResult> CheckMember(int memberId)
        {
            try
            {
                _logger.LogInformation("�}�l�ˬd�|�� {MemberId}", memberId);

                // �ϥΤ��ܤ�ƶi���Ʈw�ާ@
                var originalCulture = CultureInfo.CurrentCulture;
                var originalUICulture = CultureInfo.CurrentUICulture;
                
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    
                    var member = await _context.Members.FindAsync(memberId);
                    var profile = await _context.MemberProfiles
                        .FirstOrDefaultAsync(p => p.MembersId == memberId);
                    
                    _logger.LogInformation("�|�� {MemberId} �ˬd����: �|���s�b={MemberExists}, ��Ʀs�b={ProfileExists}", 
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
                        suggestion = member == null ? "�|�����s�b" : 
                                   profile == null ? "�|���s�b���S��������" : "�|���M��Ƴ��s�b"
                    });
                }
                finally
                {
                    // ��_��l��Ƴ]�w
                    try
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUICulture;
                    }
                    catch
                    {
                        // �p�G�L�k��_�A�O�����ܤ��
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�ˬd�|�� {MemberId} �ɵo�Ϳ��~", memberId);
                return StatusCode(500, new
                {
                    memberId = memberId,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    suggestion = "�ˬd��Ʈw�s���η|����Ƶ��c"
                });
            }
        }

        /// <summary>
        /// ²�檺��Ʈw����
        /// </summary>
        [HttpGet("db-test")]
        public async Task<IActionResult> DatabaseTest()
        {
            try
            {
                _logger.LogInformation("�}�l��Ʈw����");

                var originalCulture = CultureInfo.CurrentCulture;
                var originalUICulture = CultureInfo.CurrentUICulture;
                
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    
                    // ���հ򥻳s��
                    var canConnect = await _context.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        return StatusCode(500, new { error = "�L�k�s�����Ʈw" });
                    }

                    // ����²��d��
                    var memberCount = await _context.Members.CountAsync();
                    var profileCount = await _context.MemberProfiles.CountAsync();

                    // ���ը��o�e 5 ���|��
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
                        // �p�G�L�k��_�A�O�����ܤ��
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "��Ʈw���ե���");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    suggestion = "�ˬd��Ʈw�s���r��M�����s��"
                });
            }
        }

        private async Task<object> CheckDatabaseAsync()
        {
            try
            {
                // �ϥΤ��ܤ�ƶi���Ʈw�ާ@
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
                    // ��_��l��Ƴ]�w
                    try
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUICulture;
                    }
                    catch
                    {
                        // �p�G�L�k��_�A�O�����ܤ��
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
                    suggestion = "���ըϥΤ��ܤ�ƼҦ��s����Ʈw"
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
                    recommendation = "�ϥΤ��ܤ�ƥi�קK���ݳ��p���ۮe�ʰ��D"
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

            // ���ñK�X����
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString, 
                @"Password=([^;]+)", 
                "Password=***");
        }
    }
}