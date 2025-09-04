using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using System;
using Team.Backend.Models;
using Team.Backend.Models.EfModel;
using Team.Backend.Repositories;
using Team.Backend.Repositories.Impl;
using Team.Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

namespace Team.Backend
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			//  管理員邀請功能相關服務
			builder.Services.AddTransient<IUserEmailSender, EmailSender>();
			builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

			// 註冊通知郵件發送服務
			builder.Services.AddScoped<INotificationEmailSender, NotificationEmailSender>();

			// 註冊排程服務 - 使用 Hangfire 實作
			builder.Services.AddScoped<IScheduleService, HangfireScheduleService>();

			// JWT 認證設定
			var jwtKey = builder.Configuration["Jwt:Key"] ?? "ChangeThis_Secret_ForDevOnly";
			builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options =>
				{
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuer = false,
						ValidateAudience = false,
						ValidateLifetime = true,
						ValidateIssuerSigningKey = true,
						IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
						NameClaimType = ClaimTypes.NameIdentifier // 讓 UserIdentifier 對應到 NameIdentifier
					};
				});

			// 設定 Cloudinary
			builder.Services.Configure<CloudinarySettings>(
				builder.Configuration.GetSection("Cloudinary"));

			builder.Services.AddSingleton<Cloudinary>(provider =>
			{
				var config = builder.Configuration.GetSection("Cloudinary").Get<CloudinarySettings>();
				if (config == null)
				{
					throw new InvalidOperationException("Cloudinary configuration is missing or invalid.");
				}
				var account = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
				return new Cloudinary(account);
			});

			builder.Services.AddScoped<AdminFinanceService>();
			builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
			builder.Services.AddScoped<IOrderRepository, OrderRepository>();
			builder.Services.AddScoped<IOrderService, OrderService>();
			
			// 物流管理相關服務
			builder.Services.AddScoped<ILogisticsRepository, LogisticsRepository>();
			builder.Services.AddScoped<ILogisticsService, LogisticsService>();

			// Add services to the container.
			builder.Services.AddControllersWithViews();

			// 註冊 HttpClient Factory (OpenAI Controller 需要)
			builder.Services.AddHttpClient();

			// 註冊 Session 服務
			builder.Services.AddSession(options =>
			{
				options.IdleTimeout = TimeSpan.FromMinutes(30);
				options.Cookie.HttpOnly = true;
				options.Cookie.IsEssential = true;
			});

			// CORS 設定
			builder.Services.AddCors(options =>
			{
				options.AddPolicy("AllowAll", policy =>
				{
					policy.AllowAnyOrigin()
						  .AllowAnyHeader()
						  .AllowAnyMethod();
				});
			});

			//加入 Hangfire 服務
			builder.Services.AddHangfire(configuration => configuration
				.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
				.UseSimpleAssemblyNameTypeSerializer()
				.UseRecommendedSerializerSettings()
				.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
				{
					CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
					SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
					QueuePollInterval = TimeSpan.Zero,
					UseRecommendedIsolationLevel = true,
					DisableGlobalLocks = true
				}));

			//加入 Hangfire Server
			builder.Services.AddHangfireServer();

			//註冊AppDbContext到依賴注入容器
			var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];

			builder.Services.AddDbContext<AppDbContext>(options =>
				options.UseSqlServer(connectionString));

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Home/Error");
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}

			// 加入 Hangfire Dashboard（限制存取權限）
			app.UseHangfireDashboard("/hangfire", new DashboardOptions
			{
				Authorization = new[] { new HangfireAuthorizationFilter() }
			});

			app.UseHttpsRedirection();
			app.UseStaticFiles();

			// 啟用 CORS
			app.UseCors("AllowAll");
			app.UseSession();
			app.UseRouting();

			// 啟用認證和授權
			app.UseAuthentication();
			app.UseAuthorization();

			app.MapControllerRoute(
				name: "default",
				pattern: "{controller=Account}/{action=Login}/{id?}");

			app.Run();
		}
	}
}
