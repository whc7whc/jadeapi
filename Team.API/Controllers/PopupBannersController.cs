using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

[ApiController]
[Route("api/[controller]")]
public class PopupBannersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<PopupBannersController> _logger;

    public PopupBannersController(AppDbContext context, ILogger<PopupBannersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 取得目前啟用的彈出廣告
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActivePopupBanners()
    {
        try
        {
            _logger.LogInformation("取得啟用的彈出廣告");

            var now = DateTime.Now;
            var popupBanners = await _context.Banners
                .Where(b => b.IsActive == true &&
                           (b.StartTime == null || b.StartTime <= now) &&
                           (b.EndTime == null || b.EndTime >= now) &&
                           (b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式")) // 🔥 專門篩選彈出式廣告
                .OrderBy(b => b.DisplayOrder)
                .Select(b => new {
                    Id = b.Id,
                    Title = b.Title ?? "無標題",
                    Subtitle = ExtractSubtitle(b.Description), // 從描述中提取副標題
                    Description = b.Description ?? "",
                    Image = b.ImageUrl ?? "https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=600&h=400&fit=crop",
                    ButtonText = GetButtonText(b.LinkUrl), // 根據連結決定按鈕文字
                    ButtonLink = b.LinkUrl ?? "#",
                    BackgroundClass = GetBackgroundClass(b.Id), // 動態背景顏色
                    ProductId = b.ProductId,
                    Page = b.Page,
                    ClickCount = b.ClickCount ?? 0
                })
                .ToListAsync();

            _logger.LogInformation($"找到 {popupBanners.Count} 個啟用的彈出廣告");

            return Ok(new
            {
                Success = true,
                Data = popupBanners,
                Count = popupBanners.Count,
                Message = $"成功取得 {popupBanners.Count} 個彈出廣告"
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得彈出廣告時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "取得彈出廣告失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 取得特定頁面的彈出廣告
    /// </summary>
    [HttpGet("page/{page}")]
    public async Task<IActionResult> GetPopupBannersByPage(string page)
    {
        try
        {
            _logger.LogInformation($"取得頁面 {page} 的彈出廣告");

            var now = DateTime.Now;
            var popupBanners = await _context.Banners
                .Where(b => b.IsActive == true &&
                           b.Page == page &&
                           (b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式") &&
                           (b.StartTime == null || b.StartTime <= now) &&
                           (b.EndTime == null || b.EndTime >= now))
                .OrderBy(b => b.DisplayOrder)
                .Select(b => new {
                    Id = b.Id,
                    Title = b.Title,
                    Subtitle = ExtractSubtitle(b.Description),
                    Description = b.Description,
                    Image = b.ImageUrl,
                    ButtonText = GetButtonText(b.LinkUrl),
                    ButtonLink = b.LinkUrl,
                    BackgroundClass = GetBackgroundClass(b.Id)
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Data = popupBanners,
                Page = page,
                Count = popupBanners.Count
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"取得頁面 {page} 彈出廣告時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "取得彈出廣告失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 記錄彈出廣告點擊
    /// </summary>
    [HttpPost("{id}/click")]
    public async Task<IActionResult> RecordPopupClick(int id, [FromBody] PopupClickRequest? request = null)
    {
        try
        {
            _logger.LogInformation($"記錄彈出廣告 {id} 的點擊");

            var banner = await _context.Banners
                .Where(b => b.Id == id && (b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式"))
                .FirstOrDefaultAsync();

            if (banner == null)
            {
                return NotFound(new { Success = false, Message = "彈出廣告不存在" });
            }

            // 增加點擊次數
            banner.ClickCount = (banner.ClickCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"彈出廣告 {id} 點擊次數已更新為 {banner.ClickCount}");

            return Ok(new
            {
                Success = true,
                Message = "點擊記錄成功",
                Data = new
                {
                    BannerId = id,
                    ClickCount = banner.ClickCount,
                    Title = banner.Title
                }
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"記錄彈出廣告 {id} 點擊時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "記錄點擊失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 記錄彈出廣告展示（曝光）
    /// </summary>
    [HttpPost("{id}/impression")]
    public async Task<IActionResult> RecordPopupImpression(int id)
    {
        try
        {
            _logger.LogInformation($"記錄彈出廣告 {id} 的展示");

            var banner = await _context.Banners
                .Where(b => b.Id == id && (b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式"))
                .FirstOrDefaultAsync();

            if (banner == null)
            {
                return NotFound(new { Success = false, Message = "彈出廣告不存在" });
            }

            // 這裡可以記錄展示數據到另一個表，或者增加展示次數欄位
            // 目前先簡單記錄到日誌
            _logger.LogInformation($"彈出廣告 {id} 展示記錄：{banner.Title}");

            return Ok(new
            {
                Success = true,
                Message = "展示記錄成功",
                Data = new
                {
                    BannerId = id,
                    Title = banner.Title
                }
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"記錄彈出廣告 {id} 展示時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "記錄展示失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 取得彈出廣告統計
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetPopupStatistics()
    {
        try
        {
            var statistics = await _context.Banners
                .Where(b => b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式")
                .GroupBy(b => b.IsActive)
                .Select(g => new {
                    IsActive = g.Key,
                    Count = g.Count(),
                    TotalClicks = g.Sum(b => b.ClickCount ?? 0)
                })
                .ToListAsync();

            var totalBanners = await _context.Banners
                .Where(b => b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式")
                .CountAsync();

            var activeBanners = await _context.Banners
                .Where(b => (b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式") && b.IsActive == true)
                .CountAsync();

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    TotalPopupBanners = totalBanners,
                    ActivePopupBanners = activeBanners,
                    InactivePopupBanners = totalBanners - activeBanners,
                    TotalClicks = statistics.Sum(s => s.TotalClicks),
                    Statistics = statistics
                }
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得彈出廣告統計時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "取得統計失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 測試用：取得所有彈出廣告（包含未啟用的）
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllPopupBanners()
    {
        try
        {
            var popupBanners = await _context.Banners
                .Where(b => b.Position == "popup" || b.Position == "splash" || b.Position == "彈出式")
                .OrderByDescending(b => b.Id)
                .Select(b => new {
                    Id = b.Id,
                    Title = b.Title,
                    Page = b.Page,
                    Position = b.Position,
                    IsActive = b.IsActive,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    ClickCount = b.ClickCount ?? 0,
                    ImageUrl = b.ImageUrl,
                    LinkUrl = b.LinkUrl,
                    Description = b.Description
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Data = popupBanners,
                Total = popupBanners.Count,
                Message = "取得所有彈出廣告成功"
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得所有彈出廣告時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "取得彈出廣告失敗",
                Error = ex.Message
            });
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// 從描述中提取副標題
    /// </summary>
    private static string ExtractSubtitle(string? description)
    {
        if (string.IsNullOrEmpty(description)) return "";

        // 如果描述包含換行，取第一行作為副標題
        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 1 ? lines[0] : description.Length > 50 ? description.Substring(0, 50) + "..." : description;
    }

    /// <summary>
    /// 根據連結決定按鈕文字
    /// </summary>
    private static string GetButtonText(string? linkUrl)
    {
        if (string.IsNullOrEmpty(linkUrl) || linkUrl == "#") return "查看詳情";

        if (linkUrl.Contains("/shop") || linkUrl.Contains("/product")) return "立即購買";
        if (linkUrl.Contains("/register") || linkUrl.Contains("/signup")) return "立即註冊";
        if (linkUrl.Contains("/login")) return "立即登入";
        if (linkUrl.Contains("/posts") || linkUrl.Contains("/blog")) return "立即參加";
        if (linkUrl.Contains("/discount") || linkUrl.Contains("/coupon")) return "立即領取";

        return "立即查看";
    }

    /// <summary>
    /// 根據廣告 ID 產生背景顏色類別
    /// </summary>
    private static string GetBackgroundClass(int id)
    {
        string[] backgroundClasses = {
            "bg-pink",    // 粉色漸層
            "bg-blue",    // 藍色漸層
            "bg-green",   // 綠色漸層
            "bg-purple",  // 紫色漸層
            "bg-orange",  // 橘色漸層
            "bg-teal",    // 青色漸層
            "bg-indigo"   // 靛色漸層
        };
        return backgroundClasses[id % backgroundClasses.Length];
    }

    #endregion
}

#region Request Models

/// <summary>
/// 彈出廣告點擊請求模型
/// </summary>
public class PopupClickRequest
{
    /// <summary>
    /// 點擊來源頁面
    /// </summary>
    public string? SourcePage { get; set; }

    /// <summary>
    /// 用戶代理
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// IP 位址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 點擊時間
    /// </summary>
    public DateTime ClickTime { get; set; } = DateTime.Now;
}

#endregion