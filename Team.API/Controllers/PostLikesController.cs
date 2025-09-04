using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

[ApiController]
[Route("api/[controller]")]
public class PostLikesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<PostLikesController> _logger;

    public PostLikesController(AppDbContext context, ILogger<PostLikesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 切換貼文按讚狀態
    /// </summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleLike([FromBody] ToggleLikeRequest request)
    {
        try
        {
            // 🔥 暫時從請求中取得用戶 ID，或使用預設值
            var currentUserId = request.UserId ?? 1; // 如果沒有提供用戶 ID，預設使用 1

            _logger.LogInformation($"用戶 {currentUserId} 嘗試切換貼文 {request.PostId} 的按讚狀態");

            // 驗證輸入
            if (request.PostId <= 0)
            {
                _logger.LogWarning($"無效的貼文 ID: {request.PostId}");
                return BadRequest(new { Success = false, Message = "無效的貼文 ID" });
            }

            // 檢查貼文是否存在
            var post = await _context.Posts.FindAsync(request.PostId);
            if (post == null)
            {
                _logger.LogWarning($"貼文不存在: PostId = {request.PostId}");
                return NotFound(new { Success = false, Message = "貼文不存在" });
            }

            // 檢查用戶是否存在
            var member = await _context.Members.FindAsync(currentUserId);
            if (member == null)
            {
                _logger.LogWarning($"用戶不存在: UserId = {currentUserId}");
                return NotFound(new { Success = false, Message = "用戶不存在" });
            }

            // 檢查是否已經按過讚
            var existingLike = await _context.PostLikes
                .FirstOrDefaultAsync(pl => pl.PostId == request.PostId && pl.MembersId == currentUserId);

            bool isLiked;
            string actionMessage;

            if (existingLike != null)
            {
                // 已按讚，執行取消讚
                _context.PostLikes.Remove(existingLike);
                isLiked = false;
                actionMessage = "已取消讚";
                _logger.LogInformation($"用戶 {currentUserId} 取消對貼文 {request.PostId} 的讚");
            }
            else
            {
                // 未按讚，新增讚
                var newLike = new PostLike
                {
                    PostId = request.PostId,
                    MembersId = currentUserId
                };
                await _context.PostLikes.AddAsync(newLike);
                isLiked = true;
                actionMessage = "已按讚";
                _logger.LogInformation($"用戶 {currentUserId} 對貼文 {request.PostId} 按讚");
            }

            // 儲存變更
            await _context.SaveChangesAsync();

            // 重新計算總讚數
            var likesCount = await _context.PostLikes
                .CountAsync(pl => pl.PostId == request.PostId);

            var result = new
            {
                Success = true,
                Data = new
                {
                    PostId = request.PostId,
                    IsLiked = isLiked,
                    LikesCount = likesCount,
                    UserId = currentUserId
                },
                Message = actionMessage
            };

            _logger.LogInformation($"按讚操作成功: 用戶 {currentUserId}, 貼文 {request.PostId}, 狀態 {(isLiked ? "已按讚" : "已取消")}, 總讚數 {likesCount}");

            return Ok(result);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"切換按讚狀態時發生錯誤: PostId = {request.PostId}");
            return StatusCode(500, new
            {
                Success = false,
                Message = "操作失敗，請稍後重試",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 批量取得多個貼文的按讚狀態
    /// </summary>
    [HttpPost("batch-like-status")] // 🔥 改為與前端一致的路由
    public async Task<IActionResult> GetBatchLikeStatus([FromBody] BatchLikeStatusRequest request)
    {
        try
        {
            // 🔥 從前端看起來是直接傳送 postIds 陣列，而不是包在物件中
            // 所以可能需要調整為接收純陣列
            var postIds = request.PostIds;
            var currentUserId = request.UserId ?? 1;

            _logger.LogInformation($"用戶 {currentUserId} 批量查詢 {postIds?.Count ?? 0} 個貼文的按讚狀態");

            if (postIds == null || !postIds.Any())
            {
                return BadRequest(new { Success = false, Message = "請提供貼文 ID 列表" });
            }

            if (postIds.Count > 100)
            {
                return BadRequest(new { Success = false, Message = "一次最多只能查詢 100 個貼文" });
            }

            var validPostIds = postIds.Where(id => id > 0).Distinct().ToList();

            var userLikes = await _context.PostLikes
                .Where(pl => validPostIds.Contains(pl.PostId) && pl.MembersId == currentUserId)
                .Select(pl => pl.PostId)
                .ToListAsync();

            var likeCounts = await _context.PostLikes
                .Where(pl => validPostIds.Contains(pl.PostId))
                .GroupBy(pl => pl.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            // 🔥 調整回傳格式以符合前端期待
            var results = validPostIds.Select(postId => new
            {
                PostId = postId,        // 🔥 前端期待的欄位名稱
                LikesCount = likeCounts.GetValueOrDefault(postId, 0), // 🔥 前端期待的欄位名稱
                IsLiked = userLikes.Contains(postId)
            }).ToList();

            _logger.LogInformation($"批量查詢完成: 用戶 {currentUserId}, 查詢 {results.Count} 個貼文");

            // 🔥 調整回傳格式
            return Ok(new
            {
                Success = true,
                Data = results,
                Message = $"成功查詢 {results.Count} 個貼文的按讚狀態"
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量取得按讚狀態時發生錯誤");
            return StatusCode(500, new
            {
                Success = false,
                Message = "查詢失敗，請稍後重試",
                Error = ex.Message
            });
        }
    }

    [HttpPost("batch-like-status-array")]
    public async Task<IActionResult> GetBatchLikeStatusArray([FromBody] List<int> postIds, [FromQuery] int? userId = null)
    {
        try
        {
            var currentUserId = userId ?? 1; // 🔥 可以從查詢參數或 JWT Token 取得

            _logger.LogInformation($"用戶 {currentUserId} 批量查詢 {postIds?.Count ?? 0} 個貼文的按讚狀態 (陣列模式)");

            if (postIds == null || !postIds.Any())
            {
                return BadRequest(new { Success = false, Message = "請提供貼文 ID 列表" });
            }

            if (postIds.Count > 100)
            {
                return BadRequest(new { Success = false, Message = "一次最多只能查詢 100 個貼文" });
            }

            var validPostIds = postIds.Where(id => id > 0).Distinct().ToList();

            var userLikes = await _context.PostLikes
                .Where(pl => validPostIds.Contains(pl.PostId) && pl.MembersId == currentUserId)
                .Select(pl => pl.PostId)
                .ToListAsync();

            var likeCounts = await _context.PostLikes
                .Where(pl => validPostIds.Contains(pl.PostId))
                .GroupBy(pl => pl.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            var results = validPostIds.Select(postId => new
            {
                PostId = postId,
                LikesCount = likeCounts.GetValueOrDefault(postId, 0),
                IsLiked = userLikes.Contains(postId)
            }).ToList();

            _logger.LogInformation($"批量查詢完成 (陣列模式): 用戶 {currentUserId}, 查詢 {results.Count} 個貼文");

            return Ok(new
            {
                Success = true,
                Data = results,
                Message = $"成功查詢 {results.Count} 個貼文的按讚狀態"
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量取得按讚狀態時發生錯誤 (陣列模式)");
            return StatusCode(500, new
            {
                Success = false,
                Message = "查詢失敗，請稍後重試",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 取得單一貼文的按讚狀態
    /// </summary>
    [HttpGet("post/{postId}/status")]
    public async Task<IActionResult> GetLikeStatus(int postId, [FromQuery] int? userId = null)
    {
        try
        {
            var currentUserId = userId ?? 1; // 暫時預設用戶 ID

            _logger.LogInformation($"用戶 {currentUserId} 查詢貼文 {postId} 的按讚狀態");

            // 檢查貼文是否存在
            var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
            if (!postExists)
            {
                return NotFound(new { Success = false, Message = "貼文不存在" });
            }

            // 檢查是否已按讚
            var isLiked = await _context.PostLikes
                .AnyAsync(pl => pl.PostId == postId && pl.MembersId == currentUserId);

            // 計算總讚數
            var likesCount = await _context.PostLikes
                .CountAsync(pl => pl.PostId == postId);

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    PostId = postId,
                    IsLiked = isLiked,
                    LikesCount = likesCount,
                    UserId = currentUserId
                }
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"取得按讚狀態時發生錯誤: PostId = {postId}");
            return StatusCode(500, new
            {
                Success = false,
                Message = "查詢失敗，請稍後重試",
                Error = ex.Message
            });
        }
    }
}

#region Request Models

/// <summary>
/// 切換按讚請求模型
/// </summary>
public class ToggleLikeRequest
{
    /// <summary>
    /// 貼文 ID
    /// </summary>
    public int PostId { get; set; }

    /// <summary>
    /// 用戶 ID（暫時用，之後從 Token 取得）
    /// </summary>
    public int? UserId { get; set; }
}

/// <summary>
/// 批量查詢按讚狀態請求模型
/// </summary>
public class BatchLikeStatusRequest
{
    /// <summary>
    /// 貼文 ID 列表
    /// </summary>
    public List<int> PostIds { get; set; } = new List<int>();

    /// <summary>
    /// 用戶 ID（暫時用，之後從 Token 取得）
    /// </summary>
    public int? UserId { get; set; }
}

#endregion