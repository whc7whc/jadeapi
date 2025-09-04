using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfficialPostsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OfficialPostsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/OfficialPosts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OfficialPost>>> GetOfficialPosts(
            [FromQuery] string category = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                Console.WriteLine($"🚀 開始執行 GetOfficialPosts, category: {category}");

                // 建立基本查詢
                var query = _context.OfficialPosts
                    .Where(p => p.Status == "published");

                // 如果有指定分類，加入分類篩選
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(p => p.Category == category);
                    Console.WriteLine($"📂 篩選分類: {category}");
                }

                // 計算總數
                var totalCount = await query.CountAsync();

                // 執行分頁查詢
                var posts = await query
                    .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                Console.WriteLine($"📝 找到 {posts.Count} 篇文章 (總計: {totalCount})");

                // 手動為每篇文章查詢圖片
                var result = new List<object>();

                foreach (var post in posts)
                {
                    var images = await _context.OfficialPostImages
                        .Where(img => img.PostId == post.Id)
                        .OrderBy(img => img.SortOrder)
                        .Select(img => new {
                            img.Id,
                            img.PostId,
                            img.ImagePath,
                            img.SortOrder
                        })
                        .ToListAsync();

                    Console.WriteLine($"📸 文章 {post.Id} 有 {images.Count} 張圖片");

                    // 生成文章摘要
                    var excerpt = GenerateExcerpt(post.Content, 150);

                    result.Add(new
                    {
                        post.Id,
                        post.Title,
                        post.SeoTitle,
                        post.SeoDescription,
                        post.Content,
                        post.CoverImage,
                        post.Category,
                        post.ReadingTime,
                        post.Status,
                        post.PublishedAt,
                        post.CreatedBy,
                        post.CreatedAt,
                        post.UpdatedAt,
                        Excerpt = excerpt,
                        OfficialPostImages = images
                    });
                }

                Console.WriteLine($"✅ 成功取得 {result.Count} 筆文章（含圖片）");

                return Ok(new
                {
                    Data = result,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Controller 錯誤: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/OfficialPosts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetOfficialPost(int id)
        {
            try
            {
                Console.WriteLine($"🔍 查詢單篇文章 ID: {id}");

                var officialPost = await _context.OfficialPosts
                    .Where(p => p.Id == id && p.Status == "published")
                    .FirstOrDefaultAsync();

                if (officialPost == null)
                {
                    Console.WriteLine($"❌ 文章 {id} 不存在或未發布");
                    return NotFound(new { message = "文章不存在或未發布" });
                }

                // 獲取文章的圖片
                var images = await _context.OfficialPostImages
                    .Where(img => img.PostId == officialPost.Id)
                    .OrderBy(img => img.SortOrder)
                    .Select(img => new {
                        img.Id,
                        img.PostId,
                        img.ImagePath,
                        img.SortOrder
                    })
                    .ToListAsync();

                Console.WriteLine($"📸 文章 {id} 有 {images.Count} 張圖片");

                var result = new
                {
                    officialPost.Id,
                    officialPost.Title,
                    officialPost.SeoTitle,
                    officialPost.SeoDescription,
                    officialPost.Content,
                    officialPost.CoverImage,
                    officialPost.Category,
                    officialPost.ReadingTime,
                    officialPost.Status,
                    officialPost.PublishedAt,
                    officialPost.CreatedBy,
                    officialPost.CreatedAt,
                    officialPost.UpdatedAt,
                    OfficialPostImages = images
                };

                Console.WriteLine($"✅ 成功取得文章 {id}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 查詢單篇文章錯誤: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/OfficialPosts/categories
        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<object>>> GetCategories()
        {
            try
            {
                Console.WriteLine("📂 取得所有文章分類");

                var categories = await _context.OfficialPosts
                    .Where(p => p.Status == "published" && !string.IsNullOrEmpty(p.Category))
                    .GroupBy(p => p.Category)
                    .Select(g => new
                    {
                        Category = g.Key,
                        Count = g.Count()
                    })
                    .OrderBy(c => c.Category)
                    .ToListAsync();

                Console.WriteLine($"✅ 找到 {categories.Count} 個分類");
                return Ok(categories);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取得分類錯誤: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/OfficialPosts/by-category/{category}
        [HttpGet("by-category/{category}")]
        public async Task<ActionResult<IEnumerable<object>>> GetPostsByCategory(
            string category,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                Console.WriteLine($"📂 按分類查詢文章: {category}");

                var query = _context.OfficialPosts
                    .Where(p => p.Status == "published" && p.Category == category);

                var totalCount = await query.CountAsync();

                var posts = await query
                    .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                Console.WriteLine($"📝 找到 {posts.Count} 篇 {category} 分類文章");

                var result = new List<object>();

                foreach (var post in posts)
                {
                    var images = await _context.OfficialPostImages
                        .Where(img => img.PostId == post.Id)
                        .OrderBy(img => img.SortOrder)
                        .Select(img => new {
                            img.Id,
                            img.PostId,
                            img.ImagePath,
                            img.SortOrder
                        })
                        .ToListAsync();

                    var excerpt = GenerateExcerpt(post.Content, 150);

                    result.Add(new
                    {
                        post.Id,
                        post.Title,
                        post.SeoTitle,
                        post.SeoDescription,
                        post.Content,
                        post.CoverImage,
                        post.Category,
                        post.ReadingTime,
                        post.Status,
                        post.PublishedAt,
                        post.CreatedBy,
                        post.CreatedAt,
                        post.UpdatedAt,
                        Excerpt = excerpt,
                        OfficialPostImages = images
                    });
                }

                return Ok(new
                {
                    Data = result,
                    Category = category,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 按分類查詢錯誤: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PUT: api/OfficialPosts/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOfficialPost(int id, OfficialPost officialPost)
        {
            if (id != officialPost.Id)
            {
                return BadRequest();
            }

            _context.Entry(officialPost).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OfficialPostExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        private bool OfficialPostExists(int id)
        {
            return _context.OfficialPosts.Any(e => e.Id == id);
        }

        // 輔助方法：生成文章摘要
        private string GenerateExcerpt(string content, int length = 150)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            // 移除 HTML 標籤
            var textContent = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", string.Empty);

            // 移除多餘的空白字符
            textContent = System.Text.RegularExpressions.Regex.Replace(textContent, @"\s+", " ").Trim();

            if (textContent.Length <= length)
                return textContent;

            return textContent.Substring(0, length) + "...";
        }

        // 測試方法 - 可以在正式環境中移除
        [HttpGet("test-images")]
        public async Task<ActionResult> TestImages()
        {
            try
            {
                Console.WriteLine("🖼️ 測試圖片資料查詢");

                var imageCount = await _context.OfficialPostImages.CountAsync();
                Console.WriteLine($"📸 圖片總數: {imageCount}");

                if (imageCount > 0)
                {
                    var firstImages = await _context.OfficialPostImages
                        .Take(3)
                        .Select(img => new {
                            img.Id,
                            img.PostId,
                            img.ImagePath,
                            img.SortOrder
                        })
                        .ToListAsync();

                    Console.WriteLine("📸 前3張圖片:");
                    foreach (var img in firstImages)
                    {
                        Console.WriteLine($"   ID: {img.Id}, PostID: {img.PostId}, Path: {img.ImagePath}");
                    }

                    return Ok(new
                    {
                        totalImages = imageCount,
                        sampleImages = firstImages
                    });
                }
                else
                {
                    return Ok(new { message = "資料庫中沒有圖片資料" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 圖片查詢錯誤: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("test-with-images")]
        public async Task<ActionResult> TestWithImages()
        {
            try
            {
                Console.WriteLine("🧪 測試手動關聯查詢");

                var posts = await _context.OfficialPosts
                    .Where(p => p.Status == "published")
                    .ToListAsync();

                Console.WriteLine($"📝 找到 {posts.Count} 篇文章");

                var result = new List<object>();

                foreach (var post in posts)
                {
                    var images = await _context.OfficialPostImages
                        .Where(img => img.PostId == post.Id)
                        .Select(img => new {
                            img.Id,
                            img.PostId,
                            img.ImagePath,
                            img.SortOrder
                        })
                        .ToListAsync();

                    Console.WriteLine($"📸 文章 {post.Id} 有 {images.Count} 張圖片");

                    result.Add(new
                    {
                        post.Id,
                        post.Title,
                        post.Category,
                        post.CoverImage,
                        post.Status,
                        post.PublishedAt,
                        ImagesCount = images.Count,
                        Images = images
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 手動關聯查詢錯誤: {ex.Message}");
                Console.WriteLine($"❌ Inner Exception: {ex.InnerException?.Message}");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }
    }
}