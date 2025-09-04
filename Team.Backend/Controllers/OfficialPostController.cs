using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Team.Backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Team.Backend.Controllers
{
    [Route("Blog")]
    public class OfficialPostController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger<OfficialPostController> _logger;

        public OfficialPostController(AppDbContext context, Cloudinary cloudinary, IServiceProvider serviceProvider, ILogger<OfficialPostController> logger)
            : base(context, logger)
        {
            _context = context;
            _cloudinary = cloudinary;
            _scheduleService = ScheduleServiceFactory.CreateService(context, serviceProvider);
            _logger = logger;
        }

        // 清除 TempData 訊息的私有方法
        private void ClearTempDataMessages()
        {
            TempData.Remove("Success");
            TempData.Remove("Error");
        }

        [Route("PostManagement")]
        public async Task<IActionResult> PostManagement()
        {
            var officialPosts = await _context.OfficialPosts
                .Include(a => a.OfficialPostImages)
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync();
            return View(officialPosts);
        }

        [Route("AddArticles")]
        public async Task<IActionResult> AddArticles()
        {
            ClearTempDataMessages();

            ViewBag.CanSchedule = await _scheduleService.IsAvailable();
            ViewBag.SystemType = _scheduleService.GetSystemType();

            return View(new OfficialPostViewModel());
        }

        // 排程管理頁面
        [Route("ScheduleManagement")]
        public async Task<IActionResult> ScheduleManagement()
        {
            // ✅ 使用新的方法名稱，只取得文章相關的排程
            var schedules = await _scheduleService.GetScheduledTasksAsync("official_post");
            ViewBag.SystemType = _scheduleService.GetSystemType();
            return View(schedules);
        }

        // 取消排程的 API
        [HttpPost]
        [Route("CancelSchedule")]
        public async Task<IActionResult> CancelSchedule(int scheduleId)
        {
            try
            {
                bool success = await _scheduleService.CancelScheduleAsync(scheduleId);
                return Json(new { success = success, message = success ? "排程已取消" : "取消失敗" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [Route("EditArticle/{id:int}")]
        public async Task<IActionResult> EditArticle(int id)
        {
            var article = await _context.OfficialPosts
                .Include(a => a.OfficialPostImages)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null)
            {
                System.Diagnostics.Debug.WriteLine($"未找到 ID 為 {id} 的文章");
                return NotFound();
            }

            // ✅ 查詢是否有相關的排程
            var schedule = await _context.ContentPublishingSchedules
                .FirstOrDefaultAsync(s => s.ContentId == id &&
                                   s.Status == "pending" &&
                                   s.ContentType == "official_post");

            var viewModel = new OfficialPostViewModel
            {
                Id = article.Id,
                Title = article.Title,
                Content = article.Content,
                Status = article.Status,
                Category = article.Category,
                CoverImage = article.CoverImage,
                PublishedAt = article.PublishedAt,
                CreatedBy = article.CreatedBy,
                CreatedAt = article.CreatedAt,
                UpdatedAt = article.UpdatedAt,
                UploadedImageUrls = article.OfficialPostImages?.Select(i => i.ImagePath).ToList() ?? new List<string>(),

                // ✅ 設定排程相關屬性
                PublishType = schedule != null ? "scheduled" :
                             (article.Status == "published" ? "immediate" : ""),
                ScheduledTime = schedule?.ScheduledTime
            };

            // ✅ 傳遞排程功能資訊到 View
            ViewBag.CanSchedule = await _scheduleService.IsAvailable();
            ViewBag.SystemType = _scheduleService.GetSystemType();

            return View("AddArticles", viewModel);
        }

        [Route("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var article = await _context.OfficialPosts
                .Include(a => a.OfficialPostImages)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null)
                return NotFound();

            var viewModel = new OfficialPostViewModel
            {
                Id = article.Id,
                Title = article.Title,
                Content = article.Content,
                Status = article.Status,
                Category = article.Category,
                CoverImage = article.CoverImage,
                PublishedAt = article.PublishedAt,
                CreatedBy = article.CreatedBy,
                CreatedAt = article.CreatedAt,
                UpdatedAt = article.UpdatedAt,
                UploadedImageUrls = article.OfficialPostImages?.Select(i => i.ImagePath).ToList() ?? new()
            };

            return View(viewModel);
        }

        [HttpPost]
        [Route("DeleteArticle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            try
            {
                var article = await _context.OfficialPosts
                    .Include(a => a.OfficialPostImages)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (article == null)
                {
                    TempData["Error"] = "文章不存在";
                    return RedirectToAction("PostManagement");
                }

                // ✅ 如果文章有相關排程，先取消排程
                var relatedSchedule = await _context.ContentPublishingSchedules
                    .FirstOrDefaultAsync(s => s.ContentId == id &&
                                       s.Status == "pending" &&
                                       s.ContentType == "official_post");

                if (relatedSchedule != null)
                {
                    try
                    {
                        await _scheduleService.CancelScheduleAsync(relatedSchedule.Id);
                        Console.WriteLine($"✅ 已取消文章相關排程: {relatedSchedule.Id}");
                    }
                    catch (Exception cancelEx)
                    {
                        Console.WriteLine($"⚠️ 取消排程時發生錯誤: {cancelEx.Message}");
                    }
                }

                // 刪除 Cloudinary 圖片
                if (article.OfficialPostImages != null && article.OfficialPostImages.Any())
                {
                    foreach (var image in article.OfficialPostImages)
                    {
                        if (!string.IsNullOrEmpty(image.ImagePath) && image.ImagePath.Contains("cloudinary.com"))
                        {
                            await DeleteCloudinaryImage(image.ImagePath);
                            Console.WriteLine($"已刪除 Cloudinary 圖片: {image.ImagePath}");
                        }
                    }
                }

                // 刪除封面圖片
                if (!string.IsNullOrEmpty(article.CoverImage) && article.CoverImage.Contains("cloudinary.com"))
                {
                    await DeleteCloudinaryImage(article.CoverImage);
                    Console.WriteLine($"已刪除封面圖片: {article.CoverImage}");
                }

                _context.OfficialPosts.Remove(article);
                await _context.SaveChangesAsync();

                TempData["Success"] = "文章已成功刪除！";
                return RedirectToAction("PostManagement");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"刪除時發生錯誤：{ex.Message}";
                return RedirectToAction("PostManagement");
            }
        }

        // ✅ 完整修正的 SaveArticle 方法
        [HttpPost]
        [Route("SaveArticle")]
        public async Task<IActionResult> SaveArticle(OfficialPostViewModel model)
        {
            try
            {
                Console.WriteLine("=== SaveArticle 開始 ===");
                Console.WriteLine($"PublishType: {model.PublishType}, ScheduledTime: {model.ScheduledTime}");

                // 基本驗證
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    TempData["Error"] = "文章標題不能為空";
                    ViewBag.CanSchedule = await _scheduleService.IsAvailable();
                    ViewBag.SystemType = _scheduleService.GetSystemType();
                    return View("AddArticles", model);
                }

                if (string.IsNullOrWhiteSpace(model.Content))
                {
                    TempData["Error"] = "文章內容不能為空";
                    ViewBag.CanSchedule = await _scheduleService.IsAvailable();
                    ViewBag.SystemType = _scheduleService.GetSystemType();
                    return View("AddArticles", model);
                }

                // 檢查 CreatedBy 是否有效
                try
                {
                    var userExists = await _context.Users.AnyAsync(u => u.Id == model.CreatedBy);
                    if (!userExists)
                    {
                        Console.WriteLine($"⚠️ 使用者 ID {model.CreatedBy} 不存在，改用預設值 1");
                        model.CreatedBy = 1;
                    }
                }
                catch (Exception userEx)
                {
                    Console.WriteLine($"❌ 檢查使用者時發生錯誤: {userEx.Message}");
                    model.CreatedBy = 1;
                }

                // 處理圖片 URL
                var validImageUrls = model.UploadedImageUrls?
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToList() ?? new List<string>();

                var now = DateTime.Now;

                if (model.Id == 0) // 新增文章
                {
                    Console.WriteLine("📝 執行新增文章操作");

                    // ✅ 根據發布類型決定文章狀態
                    string articleStatus;
                    if (model.PublishType == "immediate")
                    {
                        articleStatus = "published";
                    }
                    else if (model.PublishType == "scheduled" && model.ScheduledTime.HasValue)
                    {
                        articleStatus = "scheduled";
                    }
                    else
                    {
                        articleStatus = string.IsNullOrWhiteSpace(model.Status) ? "draft" : model.Status;
                    }

                    var newArticle = new OfficialPost
                    {
                        Title = model.Title.Trim(),
                        Content = model.Content.Trim(),
                        Category = string.IsNullOrWhiteSpace(model.Category) ? "其他" : model.Category.Trim(),
                        CoverImage = string.IsNullOrWhiteSpace(model.CoverImage) ? null : model.CoverImage.Trim(),
                        Status = articleStatus,
                        CreatedBy = model.CreatedBy,
                        CreatedAt = now,
                        UpdatedAt = now,
                        PublishedAt = articleStatus == "published" ? now : null
                    };

                    _context.OfficialPosts.Add(newArticle);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✅ 文章新增成功！ID: {newArticle.Id}");

                    // 新增圖片記錄
                    if (validImageUrls.Any())
                    {
                        var imageRecords = validImageUrls.Select((url, index) => new OfficialPostImage
                        {
                            PostId = newArticle.Id,
                            ImagePath = url.Trim(),
                            SortOrder = index + 1
                        }).ToList();

                        _context.OfficialPostImages.AddRange(imageRecords);
                        await _context.SaveChangesAsync();
                        Console.WriteLine("✅ 圖片記錄新增成功！");
                    }

                    // ✅ 處理排程發布
                    if (model.PublishType == "scheduled" && model.ScheduledTime.HasValue)
                    {
                        try
                        {
                            // ✅ 使用新的方法名稱
                            var scheduleId = await _scheduleService.ScheduleTaskAsync(
                                "official_post",        // 內容類型
                                newArticle.Id,          // 內容 ID
                                model.ScheduledTime.Value,
                                model.CreatedBy
                            );

                            Console.WriteLine($"✅ 排程建立成功，ID: {scheduleId}");
                            TempData["Success"] = $"文章已排程於 {model.ScheduledTime:yyyy/MM/dd HH:mm} 發布！";
                        }
                        catch (Exception scheduleEx)
                        {
                            Console.WriteLine($"❌ 建立排程失敗: {scheduleEx.Message}");
                            newArticle.Status = "draft";
                            await _context.SaveChangesAsync();
                            TempData["Warning"] = "文章已儲存為草稿，但排程設定失敗。請手動重新設定排程。";
                        }
                    }
                    else
                    {
                        string successMessage = articleStatus switch
                        {
                            "published" => "文章已成功發布！",
                            "draft" => "文章已儲存為草稿！",
                            _ => "文章已成功儲存！"
                        };
                        TempData["Success"] = successMessage;
                    }
                }
                else // 編輯文章
                {
                    Console.WriteLine($"📝 執行編輯文章操作，ID: {model.Id}");

                    var existingArticle = await _context.OfficialPosts
                        .Include(p => p.OfficialPostImages)
                        .FirstOrDefaultAsync(p => p.Id == model.Id);

                    if (existingArticle == null)
                    {
                        TempData["Error"] = "找不到要編輯的文章";
                        return NotFound();
                    }

                    // ✅ 處理狀態變更和排程
                    string newStatus;
                    if (model.PublishType == "immediate")
                    {
                        newStatus = "published";
                    }
                    else if (model.PublishType == "scheduled" && model.ScheduledTime.HasValue)
                    {
                        newStatus = "scheduled";
                    }
                    else
                    {
                        newStatus = string.IsNullOrWhiteSpace(model.Status) ? existingArticle.Status : model.Status;
                    }

                    // ✅ 如果原本是排程狀態，現在改為其他狀態，取消舊的排程
                    if (existingArticle.Status == "scheduled" && newStatus != "scheduled")
                    {
                        try
                        {
                            var existingSchedule = await _context.ContentPublishingSchedules
                                .FirstOrDefaultAsync(s => s.ContentId == existingArticle.Id &&
                                                   s.Status == "pending" &&
                                                   s.ContentType == "official_post");

                            if (existingSchedule != null)
                            {
                                await _scheduleService.CancelScheduleAsync(existingSchedule.Id);
                                Console.WriteLine($"✅ 已取消舊的排程: {existingSchedule.Id}");
                            }
                        }
                        catch (Exception cancelEx)
                        {
                            Console.WriteLine($"⚠️ 取消舊排程時發生錯誤: {cancelEx.Message}");
                        }
                    }

                    // 處理封面圖片變更
                    if (!string.IsNullOrEmpty(existingArticle.CoverImage) &&
                        existingArticle.CoverImage != model.CoverImage &&
                        existingArticle.CoverImage.Contains("cloudinary.com"))
                    {
                        await DeleteCloudinaryImage(existingArticle.CoverImage);
                    }

                    // 更新文章
                    existingArticle.Title = model.Title.Trim();
                    existingArticle.Content = model.Content.Trim();
                    existingArticle.Category = string.IsNullOrWhiteSpace(model.Category) ? existingArticle.Category : model.Category.Trim();
                    existingArticle.CoverImage = string.IsNullOrWhiteSpace(model.CoverImage) ? null : model.CoverImage.Trim();
                    existingArticle.Status = newStatus;
                    existingArticle.UpdatedAt = now;

                    // ✅ 處理發布時間
                    if (existingArticle.Status != "published" && newStatus == "published")
                    {
                        existingArticle.PublishedAt = now;
                    }
                    else if (newStatus == "scheduled")
                    {
                        existingArticle.PublishedAt = null;
                    }

                    // 處理圖片更新
                    if (existingArticle.OfficialPostImages != null && existingArticle.OfficialPostImages.Any())
                    {
                        foreach (var oldImage in existingArticle.OfficialPostImages)
                        {
                            if (!string.IsNullOrEmpty(oldImage.ImagePath) &&
                                oldImage.ImagePath.Contains("cloudinary.com") &&
                                !validImageUrls.Contains(oldImage.ImagePath))
                            {
                                await DeleteCloudinaryImage(oldImage.ImagePath);
                            }
                        }
                        _context.OfficialPostImages.RemoveRange(existingArticle.OfficialPostImages);
                    }

                    if (validImageUrls.Any())
                    {
                        var newImageRecords = validImageUrls.Select((url, index) => new OfficialPostImage
                        {
                            PostId = existingArticle.Id,
                            ImagePath = url.Trim(),
                            SortOrder = index + 1
                        }).ToList();

                        _context.OfficialPostImages.AddRange(newImageRecords);
                    }

                    await _context.SaveChangesAsync();
                    Console.WriteLine("✅ 文章更新成功！");

                    // ✅ 處理新的排程
                    if (model.PublishType == "scheduled" && model.ScheduledTime.HasValue)
                    {
                        try
                        {
                            // ✅ 使用新的方法名稱
                            var scheduleId = await _scheduleService.ScheduleTaskAsync(
                                "official_post",
                                existingArticle.Id,
                                model.ScheduledTime.Value,
                                model.CreatedBy
                            );

                            Console.WriteLine($"✅ 新排程建立成功，ID: {scheduleId}");
                            TempData["Success"] = $"文章已重新排程於 {model.ScheduledTime:yyyy/MM/dd HH:mm} 發布！";
                        }
                        catch (Exception scheduleEx)
                        {
                            Console.WriteLine($"❌ 建立新排程失敗: {scheduleEx.Message}");
                            TempData["Warning"] = "文章已更新，但排程設定失敗。請手動重新設定排程。";
                        }
                    }
                    else
                    {
                        string successMessage = newStatus switch
                        {
                            "published" => "文章已成功發布！",
                            "draft" => "文章已更新為草稿！",
                            _ => "文章已成功更新！"
                        };
                        TempData["Success"] = successMessage;
                    }
                }

                Console.WriteLine("=== SaveArticle 成功完成 ===");
                return RedirectToAction("PostManagement");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 錯誤: {ex.Message}");
                TempData["Error"] = $"儲存失敗：{ex.Message}";
                ViewBag.CanSchedule = await _scheduleService.IsAvailable();
                ViewBag.SystemType = _scheduleService.GetSystemType();
                return View("AddArticles", model);
            }
        }

        // 其他方法保持不變...
        private async Task<string?> UploadToCloudinary(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                    return null;

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
                {
                    Console.WriteLine($"不支援的檔案類型: {imageFile.ContentType}");
                    return null;
                }

                if (imageFile.Length > 15 * 1024 * 1024)
                {
                    Console.WriteLine($"檔案過大: {imageFile.Length} bytes");
                    return null;
                }

                using var stream = imageFile.OpenReadStream();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    PublicId = $"jade_article_{DateTime.Now.Ticks}",
                    Folder = "jade-articles",
                    Transformation = new Transformation()
                        .Width(1200).Height(800).Crop("limit")
                        .Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"✅ 文章圖片上傳成功: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    Console.WriteLine($"❌ 文章圖片上傳失敗: {uploadResult.Error?.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 文章圖片上傳過程發生錯誤: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> DeleteCloudinaryImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("cloudinary.com"))
                    return true;

                var uri = new Uri(imageUrl);
                var pathParts = uri.AbsolutePath.Split('/');
                var versionIndex = Array.FindIndex(pathParts, part => part.StartsWith("v"));

                if (versionIndex > 0 && versionIndex < pathParts.Length - 1)
                {
                    var publicIdParts = pathParts.Skip(versionIndex + 1).ToArray();
                    var publicId = string.Join("/", publicIdParts);

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
                        Console.WriteLine($"✅ Cloudinary 文章圖片刪除成功: {publicId}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Cloudinary 文章圖片刪除失敗: {result.Error?.Message}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刪除 Cloudinary 文章圖片時發生錯誤: {ex.Message}");
                return false;
            }
        }

        [HttpPost]
        [Route("UploadImage")]
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
                    return Json(new { success = false, message = "上傳失敗，請檢查檔案格式和大小" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AJAX 圖片上傳錯誤: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}