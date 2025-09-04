using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using CloudinaryDotNet; 
using CloudinaryDotNet.Actions; 

namespace Team.Backend.Controllers
{
    public class AdManageController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<AdManageController> _logger;

        public AdManageController(AppDbContext context, Cloudinary cloudinary, ILogger<AdManageController> logger) 
            : base(context, logger)
        {
            _context = context;
            _cloudinary = cloudinary;
            _logger = logger;
        }


        public async Task<IActionResult> Banner()
        {
            // 🔥 排除彈出廣告，只顯示一般廣告
            var banners = await _context.Banners
                .Where(b => b.Position != "popup" &&
                           b.Position != "splash" &&
                           b.Position != "彈出式" &&
                           b.Position != "彈窗" &&
                           !b.Position.StartsWith("彈"))
                .OrderByDescending(b => b.UpdatedAt)
                .ToListAsync();

            return View(banners);
        }

        // GET: 新增
        public IActionResult Create()
        {
            return View();
        }

        // GET: 顯示編輯表單
        public async Task<IActionResult> Edit(int id)
        {
            Console.WriteLine($"=== Edit action 被呼叫，ID: {id} ===");

            // 🔥 確保只編輯一般廣告，不包含彈出廣告
            var banner = await _context.Banners
                .Where(b => b.Id == id &&
                           b.Position != "popup" &&
                           b.Position != "splash" &&
                           b.Position != "彈出式" &&
                           b.Position != "彈窗" &&
                           !b.Position.StartsWith("彈"))
                .FirstOrDefaultAsync();

            if (banner == null)
            {
                Console.WriteLine($"❌ 找不到 ID {id} 的一般 Banner");
                TempData["Error"] = "找不到要編輯的 Banner，或這是彈出廣告請到彈出廣告管理頁面編輯";
                return RedirectToAction("Banner");
            }

            Console.WriteLine($"✅ 找到一般 Banner: {banner.Title}");
            return View("Create", banner);
        }

        // GET: 顯示詳細資料
        public async Task<IActionResult> Details(int id)
        {
            // 🔥 確保只顯示一般廣告詳情
            var banner = await _context.Banners
                .Where(b => b.Id == id &&
                           b.Position != "popup" &&
                           b.Position != "splash" &&
                           b.Position != "彈出式" &&
                           b.Position != "彈窗" &&
                           !b.Position.StartsWith("彈"))
                .FirstOrDefaultAsync();

            if (banner == null)
            {
                TempData["Error"] = "找不到該 Banner，或這是彈出廣告請到彈出廣告管理頁面查看";
                return RedirectToAction("Banner");
            }

            return View(banner);
        }

        // POST: 刪除 Banner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // 🔥 確保只刪除一般廣告
                var banner = await _context.Banners
                    .Where(b => b.Id == id &&
                               b.Position != "popup" &&
                               b.Position != "splash" &&
                               b.Position != "彈出式" &&
                               b.Position != "彈窗" &&
                               !b.Position.StartsWith("彈"))
                    .FirstOrDefaultAsync();

                if (banner == null)
                {
                    TempData["Error"] = "Banner 不存在，或這是彈出廣告請到彈出廣告管理頁面刪除";
                    return RedirectToAction("Banner");
                }

                // 🔥 刪除 Cloudinary 圖片
                if (!string.IsNullOrEmpty(banner.ImageUrl) && banner.ImageUrl.Contains("cloudinary.com"))
                {
                    await DeleteCloudinaryImage(banner.ImageUrl);
                }

                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();

