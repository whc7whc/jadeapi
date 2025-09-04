using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Team.Backend.Controllers
{
    public class SplashController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SplashController> _logger;
        private readonly Cloudinary _cloudinary;

        public SplashController(AppDbContext context, ILogger<SplashController> logger, Cloudinary cloudinary)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
            _cloudinary = cloudinary;
        }

        // GET: 顯示彈出廣告列表
        public async Task<IActionResult> Splash()
        {
            try
            {
                // 🔥 更明確的過濾條件，確保只顯示彈出廣告
                var popupBanners = await _context.Banners
                    .Where(b => b.Position == "popup" ||
                               b.Position == "splash" ||
                               b.Position == "彈出式" ||
                               b.Position == "彈窗" ||
                               b.Position.StartsWith("彈"))
                    .OrderByDescending(b => b.UpdatedAt)
                    .ThenByDescending(b => b.Id)
                    .ToListAsync();

                _logger.LogInformation($"🎯 載入了 {popupBanners.Count} 個彈出廣告");
                return View("~/Views/AdManage/Splash.cshtml", popupBanners);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入彈出廣告列表時發生錯誤");
                TempData["ErrorMessage"] = "載入廣告列表時發生錯誤";
                return View("~/Views/AdManage/Splash.cshtml", new List<Banner>());
            }
        }

        // GET: 顯示建立彈出廣告頁面
        [HttpGet]
        public IActionResult CreateSplash()
        {
            var model = new CreatePopupBannerViewModel
            {
                IsActive = true,
                DisplayOrder = 1,
                Position = "popup", // 🔥 預設為 popup
                Page = "home"
            };
            return View("~/Views/AdManage/CreateSplash.cshtml", model);
        }

        // POST: 處理建立彈出廣告
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSplash(CreatePopupBannerViewModel model, IFormFile ImageFile)
        {
            try
            {
                _logger.LogInformation($"=== CreateSplash POST 開始 ===");
                _logger.LogInformation($"收到模型資料 - Title: {model.Title}, Position: {model.Position}");

                // 基本驗證
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["ErrorMessage"] = "廣告標題不能為空";
                    return View("~/Views/AdManage/CreateSplash.cshtml", model);
                }

                if (string.IsNullOrWhiteSpace(model.Description))
                {
                    TempData["ErrorMessage"] = "廣告內容不能為空";
                    return View("~/Views/AdManage/CreateSplash.cshtml", model);
                }

                if (string.IsNullOrWhiteSpace(model.Page))
                {
                    TempData["ErrorMessage"] = "請選擇觸發頁面";
                    return View("~/Views/AdManage/CreateSplash.cshtml", model);
                }

                if (string.IsNullOrWhiteSpace(model.Position))
                {
                    TempData["ErrorMessage"] = "請選擇彈窗類型";
                    return View("~/Views/AdManage/CreateSplash.cshtml", model);
                }

                var now = DateTime.Now;
                string imageUrl = null;

                // 🔥 處理圖片上傳（如果有的話）
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    imageUrl = await UploadToCloudinary(ImageFile);
                    if (imageUrl == null)
                    {
                        TempData["ErrorMessage"] = "圖片上傳失敗，請檢查檔案格式和大小";
                        return View("~/Views/AdManage/CreateSplash.cshtml", model);
                    }
                }

                // 🔥 建立新的彈出廣告，確保 Position 正確
                var newBanner = new Banner
                {
                    Title = model.Title.Trim(),
                    Description = model.Description?.Trim() ?? "",
                    Page = model.Page.Trim(),
                    Position = model.Position.Trim(), // 🔥 確保使用正確的 Position
                    ImageUrl = imageUrl ?? model.ImageUrl ?? "https://via.placeholder.com/800x500/667eea/ffffff?text=彈出廣告",
                    LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim(),
                    ProductId = null,
                    DisplayOrder = model.DisplayOrder > 0 ? model.DisplayOrder : 1,
                    IsActive = model.IsActive,
                    StartTime = model.StartTime,
                    EndTime = model.EndTime,
                    ClickCount = 0,
                    CreatedBy = null,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _logger.LogInformation($"準備新增彈出廣告: {newBanner.Title}, Position: {newBanner.Position}");

                _context.Banners.Add(newBanner);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 彈出廣告建立成功！ID: {newBanner.Id}");

                TempData["SuccessMessage"] = $"✅ 彈出廣告「{newBanner.Title}」已成功建立！";

                // 🔥 確保跳轉到 Splash 管理頁面，而不是 Banner 頁面
                return RedirectToAction("Splash", "Splash");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立彈出廣告時發生錯誤");
                TempData["ErrorMessage"] = $"❌ 建立廣告時發生錯誤：{ex.Message}";
                return View("~/Views/AdManage/CreateSplash.cshtml", model ?? new CreatePopupBannerViewModel());
            }
        }

        // GET: 顯示編輯彈出廣告頁面
        [HttpGet]
        public async Task<IActionResult> EditSplash(int id)
        {
            try
            {
                // 🔥 更明確的過濾條件
                var banner = await _context.Banners
                    .Where(b => b.Id == id && (
                        b.Position == "popup" ||
                        b.Position == "splash" ||
                        b.Position == "彈出式" ||
                        b.Position == "彈窗" ||
                        b.Position.StartsWith("彈")))
                    .FirstOrDefaultAsync();

                if (banner == null)
                {
                    TempData["ErrorMessage"] = "找不到指定的彈出廣告";
                    return RedirectToAction("Splash");
                }

                var model = new CreatePopupBannerViewModel
                {
                    Id = banner.Id,
                    Title = banner.Title ?? "",
                    Description = banner.Description ?? "",
                    ImageUrl = banner.ImageUrl,
                    LinkUrl = banner.LinkUrl,
                    Page = banner.Page ?? "home",
                    Position = banner.Position ?? "popup",
                    IsActive = banner.IsActive ?? true,
                    DisplayOrder = banner.DisplayOrder ?? 1,
                    StartTime = banner.StartTime,
                    EndTime = banner.EndTime
                };

                return View("~/Views/AdManage/CreateSplash.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"載入編輯頁面時發生錯誤: ID={id}");
                TempData["ErrorMessage"] = "載入編輯頁面時發生錯誤";
                return RedirectToAction("Splash");
            }
        }

        // POST: 處理編輯彈出廣告
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSplash(int id, CreatePopupBannerViewModel model, IFormFile ImageFile)
        {
            if (id != model.Id)
            {
                return BadRequest("ID 不匹配");
            }

            try
            {
                _logger.LogInformation($"=== EditSplash POST 開始，ID: {id} ===");

                // 基本驗證
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["ErrorMessage"] = "廣告標題不能為空";
                    return View("~/Views/AdManage/CreateSplash.cshtml", model);
                }

                if (string.IsNullOrWhiteSpace(model.Description))
                {
                    TempData["ErrorMessage"] = "廣告內容不能為空";
                    return View("~/Views/AdManage/CreateSplash.cshtml", model);
                }

                var banner = await _context.Banners.FindAsync(id);
                if (banner == null)
                {
                    TempData["ErrorMessage"] = "找不到指定的彈出廣告";
                    return RedirectToAction("Splash");
                }

                // 🔥 處理新圖片上傳
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // 刪除舊圖片（如果是 Cloudinary 圖片）
                    if (!string.IsNullOrEmpty(banner.ImageUrl) && banner.ImageUrl.Contains("cloudinary.com"))
                    {
                        await DeleteCloudinaryImage(banner.ImageUrl);
                    }

                    // 上傳新圖片
                    var newImageUrl = await UploadToCloudinary(ImageFile);
                    if (newImageUrl != null)
                    {
                        banner.ImageUrl = newImageUrl;
                        _logger.LogInformation($"已更新圖片: {newImageUrl}");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "新圖片上傳失敗";
                        return View("~/Views/AdManage/CreateSplash.cshtml", model);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
                {
                    banner.ImageUrl = model.ImageUrl.Trim();
                }

                // 🔥 更新其他欄位
                banner.Title = model.Title.Trim();
                banner.Description = model.Description?.Trim() ?? "";
                banner.Page = model.Page?.Trim() ?? "home";
                banner.Position = model.Position?.Trim() ?? "popup"; // 🔥 確保 Position 正確
                banner.LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim();
                banner.IsActive = model.IsActive;
                banner.DisplayOrder = model.DisplayOrder > 0 ? model.DisplayOrder : 1;
                banner.StartTime = model.StartTime;
                banner.EndTime = model.EndTime;
                banner.UpdatedAt = DateTime.Now;

                _context.Update(banner);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 彈出廣告更新成功: ID {banner.Id}, 標題: {banner.Title}");

                TempData["SuccessMessage"] = $"✅ 彈出廣告「{banner.Title}」已成功更新！";

                // 🔥 確保跳轉到 Splash 頁面
                return RedirectToAction("Splash", "Splash");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新彈出廣告時發生錯誤: ID {id}");
                TempData["ErrorMessage"] = "❌ 更新廣告時發生錯誤，請重試。";
                return View("~/Views/AdManage/CreateSplash.cshtml", model);
            }
        }

        // POST: 刪除彈出廣告
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // 🔥 確保只能刪除彈出廣告
                var banner = await _context.Banners
                    .Where(b => b.Id == id && (
                        b.Position == "popup" ||
                        b.Position == "splash" ||
                        b.Position == "彈出式" ||
                        b.Position == "彈窗" ||
                        b.Position.StartsWith("彈")))
                    .FirstOrDefaultAsync();

                if (banner == null)
                {
                    TempData["ErrorMessage"] = "找不到指定的彈出廣告";
                    return RedirectToAction("Splash");
                }

                // 🔥 刪除 Cloudinary 圖片
                if (!string.IsNullOrEmpty(banner.ImageUrl) && banner.ImageUrl.Contains("cloudinary.com"))
                {
                    await DeleteCloudinaryImage(banner.ImageUrl);
                }

                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 成功刪除彈出廣告: ID {id}, 標題: {banner.Title}");

                TempData["SuccessMessage"] = "✅ 彈出廣告已成功刪除！";
                return RedirectToAction("Splash"); // 🔥 保持在 Splash 頁面
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"刪除彈出廣告時發生錯誤: ID {id}");
                TempData["ErrorMessage"] = "❌ 刪除廣告時發生錯誤，請重試。";
                return RedirectToAction("Splash");
            }
        }

        // GET: 查看廣告詳情
        public async Task<IActionResult> Details(int id)
        {
            var banner = await _context.Banners
                .Where(b => b.Id == id && (
                    b.Position == "popup" ||
                    b.Position == "splash" ||
                    b.Position == "彈出式" ||
                    b.Position == "彈窗" ||
                    b.Position.StartsWith("彈")))
                .FirstOrDefaultAsync();

            if (banner == null)
            {
                TempData["ErrorMessage"] = "找不到指定的彈出廣告";
                return RedirectToAction("Splash");
            }

            return View("~/Views/AdManage/SplashDetails.cshtml", banner);
        }

        // POST: 切換廣告啟用狀態
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var banner = await _context.Banners.FindAsync(id);
                if (banner == null)
                {
                    return Json(new { success = false, message = "找不到指定的廣告" });
                }

                banner.IsActive = !(banner.IsActive ?? false);
                banner.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    isActive = banner.IsActive,
                    message = banner.IsActive == true ? "廣告已啟用" : "廣告已停用"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"切換廣告狀態時發生錯誤: ID {id}");
                return Json(new { success = false, message = "操作失敗，請重試" });
            }
        }

        // 🔥 API: 取得啟用的彈出廣告（供前端使用）
        [HttpGet]
        public async Task<IActionResult> GetActiveSplashBanners(string page = "home")
        {
            try
            {
                var now = DateTime.Now;

                var activeBanners = await _context.Banners
                    .Where(b => b.IsActive == true)
                    .Where(b => b.Position == "popup" ||
                               b.Position == "splash" ||
                               b.Position == "彈出式" ||
                               b.Position == "彈窗" ||
                               b.Position.StartsWith("彈"))
                    .Where(b => b.Page == page || b.Page == "全站" || b.Page == "all")
                    .Where(b => b.StartTime == null || b.StartTime <= now)
                    .Where(b => b.EndTime == null || b.EndTime >= now)
                    .OrderBy(b => b.DisplayOrder)
                    .ThenByDescending(b => b.Id)
                    .Select(b => new {
                        id = b.Id,
                        title = b.Title,
                        subtitle = b.Description, // 🔥 使用 Description 作為 subtitle
                        description = b.Description,
                        image = b.ImageUrl,
                        buttonText = "立即查看",
                        buttonLink = b.LinkUrl ?? "#",
                        backgroundClass = "bg-gradient" // 🔥 因為現在是全圖片背景，這個可能不需要
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = activeBanners
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"取得彈出廣告時發生錯誤: page={page}");
                return Json(new
                {
                    success = false,
                    message = "取得廣告資料失敗",
                    data = new List<object>()
                });
            }
        }

        // 🔥 API: 記錄廣告展示
        [HttpPost]
        public async Task<IActionResult> RecordImpression(int id)
        {
            try
            {
                var banner = await _context.Banners.FindAsync(id);
                if (banner != null)
                {
                    // 這裡可以加入展示次數的記錄邏輯
                    // 目前 Banner model 沒有 impression 欄位，先用 log 記錄
                    _logger.LogInformation($"📊 彈出廣告展示: ID {id}, 標題: {banner.Title}");

                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "找不到廣告" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"記錄廣告展示時發生錯誤: ID {id}");
                return Json(new { success = false, message = "記錄失敗" });
            }
        }

        // 🔥 API: 記錄廣告點擊
        [HttpPost]
        public async Task<IActionResult> RecordClick(int id)
        {
            try
            {
                var banner = await _context.Banners.FindAsync(id);
                if (banner != null)
                {
                    banner.ClickCount = (banner.ClickCount ?? 0) + 1;
                    banner.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"📊 彈出廣告點擊: ID {id}, 總點擊數: {banner.ClickCount}");

                    return Json(new
                    {
                        success = true,
                        clickCount = banner.ClickCount
                    });
                }

                return Json(new { success = false, message = "找不到廣告" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"記錄廣告點擊時發生錯誤: ID {id}");
                return Json(new { success = false, message = "記錄失敗" });
            }
        }

        // 🔥 私人方法：上傳圖片到 Cloudinary
        private async Task<string?> UploadToCloudinary(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                    return null;

                // 檢查檔案類型
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
                {
                    _logger.LogWarning($"不支援的檔案類型: {imageFile.ContentType}");
                    return null;
                }

                // 檢查檔案大小 (10MB)
                if (imageFile.Length > 10 * 1024 * 1024)
                {
                    _logger.LogWarning($"檔案過大: {imageFile.Length} bytes");
                    return null;
                }

                using var stream = imageFile.OpenReadStream();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    PublicId = $"jade_splash_{DateTime.Now.Ticks}", // 🔥 彈出廣告專用前綴
                    Folder = "jade-splash-banners", // 🔥 彈出廣告專用資料夾
                    Transformation = new Transformation()
                        .Width(800).Height(500).Crop("fill") // 🔥 彈出廣告最佳尺寸
                        .Gravity("center")
                        .Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.LogInformation($"✅ 彈出廣告圖片上傳成功: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    _logger.LogError($"❌ 圖片上傳失敗: {uploadResult.Error?.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上傳圖片到 Cloudinary 時發生錯誤");
                return null;
            }
        }

        // 🔥 私人方法：刪除 Cloudinary 圖片
        private async Task<bool> DeleteCloudinaryImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("cloudinary.com"))
                    return true;

                // 從 URL 提取 public_id
                var uri = new Uri(imageUrl);
                var pathParts = uri.AbsolutePath.Split('/');

                // 尋找版本號後的路徑部分
                var versionIndex = Array.FindIndex(pathParts, part => part.StartsWith("v"));

                if (versionIndex > 0 && versionIndex < pathParts.Length - 1)
                {
                    var publicIdParts = pathParts.Skip(versionIndex + 1).ToArray();
                    var publicId = string.Join("/", publicIdParts);

                    // 移除檔案副檔名
                    var lastDotIndex = publicId.LastIndexOf('.');
                    if (lastDotIndex > 0)
                    {
                        publicId = publicId.Substring(0, lastDotIndex);
                    }

                    var deleteParams = new DeletionParams(publicId)
                    {
                        ResourceType = ResourceType.Image
                    };

                    var result = await _cloudinary.DestroyAsync(deleteParams);

                    if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        _logger.LogInformation($"✅ Cloudinary 圖片刪除成功: {publicId}");
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"❌ Cloudinary 圖片刪除失敗: {result.Error?.Message}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除 Cloudinary 圖片時發生錯誤");
                return false;
            }
        }

        // API: 即時圖片上傳 (AJAX用)
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("沒有選擇檔案");

            try
            {
                var imageUrl = await UploadToCloudinary(file);
                if (imageUrl != null)
                {
                    return Json(new
                    {
                        success = true,
                        url = imageUrl,
                        fileName = Path.GetFileName(file.FileName)
                    });
                }
                else
                {
                    return Json(new { success = false, message = "上傳失敗" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 輔助方法：取得當前用戶ID（目前未使用）
        private int? GetCurrentUserId()
        {
            // 🔥 目前 CreatedBy 設為 null，不需要使用此方法
            // 如果未來需要記錄使用者，可以在這裡實作
            return null;
        }
    }
}