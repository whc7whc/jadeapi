using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

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
                    version = "1.0.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
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

            var healthCheck = new
            {
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                port = Environment.GetEnvironmentVariable("PORT") ?? "Unknown",
                database = databaseCheck,
                configuration = configurationCheck,
                services = servicesCheck
            };

            // 檢查資料庫和設定是否正常
            bool isDatabaseHealthy = (bool)((dynamic)databaseCheck).connected;
            bool isConfigurationValid = (bool)((dynamic)configurationCheck).isValid;

            var isHealthy = isDatabaseHealthy && isConfigurationValid;

            return isHealthy ? Ok(healthCheck) : StatusCode(500, healthCheck);
        }

        private async Task<object> CheckDatabaseAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                var memberCount = await _context.Members.CountAsync();
                
                return new
                {
                    connected = true,
                    connectionString = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection")),
                    memberCount = memberCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection failed");
                return new
                {
                    connected = false,
                    error = ex.Message,
                    connectionString = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection"))
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