using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using System.IO;
using Team.API.DTO;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Team.API.Extensions;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<PostsController> _logger;

        public PostsController(AppDbContext context, Cloudinary cloudinary, ILogger<PostsController> logger)
        {
            _context = context;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        // GET: api/Posts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PostResponseDto>>> GetPosts()
        {
            try
            {
                _logger.LogInformation("=== GetPosts 開始 ===");

                var posts = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.MemberProfile)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation($"從資料庫取得 {posts.Count} 個貼文");

                var postDtos = posts.Select(p =>
                {
                    var dto = p.ToDto();
                    _logger.LogDebug($"處理貼文: {dto.Title}, 狀態: '{dto.Status}'");
                    return dto;
                }).ToList();

                // 🔥 統計各狀態貼文數量
                var statusCounts = postDtos.GroupBy(p => p.Status?.ToLower())
                    .ToDictionary(g => g.Key ?? "unknown", g => g.Count());

                _logger.LogInformation("📊 貼文狀態統計:");
                foreach (var kvp in statusCounts)
                {
                    _logger.LogInformation($"  {kvp.Key}: {kvp.Value} 個");
                }

                _logger.LogInformation($"✅ 回傳 {postDtos.Count} 個貼文 DTO");

                return Ok(postDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GetPosts 失敗");
                return StatusCode(500, new { message = "取得貼文失敗", error = ex.Message });
            }
        }

        // GET: api/Posts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PostResponseDto>> GetPost(int id)
        {
            try
            {
                _logger.LogInformation($"=== GetPost 開始，ID: {id} ===");

                var post = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.MemberProfile)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的貼文");
                    return NotFound(new { message = "找不到指定的貼文" });
                }

                var dto = post.ToDto();
                _logger.LogInformation($"✅ 成功取得貼文: {dto.Title}");

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"取得 ID 為 {id} 的貼文失敗");
                return StatusCode(500, new { message = "取得貼文失敗", error = ex.Message });
            }
        }

        // POST: api/Posts
        [HttpPost]
        public async Task<ActionResult<PostResponseDto>> CreatePost([FromForm] CreatePostDto postDto, IFormFile imageFile)
        {
            try
            {
                _logger.LogInformation("=== CreatePost 開始 ===");

                // 🔥 詳細記錄接收的資料
                LogPostCreationDetails(postDto, imageFile);

                // 🔥 基本驗證
                var validationResult = ValidatePostData(postDto);
                if (validationResult != null)
                {
                    return validationResult;
                }

                // 🔥 狀態處理
                var status = NormalizeStatus(postDto.Status);
                var statusInfo = GetStatusInfo(status);

                _logger.LogInformation($"🔍 貼文類型: {statusInfo.Description}");

                // 🔥 圖片處理
                string imagePath = null;
                if (imageFile != null && imageFile.Length > 0)
                {
                    _logger.LogInformation("📷 開始處理圖片上傳");
                    imagePath = await UploadToCloudinary(imageFile);
                    if (imagePath == null)
                    {
                        _logger.LogError("❌ 圖片上傳失敗");
                        return BadRequest(new { message = "圖片上傳失敗，請檢查檔案格式和大小" });
                    }
                    _logger.LogInformation($"✅ 圖片上傳成功: {imagePath}");
                }
                else if (statusInfo.RequiresImage)
                {
                    _logger.LogWarning("❌ 發布狀態需要圖片");
                    return BadRequest(new { message = statusInfo.ImageRequiredMessage });
                }

                // 🔥 建立貼文
                var post = CreatePostEntity(postDto, imagePath, status, statusInfo.IsPublished);

                _logger.LogInformation($"💾 準備儲存貼文: Status={post.Status}");

                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 貼文已儲存，ID: {post.Id}");

                // 🔥 載入完整資料並回傳
                var responseDto = await GetPostWithRelatedData(post.Id);
                if (responseDto == null)
                {
                    _logger.LogError("❌ 無法載入已建立的貼文");
                    return StatusCode(500, new { message = "建立貼文後無法載入資料" });
                }

                _logger.LogInformation($"✅ CreatePost 成功完成，回傳貼文 ID: {responseDto.Id}");

                return CreatedAtAction("GetPost", new { id = post.Id }, responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CreatePost 發生異常");
                return StatusCode(500, new { message = "建立貼文失敗", error = ex.Message });
            }
        }

        // PUT: api/Posts/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(int id, [FromForm] UpdatePostDto updatePostDto, IFormFile? imageFile)
        {
            try
            {
                _logger.LogInformation($"=== UpdatePost 開始，ID: {id} ===");

                // 🔥 記錄接收的資料
                _logger.LogInformation($"Title: '{updatePostDto?.Title}'");
                _logger.LogInformation($"Content: '{updatePostDto?.Content}' (長度: {updatePostDto?.Content?.Length})");
                _logger.LogInformation($"Status: '{updatePostDto?.Status}'");
                _logger.LogInformation($"MembersId: {updatePostDto?.MembersId}");
                _logger.LogInformation($"imageFile 是否為 null: {imageFile == null}");
                if (imageFile != null)
                {
                    _logger.LogInformation($"imageFile 檔案名: {imageFile.FileName}, 大小: {imageFile.Length} bytes");
                }

                // 🔥 查詢現有貼文
                var existingPost = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.MemberProfile)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingPost == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的貼文");
                    return NotFound(new { Success = false, Message = "貼文不存在" });
                }

                _logger.LogInformation($"找到現有貼文: {existingPost.Title}，目前狀態: {existingPost.Status}，目前圖片: {existingPost.Image}");

                // 🔥 基本驗證
                if (string.IsNullOrEmpty(updatePostDto?.Title))
                {
                    return BadRequest(new { Success = false, Message = "標題為必填欄位" });
                }

                if (string.IsNullOrEmpty(updatePostDto?.Content))
                {
                    return BadRequest(new { Success = false, Message = "內容為必填欄位" });
                }

                // 🔥 處理圖片更新邏輯
                string? newImageUrl = existingPost.Image; // 預設保持現有圖片

                if (imageFile != null && imageFile.Length > 0)
                {
                    _logger.LogInformation("📷 偵測到新圖片，開始處理上傳");

                    // 上傳新圖片
                    newImageUrl = await UploadToCloudinary(imageFile);
                    if (newImageUrl == null)
                    {
                        _logger.LogError("❌ 新圖片上傳失敗");
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "圖片上傳失敗，請檢查檔案格式和大小"
                        });
                    }

                    // 刪除舊圖片（如果存在且不同於新圖片）
                    if (!string.IsNullOrEmpty(existingPost.Image) && existingPost.Image != newImageUrl)
                    {
                        _logger.LogInformation("🗑️ 刪除舊圖片");
                        await DeleteCloudinaryImage(existingPost.Image);
                    }

                    _logger.LogInformation($"✅ 圖片更新成功: {newImageUrl}");
                }
                else
                {
                    _logger.LogInformation("📷 沒有新圖片，保持現有圖片");

                    // 🔥 重要：檢查是否必須要有圖片
                    if (string.IsNullOrEmpty(existingPost.Image))
                    {
                        _logger.LogWarning("❌ 貼文沒有現有圖片且沒有上傳新圖片");
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "貼文必須包含圖片，請上傳圖片"
                        });
                    }
                }

                // 🔥 狀態處理
                var newStatus = NormalizeStatus(updatePostDto.Status ?? existingPost.Status);

                // 🔥 更新貼文資料
                existingPost.Title = updatePostDto.Title;
                existingPost.Content = updatePostDto.Content;
                existingPost.Image = newImageUrl;
                existingPost.Status = newStatus;
                existingPost.UpdatedAt = DateTime.UtcNow;

                // 🔥 如果狀態變為發布，設定發布時間
                if (newStatus == PostStatus.Published && existingPost.PublishedAt == null)
                {
                    existingPost.PublishedAt = DateTime.UtcNow;
                    _logger.LogInformation("📅 設定發布時間");
                }

                // 🔥 儲存變更
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 貼文更新成功，新狀態: {existingPost.Status}");

                // 🔥 回傳更新後的貼文資料
                var responseDto = existingPost.ToDto();

                return Ok(new
                {
                    Success = true,
                    Message = "貼文更新成功",
                    Data = responseDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新 ID 為 {id} 的貼文失敗");
                return BadRequest(new
                {
                    Success = false,
                    Message = $"更新失敗: {ex.Message}"
                });
            }
        }

        // 🔥 記錄更新貼文詳細資訊
        private void LogUpdatePostDetails(int id, UpdatePostDto? updatePostDto, IFormFile? imageFile)
        {
            _logger.LogInformation($"更新貼文 ID: {id}");
            _logger.LogInformation($"updatePostDto == null: {updatePostDto == null}");

            if (updatePostDto != null)
            {
                _logger.LogInformation($"Title: '{updatePostDto.Title}' (長度: {updatePostDto.Title?.Length})");
                _logger.LogInformation($"Content: '{updatePostDto.Content}' (長度: {updatePostDto.Content?.Length})");
                _logger.LogInformation($"Status: '{updatePostDto.Status}'");
                _logger.LogInformation($"MembersId: {updatePostDto.MembersId}");
            }

            _logger.LogInformation($"imageFile == null: {imageFile == null}");
            if (imageFile != null)
            {
                _logger.LogInformation($"imageFile.FileName: '{imageFile.FileName}'");
                _logger.LogInformation($"imageFile.Length: {imageFile.Length} bytes");
            }
        }

        // 🔥 驗證更新貼文資料
        private ActionResult? ValidateUpdatePostData(UpdatePostDto? updatePostDto)
        {
            if (updatePostDto == null)
            {
                _logger.LogWarning("❌ updatePostDto 為 null");
                return BadRequest(new { Success = false, Message = "貼文資料不能為空" });
            }

            if (string.IsNullOrEmpty(updatePostDto.Title))
            {
                _logger.LogWarning("❌ 標題為空");
                return BadRequest(new { Success = false, Message = "標題為必填欄位" });
            }

            if (string.IsNullOrEmpty(updatePostDto.Content))
            {
                _logger.LogWarning("❌ 內容為空");
                return BadRequest(new { Success = false, Message = "內容為必填欄位" });
            }

            if (updatePostDto.MembersId.HasValue && updatePostDto.MembersId <= 0)
            {
                _logger.LogWarning("❌ 會員ID無效");
                return BadRequest(new { Success = false, Message = "會員ID不能為空或小於等於0" });
            }

            return null;
        }
        // DELETE: api/Posts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            try
            {
                _logger.LogInformation($"=== DeletePost 開始，ID: {id} ===");

                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的貼文");
                    return NotFound(new { message = "找不到指定的貼文" });
                }

                // 🔥 刪除關聯的圖片
                if (!string.IsNullOrEmpty(post.Image))
                {
                    await DeleteCloudinaryImage(post.Image);
                }

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 貼文 {post.Title} 已成功刪除");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"刪除 ID 為 {id} 的貼文失敗");
                return StatusCode(500, new { message = "刪除貼文失敗", error = ex.Message });
            }
        }

        // 🔥 審核貼文 (管理員用)
        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApprovePost(int id, [FromBody] ApprovePostDto approveDto)
        {
            try
            {
                _logger.LogInformation($"=== ApprovePost 開始，ID: {id}，審核結果: {approveDto.Approved} ===");

                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的貼文");
                    return NotFound(new { message = "找不到指定的貼文" });
                }

                var originalStatus = post.Status;

                // 🔥 設定審核結果
                if (approveDto.Approved)
                {
                    post.Status = PostStatus.Published;
                    post.PublishedAt = DateTime.UtcNow;
                    _logger.LogInformation("✅ 貼文已核准發布");
                }
                else
                {
                    post.Status = PostStatus.Rejected;
                    post.RejectedReason = approveDto.Reason ?? "未通過審核";
                    _logger.LogInformation("❌ 貼文已被拒絕");
                }

                post.UpdatedAt = DateTime.UtcNow;
                post.ReviewedAt = DateTime.UtcNow;
                // TODO: 設定 ReviewedBy 為當前管理員 ID
                // post.ReviewedBy = GetCurrentUserId();

                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 狀態已從 {originalStatus} 更新為 {post.Status}");

                return Ok(new
                {
                    message = approveDto.Approved ? "貼文已核准發布" : "貼文已拒絕",
                    status = post.Status,
                    publishedAt = post.PublishedAt,
                    rejectedReason = post.RejectedReason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"審核 ID 為 {id} 的貼文失敗");
                return StatusCode(500, new { message = "審核貼文失敗", error = ex.Message });
            }
        }

        // 🔥 取得待審核貼文 (管理員用)
        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<PostResponseDto>>> GetPendingPosts()
        {
            try
            {
                _logger.LogInformation("=== GetPendingPosts 開始 ===");

                var pendingPosts = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.MemberProfile)
                    .Where(p => p.Status == PostStatus.Draft || p.Status == PostStatus.Pending)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                var postDtos = pendingPosts.Select(p => p.ToDto()).ToList();

                _logger.LogInformation($"✅ 取得 {postDtos.Count} 個待審核貼文");

                return Ok(postDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得待審核貼文失敗");
                return StatusCode(500, new { message = "取得待審核貼文失敗", error = ex.Message });
            }
        }

        // 🔥 即時圖片上傳 API
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "沒有選擇檔案" });
            }

            try
            {
                _logger.LogInformation($"開始上傳圖片: {file.FileName}");

                var imageUrl = await UploadToCloudinary(file);
                if (imageUrl != null)
                {
                    _logger.LogInformation($"✅ 圖片上傳成功: {imageUrl}");
                    return Ok(new
                    {
                        success = true,
                        url = imageUrl,
                        fileName = Path.GetFileName(file.FileName)
                    });
                }
                else
                {
                    _logger.LogWarning("❌ 圖片上傳失敗");
                    return BadRequest(new { success = false, message = "上傳失敗，請檢查檔案格式和大小" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "圖片上傳過程發生錯誤");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #region 私有方法

        // 🔥 常數定義
        private static class PostStatus
        {
            public const string Draft = "draft";
            public const string Pending = "pending";
            public const string Published = "published";
            public const string Rejected = "rejected";
        }

        // 🔥 狀態資訊類別
        private class StatusInfo
        {
            public string Description { get; set; } = string.Empty;
            public bool RequiresImage { get; set; }
            public bool IsPublished { get; set; }
            public string ImageRequiredMessage { get; set; } = string.Empty;
        }

        // 🔥 記錄貼文創建詳細資訊
        private void LogPostCreationDetails(CreatePostDto? postDto, IFormFile? imageFile)
        {
            _logger.LogInformation($"postDto == null: {postDto == null}");

            if (postDto != null)
            {
                _logger.LogInformation($"Title: '{postDto.Title}' (長度: {postDto.Title?.Length})");
                _logger.LogInformation($"Content: '{postDto.Content}' (長度: {postDto.Content?.Length})");
                _logger.LogInformation($"MembersId: {postDto.MembersId}");
                _logger.LogInformation($"Status: '{postDto.Status}'");
            }

            _logger.LogInformation($"imageFile == null: {imageFile == null}");
            if (imageFile != null)
            {
                _logger.LogInformation($"imageFile.FileName: '{imageFile.FileName}'");
                _logger.LogInformation($"imageFile.Length: {imageFile.Length} bytes");
            }
        }

        // 🔥 驗證貼文資料
        private ActionResult? ValidatePostData(CreatePostDto? postDto)
        {
            if (postDto == null)
            {
                _logger.LogWarning("❌ postDto 為 null");
                return BadRequest(new { message = "貼文資料不能為空" });
            }

            if (string.IsNullOrEmpty(postDto.Title) || string.IsNullOrEmpty(postDto.Content))
            {
                _logger.LogWarning("❌ 標題或內容為空");
                return BadRequest(new { message = "標題和內容為必填欄位" });
            }

            if (postDto.MembersId <= 0)
            {
                _logger.LogWarning("❌ 會員ID無效");
                return BadRequest(new { message = "會員ID不能為空" });
            }

            return null;
        }

        // 🔥 標準化狀態
        private static string NormalizeStatus(string? status)
        {
            return status?.ToLower() switch
            {
                "draft" => PostStatus.Draft,
                "pending" => PostStatus.Pending,
                "published" => PostStatus.Published,
                "rejected" => PostStatus.Rejected,
                _ => PostStatus.Draft
            };
        }

        // 🔥 取得狀態資訊
        private static StatusInfo GetStatusInfo(string status)
        {
            return status switch
            {
                PostStatus.Published => new StatusInfo
                {
                    Description = "發布狀態",
                    RequiresImage = true,
                    IsPublished = true,
                    ImageRequiredMessage = "發布貼文必須包含圖片"
                },
                PostStatus.Pending => new StatusInfo
                {
                    Description = "待審核狀態",
                    RequiresImage = false,
                    IsPublished = false
                },
                PostStatus.Draft => new StatusInfo
                {
                    Description = "草稿狀態",
                    RequiresImage = false,
                    IsPublished = false
                },
                _ => new StatusInfo
                {
                    Description = "未知狀態",
                    RequiresImage = false,
                    IsPublished = false
                }
            };
        }

        // 🔥 建立貼文實體
        private static Post CreatePostEntity(CreatePostDto postDto, string? imagePath, string status, bool isPublished)
        {
            return new Post
            {
                Title = postDto.Title,
                Content = postDto.Content,
                MembersId = postDto.MembersId,
                Image = imagePath,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                PublishedAt = isPublished ? DateTime.UtcNow : null
            };
        }

        // 🔥 更新貼文實體
        private static void UpdatePostEntity(Post post, CreatePostDto postDto, string newStatus)
        {
            post.Title = postDto.Title ?? post.Title;
            post.Content = postDto.Content ?? post.Content;
            post.Status = newStatus;
            post.UpdatedAt = DateTime.UtcNow;

            if (newStatus == PostStatus.Published && post.PublishedAt == null)
            {
                post.PublishedAt = DateTime.UtcNow;
            }
        }

        // 🔥 處理圖片更新
        private async Task<string?> HandleImageUpdate(Post post, IFormFile imageFile)
        {
            try
            {
                // 刪除舊圖片
                if (!string.IsNullOrEmpty(post.Image))
                {
                    await DeleteCloudinaryImage(post.Image);
                }

                // 上傳新圖片
                var newImagePath = await UploadToCloudinary(imageFile);
                if (newImagePath != null)
                {
                    post.Image = newImagePath;
                    _logger.LogInformation($"✅ 圖片更新成功: {newImagePath}");
                }

                return newImagePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "圖片更新失敗");
                return null;
            }
        }

        // 🔥 取得包含關聯資料的貼文
        private async Task<PostResponseDto?> GetPostWithRelatedData(int postId)
        {
            try
            {
                var post = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.MemberProfile)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                return post?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"載入 ID 為 {postId} 的貼文失敗");
                return null;
            }
        }

        [HttpPost("batch-like-status")]
        public IActionResult RedirectToBatchLikeStatus()
        {
            return BadRequest(new
            {
                Success = false,
                Message = "請使用 /api/PostLikes/batch-like-status 端點查詢按讚狀態"
            });
        }

        // 🔥 上傳圖片到 Cloudinary
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

                // 檢查檔案大小 (5MB)
                const int maxFileSize = 5 * 1024 * 1024;
                if (imageFile.Length > maxFileSize)
                {
                    _logger.LogWarning($"檔案過大: {imageFile.Length} bytes (限制: {maxFileSize} bytes)");
                    return null;
                }

                using var stream = imageFile.OpenReadStream();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    PublicId = $"jade_member_post_{DateTime.Now.Ticks}",
                    Folder = "jade-member-posts",
                    Transformation = new Transformation()
                        .Width(800).Height(600).Crop("limit")
                        .Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.LogInformation($"✅ 圖片上傳成功: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    _logger.LogWarning($"❌ 圖片上傳失敗: {uploadResult.Error?.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "圖片上傳過程發生錯誤");
                return null;
            }
        }


        // 🔥 刪除 Cloudinary 圖片
        private async Task<bool> DeleteCloudinaryImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("cloudinary.com"))
                    return true;

                // 從 URL 提取 public_id
                var uri = new Uri(imageUrl);
                var pathParts = uri.AbsolutePath.Split('/');
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
                        _logger.LogWarning($"❌ Cloudinary 圖片刪除失敗: {result.Error?.Message}");
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

        private bool PostExists(int id)
        {
            return _context.Posts.Any(e => e.Id == id);
        }

        #endregion
    }
}