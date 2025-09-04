using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Team.API.Models.EfModel;
using Team.API.Services;
using Team.Backend.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Team.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 配置監聽的端口 - Railway 會設定 PORT 環境變數
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            // Gmail SMTP 設定
            builder.Services.AddSingleton(new SmtpEmailService(
                smtpHost: "smtp.gmail.com",
                smtpPort: 587,
                smtpUser: "tainanjade@gmail.com",      
                smtpPass: "izkb nhjp ilvm tmbi"   
            ));

            //Google Authentication 註冊服務
            builder.Services.AddScoped<JwtService>();

            // 註冊點數服務
            builder.Services.AddScoped<IPointsService, PointsService>();

            // 註冊會員等級服務
            builder.Services.AddScoped<IMemberLevelService, MemberLevelService>();

            // 註冊會員等級公開查詢服務
            builder.Services.AddScoped<IMembershipLevelPublicService, MembershipLevelPublicService>();

            // 註冊會員等級升等服務
            builder.Services.AddScoped<MemberLevelUpgradeService>();

            // 註冊記憶體快取服務
            builder.Services.AddMemoryCache();

            // 設定 Cloudinary - 修改為更安全的方式
            builder.Services.Configure<CloudinarySettings>(
                builder.Configuration.GetSection("Cloudinary"));

            builder.Services.AddSingleton<Cloudinary>(provider =>
            {
                try
                {
                    var config = builder.Configuration.GetSection("Cloudinary").Get<CloudinarySettings>();
                    
                    // 檢查設定是否完整，如果不完整則使用預設值
                    if (config == null || string.IsNullOrEmpty(config.CloudName) || 
                        string.IsNullOrEmpty(config.ApiKey) || string.IsNullOrEmpty(config.ApiSecret))
                    {
                        // 使用預設設定（開發環境的設定）
                        var account = new Account("jadetainan", "384776688611428", "4dSdNavAr96WmP0vO_wJL8TkbTU");
                        return new Cloudinary(account);
                    }
                    
                    var validAccount = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
                    return new Cloudinary(validAccount);
                }
                catch (Exception ex)
                {
                    // 記錄錯誤並使用預設設定
                    Console.WriteLine($"Cloudinary configuration error: {ex.Message}");
                    var fallbackAccount = new Account("jadetainan", "384776688611428", "4dSdNavAr96WmP0vO_wJL8TkbTU");
                    return new Cloudinary(fallbackAccount);
                }
            });

            // JWT 設定 - 提供預設值
            var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyThatIsLongAndComplex_123!@#";
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://jadeapi-production.up.railway.app";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "https://moonlit-klepon-a78f8c.netlify.app";

            builder.Services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtKey)
                        ),
                        ClockSkew = TimeSpan.Zero // Optional：不允許時間誤差
                    };
                });

            //註冊 EmailService(驗證碼寄送)
            builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
            builder.Services.AddTransient<IEmailService, EmailService>();
            
            // Add services to the container.
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // 註冊 DbContext 到 DI 容器
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // === 註冊購物車相關服務 ===
            builder.Services.AddScoped<ICartService, CartService>();

            // === 註冊結帳相關服務 ===
            builder.Services.AddScoped<ICheckoutService, CheckoutService>();

            // === 註冊綠界金流服務 ===
            builder.Services.Configure<Team.API.Payments.EcpayOptions>(builder.Configuration.GetSection("Ecpay"));
            builder.Services.AddHttpClient<Team.API.Payments.IPaymentGateway, Team.API.Payments.EcpaySandboxGateway>();

            // 配置 CORS 策略 - 支援您的 Netlify 前端網站
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", policy =>
                {
                    policy.WithOrigins(
                        "https://moonlit-klepon-a78f8c.netlify.app",
                        "http://localhost:8087",
                        "http://localhost:3000"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
                });

                // 保留原本的 AllowAll 作為備用
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // 添加全域異常處理
            app.UseExceptionHandler(appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"Internal server error occurred\"}");
                });
            });

            // Configure the HTTP request pipeline.
            // 在生產環境也啟用 Swagger (可選)
            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // 只在開發環境使用 HTTPS 重定向
            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();
            app.UseRouting();
          
            
            // 啟用 CORS - 使用更安全的配置
            app.UseCors("AllowSpecificOrigins");

            // 啟用 Authentication 和 Authorization 中介軟體
            app.UseAuthentication();
            app.UseAuthorization();
       
            app.MapControllers();
            app.Run();
        }
    }
}