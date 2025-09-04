using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(AppDbContext context, ILogger<ReviewsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Reviews?productId=123
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> Get([FromQuery] int? productId)
        {
            if (productId == null || productId <= 0)
            {
                return BadRequest(new { message = "缺少或無效的 productId" });
            }

            try
            {
                var reviews = await _context.Reviews
                    .AsNoTracking()
                    .Include(r => r.Member)
                        .ThenInclude(m => m.MemberProfile)
                    .Where(r => r.ProductId == productId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        id = r.Id,
                        productId = r.ProductId,
                        memberId = r.MemberId,
                        // 前端容錯鍵：customerName / userName
                        customerName = r.Member != null ? (r.Member.MemberProfile != null ? r.Member.MemberProfile.Name : (r.Member.Email ?? "會員")) : "匿名用戶",
                        userName = r.Member != null ? (r.Member.MemberProfile != null ? r.Member.MemberProfile.Name : (r.Member.Email ?? "會員")) : "匿名用戶",
                        rating = r.Rating,
                        comment = r.Comment,
                        isVerified = r.IsVerified,
                        createdAt = r.CreatedAt,
                        updatedAt = r.UpdatedAt,
                        // 額外提供 reviewDate 供前端對應
                        reviewDate = r.CreatedAt
                    })
                    .ToListAsync();

                return Ok(reviews);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得商品評價失敗");
                return StatusCode(500, new { message = "取得商品評價失敗", error = ex.Message });
            }
        }

        public class CreateReviewRequest
        {
            public int ProductId { get; set; }
            public int Rating { get; set; }
            public string? Comment { get; set; }
            // 選填：前端可能會傳
            public int? CustomerId { get; set; }
            public string? CustomerName { get; set; }
        }

        // POST: api/Reviews
        [HttpPost]
        public async Task<ActionResult<object>> Post([FromBody] CreateReviewRequest request)
        {
            try
            {
                // 基本驗證
                if (request == null)
                {
                    return BadRequest(new { message = "請提供評價內容" });
                }
                if (request.ProductId <= 0)
                {
                    return BadRequest(new { message = "無效的商品代碼" });
                }
                if (request.Rating < 1 || request.Rating > 5)
                {
                    return BadRequest(new { message = "評分需介於 1 到 5" });
                }
                if (string.IsNullOrWhiteSpace(request.Comment))
                {
                    return BadRequest(new { message = "請輸入評價內容" });
                }

                // 確認商品存在
                var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId);
                if (!productExists)
                {
                    return NotFound(new { message = "找不到指定商品" });
                }

                // 新需求：必須登入會員才能評價
                if (!request.CustomerId.HasValue || request.CustomerId.Value <= 0)
                {
                    return Unauthorized(new { message = "請先登入後再留下評價" });
                }
                var memberExists = await _context.Members.AnyAsync(m => m.Id == request.CustomerId.Value);
                if (!memberExists)
                {
                    return Unauthorized(new { message = "會員不存在或未登入" });
                }
                var memberId = request.CustomerId!.Value;

                // 必須為該商品的已完成訂單才可評價；且同商品每筆完成訂單可留一次（總評價數不可超過完成訂單數）
                // 1) 計算該會員此商品的完成訂單數（以 OrderId 去重）
                var completedOrderCount = await _context.OrderDetails
                    .AsNoTracking()
                    .Where(od => od.ProductId == request.ProductId
                                 && od.Order.MemberId == memberId
                                 && (od.Order.OrderStatus == OrderStatus.Completed
                                     || od.Order.OrderStatus == "Completed"
                                     || od.Order.OrderStatus == "已完成"))
                    .Select(od => od.OrderId)
                    .Distinct()
                    .CountAsync();

                if (completedOrderCount == 0)
                {
                    return Forbid("僅限已完成訂單的會員可評價此商品");
                }

                // 2) 已留下的評論數
                var existingReviewCount = await _context.Reviews
                    .AsNoTracking()
                    .CountAsync(r => r.ProductId == request.ProductId && r.MemberId == memberId);

                if (existingReviewCount >= completedOrderCount)
                {
                    return Conflict(new { message = "每筆完成訂單限留一則評價，您已達上限" });
                }

                var entity = new Review
                {
                    ProductId = request.ProductId,
                    MemberId = memberId,
                    Rating = request.Rating,
                    Comment = request.Comment,
                    IsVerified = true, // 預設為已核可
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Reviews.Add(entity);
                await _context.SaveChangesAsync();

                // 回傳 DTO（含 customerName 便於前端顯示）
                string name = "會員";
                var member = await _context.Members
                    .AsNoTracking()
                    .Include(m => m.MemberProfile)
                    .FirstOrDefaultAsync(m => m.Id == memberId);
                if (member != null)
                {
                    name = member.MemberProfile?.Name ?? member.Email ?? name;
                }
                else if (!string.IsNullOrWhiteSpace(request.CustomerName))
                {
                    name = request.CustomerName!;
                }

                var dto = new
                {
                    id = entity.Id,
                    productId = entity.ProductId,
                    memberId = entity.MemberId,
                    customerName = name,
                    userName = name,
                    rating = entity.Rating,
                    comment = entity.Comment,
                    isVerified = entity.IsVerified,
                    createdAt = entity.CreatedAt,
                    updatedAt = entity.UpdatedAt,
                    reviewDate = entity.CreatedAt
                };

                // 201 不易指向單一資源（本控制器無單一 GET/{id}），直接回 200
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新增商品評價失敗");
                return StatusCode(500, new { message = "新增商品評價失敗", error = ex.Message });
            }
        }
    }
}
