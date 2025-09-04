using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Team.Backend.Controllers
{
    [Route("Blog")]
    public class MemberPostController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<MemberPostController> _logger;

        public MemberPostController(AppDbContext context, Cloudinary cloudinary, ILogger<MemberPostController> logger)
            : base(context, logger)
        {
            _context = context;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        [HttpGet("MemberPosts")]
        public async Task<IActionResult> MemberPosts()
        {
            try
            {
                // 只顯示非草稿狀態的貼文
                var posts = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.Profile)
                    .Where(p => p.Status != "draft") // 過濾掉草稿
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                Console.WriteLine($"取得 {posts.Count} 篇會員貼文（已過濾草稿）");
                return View(posts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取得會員貼文失敗: {ex.Message}");
                TempData["Error"] = "載入會員貼文失敗";
                return View(new List<Post>());
            }
        }

        // 會員貼文詳情頁面（只供查看）
        [HttpGet("MemberPostDetails/{id:int}")]
        public async Task<IActionResult> MemberPostDetails(int id)
        {
            try
            {
                var post = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.Profile)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    TempData["Error"] = "找不到指定的貼文";
                    return RedirectToAction("MemberPosts");
                }

                return View(post);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取得貼文詳情失敗: {ex.Message}");
                TempData["Error"] = "載入貼文詳情失敗";
                return RedirectToAction("MemberPosts");
            }
        }



        [HttpPost("ReviewPost")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewPost(int id, string action, string rejectReason = null)
        {
            try
            {
                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                {
                    return Json(new { success = false, message = "找不到指定的貼文" });
                }

                var now = DateTime.Now;
                string message = "";

                // 🔥 增強的動作處理
                switch (action.ToLower())
                {
                    case "approve":
                        post.Status = "published";
                        post.PublishedAt = now;
                        post.ReviewedAt = now;
                        post.RejectedReason = null; // 清除之前的退回原因
                        message = "貼文已核准發布";
                        break;

                    case "reject":
                        if (string.IsNullOrWhiteSpace(rejectReason))
                        {
                            return Json(new { success = false, message = "請提供退回原因" });
                        }
                        post.Status = "rejected";
                        post.ReviewedAt = now;
                        post.RejectedReason = rejectReason;
                        post.PublishedAt = null; // 清除發布時間
                        message = "貼文已退回";
                        break;

                    case "pending":
                        // 撤銷發布，改為待審核
                        post.Status = "pending";
                        post.ReviewedAt = now;
                        post.PublishedAt = null; // 清除發布時間
                        post.RejectedReason = null; // 清除退回原因
                        message = "貼文已改為待審核狀態";
                        break;

                    case "unpublish":
                        // 撤銷發布（同 pending）
                        post.Status = "pending";
                        post.ReviewedAt = now;
                        post.PublishedAt = null;
                        post.RejectedReason = null;
                        message = "貼文發布已撤銷，改為待審核狀態";
                        break;

                    default:
                        return Json(new { success = false, message = "無效的審核操作" });
                }

                post.UpdatedAt = now;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ {message}: ID={id}, 新狀態={post.Status}");

                return Json(new
                {
                    success = true,
                    message = message,
                    newStatus = post.Status
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 審核貼文失敗: {ex.Message}");
                return Json(new { success = false, message = $"審核失敗: {ex.Message}" });
            }
        }

        // 批次審核貼文
        [HttpPost("BatchReview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchReview(int[] postIds, string action, string rejectReason = null)
        {
            try
            {
                if (postIds == null || !postIds.Any())
                {
                    return Json(new { success = false, message = "請選擇要審核的貼文" });
                }

                if (action.ToLower() == "reject" && string.IsNullOrWhiteSpace(rejectReason))
                {
                    return Json(new { success = false, message = "批次退回需要提供原因" });
                }

                var posts = await _context.Posts
                    .Where(p => postIds.Contains(p.Id))
                    .ToListAsync();

                if (!posts.Any())
                {
                    return Json(new { success = false, message = "找不到指定的貼文" });
                }

                int successCount = 0;
                foreach (var post in posts)
                {
                    switch (action.ToLower())
                    {
                        case "approve":
                            post.Status = "published";
                            post.PublishedAt = DateTime.Now;
                            post.ReviewedAt = DateTime.Now;
                            post.RejectedReason = null;
                            successCount++;
                            break;

                        case "reject":
                            post.Status = "rejected";
                            post.ReviewedAt = DateTime.Now;
                            post.RejectedReason = rejectReason;
                            successCount++;
                            break;
                    }
                    post.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                string message = action.ToLower() == "approve"
                    ? $"已核准 {successCount} 篇貼文"
                    : $"已退回 {successCount} 篇貼文";

                Console.WriteLine($"✅ 批次審核完成: {message}");

                return Json(new
                {
                    success = true,
                    message = message,
                    processedCount = successCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 批次審核失敗: {ex.Message}");
                return Json(new { success = false, message = $"批次審核失敗: {ex.Message}" });
            }
        }

        // 取得單篇貼文詳情 (AJAX用)
        [HttpGet("GetPostDetails")]
        public async Task<IActionResult> GetPostDetails(int id)
        {
            try
            {
                var post = await _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.Profile)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    return Json(new { success = false, message = "找不到指定的貼文" });
                }

                var result = new
                {
                    success = true,
                    data = new
                    {
                        id = post.Id,
                        title = post.Title,
                        content = post.Content,
                        image = post.Image,
                        status = post.Status,
                        createdAt = post.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                        reviewedAt = post.ReviewedAt?.ToString("yyyy/MM/dd HH:mm"),
                        rejectedReason = post.RejectedReason,
                        author = new
                        {
                            id = post.Members?.Id,
                            name = post.Members?.Profile?.Name ?? "未知用戶",
                            email = post.Members?.Email
                        }
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取得貼文詳情失敗: {ex.Message}");
                return Json(new { success = false, message = $"取得貼文詳情失敗: {ex.Message}" });
            }
        }

        // 刪除會員貼文（處理違規內容）
        [HttpPost("DeleteMemberPost")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMemberPost(int id)
        {
            try
            {
                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                {
                    TempData["Error"] = "找不到指定的貼文";
                    return RedirectToAction("MemberPosts");
                }

                // 刪除 Cloudinary 圖片
                if (!string.IsNullOrEmpty(post.Image) && post.Image.Contains("cloudinary.com"))
                {
                    await DeleteCloudinaryImage(post.Image);
                    Console.WriteLine($"已刪除會員貼文圖片: {post.Image}");
                }

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                TempData["Success"] = "會員貼文已成功刪除";
                Console.WriteLine($"✅ 已刪除會員貼文: ID={id}");

                return RedirectToAction("MemberPosts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刪除會員貼文失敗: {ex.Message}");
                TempData["Error"] = $"刪除失敗: {ex.Message}";
                return RedirectToAction("MemberPosts");
            }
        }

        // 篩選和搜尋會員貼文
        [HttpGet("FilterMemberPosts")]
        public async Task<IActionResult> FilterMemberPosts(string status = "", string search = "", int page = 1, int pageSize = 10)
        {
            try
            {
                var query = _context.Posts
                    .Include(p => p.Members)
                        .ThenInclude(m => m.Profile)
                    .Where(p => p.Status != "draft") // 過濾掉草稿
                    .AsQueryable();

                // 狀態篩選
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(p => p.Status == status);
                }

                // 搜尋
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p =>
                        p.Title.Contains(search) ||
                        p.Content.Contains(search) ||
                        (p.Members != null && p.Members.Profile != null && p.Members.Profile.Name.Contains(search))
                    );
                }

                var totalCount = await query.CountAsync();

                var posts = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        id = p.Id,
                        title = p.Title,
                        content = p.Content.Length > 100 ? p.Content.Substring(0, 100) + "..." : p.Content,
                        image = p.Image,
                        status = p.Status,
                        createdAt = p.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                        reviewedAt = p.ReviewedAt.HasValue ? p.ReviewedAt.Value.ToString("yyyy/MM/dd HH:mm") : "",
                        rejectedReason = p.RejectedReason,
                        authorName = p.Members.Profile.Name ?? "未知用戶",
                        membersId = p.MembersId
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = posts,
                    totalCount = totalCount,
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 篩選會員貼文失敗: {ex.Message}");
                return Json(new { success = false, message = $"篩選失敗: {ex.Message}" });
            }
        }

        // 私有方法：刪除 Cloudinary 圖片
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
    }
}