                TempData["Success"] = "一般廣告 Banner 已成功刪除！";
                return RedirectToAction("Banner");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"刪除時發生錯誤：{ex.Message}";
                return RedirectToAction("Banner");
            }
        }

        // POST: 儲存 Banner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Banner model, IFormFile ImageFile)
        {
            TempData["Debug"] = $"收到資料 - Id:{model.Id}, Title:'{model.Title}', ImageFile:{(ImageFile?.FileName ?? "無檔案")}";
            try
            {
                Console.WriteLine("=== SaveBanner 詳細除錯開始 ===");
                Console.WriteLine($"模型狀態有效: {ModelState.IsValid}");

                // 基本驗證
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    Console.WriteLine("❌ 標題為空");
                    TempData["Error"] = "Banner 標題不能為空";
                    return View("Create", model);
                }

                if (string.IsNullOrWhiteSpace(model.Page))
                {
                    Console.WriteLine("❌ 頁面為空");
                    TempData["Error"] = "請選擇頁面";
                    return View("Create", model);
                }

                if (string.IsNullOrWhiteSpace(model.Position))
                {
                    Console.WriteLine("❌ 位置為空");
                    TempData["Error"] = "請選擇位置";
                    return View("Create", model);
                }

                var now = DateTime.Now;

                if (model.Id == 0) // 新增 Banner
                {
                    Console.WriteLine("📝 執行新增 Banner 操作");

                    // 檢查圖片檔案
                    if (ImageFile == null || ImageFile.Length == 0)
                    {
                        Console.WriteLine("❌ 沒有選擇圖片檔案");
                        TempData["Error"] = "請選擇 Banner 圖片";
                        return View("Create", model);
                    }

                    // 🔥 使用 Cloudinary 上傳圖片
                    var imageUrl = await UploadToCloudinary(ImageFile);
                    if (imageUrl == null)
                    {
                        TempData["Error"] = "圖片上傳失敗，請檢查檔案格式和大小";
                        return View("Create", model);
                    }

                    var newBanner = new Banner
                    {
                        Title = model.Title.Trim(),
                        Page = model.Page.Trim(),
                        Position = model.Position.Trim(),
                        ImageUrl = imageUrl, // Cloudinary URL
                        LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim(),
                        ProductId = model.ProductId,
                        Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                        DisplayOrder = model.DisplayOrder ?? 1,
                        IsActive = model.IsActive ?? true,
                        StartTime = model.StartTime,
                        EndTime = model.EndTime,
                        ClickCount = 0,
                        CreatedBy = null,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    Console.WriteLine($"準備新增 Banner，圖片URL: {imageUrl}");

                    _context.Banners.Add(newBanner);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✅ Banner 新增成功！ID: {newBanner.Id}");
                }
                else // 編輯 Banner
                {
                    Console.WriteLine($"📝 執行編輯 Banner 操作，ID: {model.Id}");

                    var existingBanner = await _context.Banners
                        .FirstOrDefaultAsync(b => b.Id == model.Id);

                    if (existingBanner == null)
                    {
                        Console.WriteLine($"❌ 找不到 ID {model.Id} 的 Banner");
                        TempData["Error"] = "找不到要編輯的 Banner";
                        return NotFound();
                    }

                    // 🔥 處理圖片更新
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        // 刪除舊的 Cloudinary 圖片
                        if (!string.IsNullOrEmpty(existingBanner.ImageUrl) && existingBanner.ImageUrl.Contains("cloudinary.com"))
                        {
                            await DeleteCloudinaryImage(existingBanner.ImageUrl);
                        }

                        // 上傳新圖片
                        var newImageUrl = await UploadToCloudinary(ImageFile);
                        if (newImageUrl != null)
                        {
                            existingBanner.ImageUrl = newImageUrl;
                            Console.WriteLine($"已更新圖片: {newImageUrl}");
                        }
                        else
                        {
                            TempData["Error"] = "新圖片上傳失敗";
                            return View("Create", model);
                        }
                    }

                    // 更新其他欄位
                    existingBanner.Title = model.Title.Trim();
                    existingBanner.Page = model.Page.Trim();
                    existingBanner.Position = model.Position.Trim();
                    existingBanner.LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim();
                    existingBanner.ProductId = model.ProductId;
                    existingBanner.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
                    existingBanner.DisplayOrder = model.DisplayOrder ?? existingBanner.DisplayOrder;
                    existingBanner.IsActive = model.IsActive ?? existingBanner.IsActive;
                    existingBanner.StartTime = model.StartTime;
                    existingBanner.EndTime = model.EndTime;
                    existingBanner.UpdatedAt = now;

                    await _context.SaveChangesAsync();
                    Console.WriteLine("✅ Banner 更新成功！");
                }

                TempData["Success"] = "Banner 已成功儲存！";
                return RedirectToAction("Banner");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 錯誤: {ex.Message}");
                TempData["Error"] = $"儲存失敗：{ex.Message}";
                return View("Create", model);
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
                    Console.WriteLine($"不支援的檔案類型: {imageFile.ContentType}");
                    return null;
                }

                // 檢查檔案大小 (10MB)
                if (imageFile.Length > 10 * 1024 * 1024)
                {
                    Console.WriteLine($"檔案過大: {imageFile.Length} bytes");
                    return null;
                }

                using var stream = imageFile.OpenReadStream();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    PublicId = $"jade_banner_{DateTime.Now.Ticks}",
                    Folder = "jade-banners", // 使用專門的資料夾
                    Transformation = new Transformation()
                        .Width(1200).Height(628).Crop("fill") // 固定廣告尺寸
                        .Gravity("center")
                        .Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"✅ 圖片上傳成功: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    Console.WriteLine($"❌ 圖片上傳失敗: {uploadResult.Error?.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 上傳過程發生錯誤: {ex.Message}");
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
                        Console.WriteLine($"✅ Cloudinary 圖片刪除成功: {publicId}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Cloudinary 圖片刪除失敗: {result.Error?.Message}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刪除 Cloudinary 圖片時發生錯誤: {ex.Message}");
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

        // ... 其他現有方法保持不變 ...

        // GET: 顯示彈出式廣告管理頁面
        public async Task<IActionResult> Splash()
        {
            var splashBanners = await _context.Banners
                .Where(b => b.Position.StartsWith("彈窗") || b.Position.StartsWith("彈出"))
                .OrderByDescending(b => b.UpdatedAt)
                .ToListAsync();

            return View(splashBanners);
        }

        // GET: 顯示新增彈出式廣告表單
        public IActionResult CreateSplash()
        {
            return View();
        }

        // GET: 顯示編輯彈出式廣告表單
        public async Task<IActionResult> EditSplash(int id)
        {
            var banner = await _context.Banners
                .FirstOrDefaultAsync(b => b.Id == id);

            if (banner == null)
            {
                TempData["Error"] = "找不到要編輯的彈出式廣告";
                return RedirectToAction("Splash");
            }

            return View("CreateSplash", banner);
        }

        // API: 取得彈出式廣告資料
        [HttpGet]
        public async Task<IActionResult> GetSplashBanners(string page = "全站")
        {
            var now = DateTime.Now;

            var splashBanners = await _context.Banners
                .Where(b => b.IsActive == true)
                .Where(b => b.Position.StartsWith("彈窗") || b.Position.StartsWith("彈出"))
                .Where(b => b.Page == page || b.Page == "全站")
                .Where(b => b.StartTime == null || b.StartTime <= now)
                .Where(b => b.EndTime == null || b.EndTime >= now)
                .OrderBy(b => b.DisplayOrder)
                .Select(b => new {
                    id = b.Id,
                    title = b.Title,
                    description = b.Description,
                    imageUrl = b.ImageUrl,
                    linkUrl = b.LinkUrl,
                    position = b.Position,
                    page = b.Page
                })
                .ToListAsync();

            return Json(splashBanners);
        }

        // API: 記錄彈窗點擊數
        [HttpPost]
        public async Task<IActionResult> TrackSplashClick(int id)
        {
            try
            {
                var banner = await _context.Banners.FindAsync(id);
                if (banner != null)
                {
                    banner.ClickCount = (banner.ClickCount ?? 0) + 1;
                    banner.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, clickCount = banner.ClickCount });
                }

                return Json(new { success = false, message = "找不到廣告" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: 儲存彈出式廣告
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSplash(Banner model, IFormFile ImageFile)
        {
            // 重用 Save 方法的邏輯
            var result = await Save(model, ImageFile);

            if (result is RedirectToActionResult redirectResult && redirectResult.ActionName == "Banner")
            {
                return RedirectToAction("Splash"); // 導向彈窗管理頁面
            }

            if (result is ViewResult viewResult)
            {
                return View("CreateSplash", viewResult.Model);
            }

            return result;
        }
    }
}