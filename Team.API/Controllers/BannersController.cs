using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

[Route("api/[controller]")]
[ApiController]
public class BannersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly Cloudinary _cloudinary;

    public BannersController(AppDbContext context, Cloudinary cloudinary)
    {
        _context = context;
        _cloudinary = cloudinary;
    }

    // GET: api/banners/homepage - 前台取得首頁廣告
    [HttpGet("homepage")]
    public async Task<ActionResult<object>> GetHomepageBanners()
    {
        try
        {
            var now = DateTime.Now;
            var banners = await _context.Banners
                .Where(b => b.IsActive == true &&
                           (b.Page == "homepage" || b.Page == "首頁" || b.Page == "Homepage" || b.Page == "全站"))
                .Where(b => b.StartTime == null || b.StartTime <= now)
                .Where(b => b.EndTime == null || b.EndTime >= now)
                .OrderBy(b => b.DisplayOrder ?? int.MaxValue)
                .ToListAsync();

            var bannerData = banners.Select(b => new
            {
                Id = b.Id,
                Position = b.Position,
                ImageUrl = OptimizeImageUrl(b.ImageUrl, 1200, 628), // ✅ 自動優化
                LinkUrl = b.LinkUrl ?? "/products", // 提供預設連結
                DisplayOrder = b.DisplayOrder,
                Title = b.Title ?? $"精選商品 {b.Id}",
                Description = b.Description ?? "點擊查看更多優質商品",
                // 除錯資訊（可選）
                Debug = new
                {
                    OriginalImageUrl = b.ImageUrl,
                    Optimized = _cloudinary != null && b.ImageUrl?.Contains("cloudinary.com") == true,
                    IsCloudinaryUrl = b.ImageUrl?.Contains("cloudinary.com") == true,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                }
            }).ToList();

            return Ok(new
            {
                success = true,
                data = bannerData,
                count = bannerData.Count,
                message = $"成功取得 {bannerData.Count} 個廣告",
                cloudinaryEnabled = _cloudinary != null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "取得廣告時發生錯誤",
                error = ex.Message,
                details = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// 如果是 Cloudinary 圖片則回傳優化版本，否則回傳原圖
    /// </summary>
    private string OptimizeImageUrl(string originalUrl, int width, int height, string format = "auto")
    {
        if (string.IsNullOrEmpty(originalUrl))
            return originalUrl;

        // 必須 Cloudinary 有啟用，且是 Cloudinary 圖片
        if (_cloudinary != null && originalUrl.Contains("cloudinary.com"))
        {
            try
            {
                var uri = new Uri(originalUrl);
                var pathParts = uri.AbsolutePath.Split('/');
                var versionIndex = Array.FindIndex(pathParts, part => part.StartsWith("v"));

                if (versionIndex > 0 && versionIndex < pathParts.Length - 1)
                {
                    var publicIdParts = pathParts.Skip(versionIndex + 1).ToArray();
                    var publicId = string.Join("/", publicIdParts);

                    // 移除副檔名
                    var lastDotIndex = publicId.LastIndexOf('.');
                    if (lastDotIndex > 0)
                    {
                        publicId = publicId.Substring(0, lastDotIndex);
                    }

                    return _cloudinary.Api.UrlImgUp
                        .Transform(new Transformation()
                            .Width(width).Height(height).Crop("fill")
                            .Quality("auto").FetchFormat(format))
                        .BuildUrl(publicId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloudinary 優化失敗，使用原始 URL: {ex.Message}");
            }
        }

        // 不是 Cloudinary 圖片或失敗時，回傳原始 URL
        return originalUrl;
    }
}