using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;

namespace Team.Backend.Controllers
{
    public class ProductReviewsController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductReviewsController> _logger;

        public ProductReviewsController(AppDbContext db, ILogger<ProductReviewsController> logger)
            : base(db, logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: /ProductReviews
        [HttpGet]
        public async Task<IActionResult> Index(int? productId)
        {
            var query = _db.Reviews
                .AsNoTracking()
                .Include(r => r.Product)
                .Include(r => r.Member)
                    .ThenInclude(m => m.Profile)
                .OrderByDescending(r => r.CreatedAt)
                .AsQueryable();

            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(r => r.ProductId == productId.Value);
            }

            var list = await query.Select(r => new ProductReviewViewModel
            {
                Id = r.Id,
                ProductId = r.ProductId ?? 0,
                ProductName = r.Product != null ? r.Product.Name : "-",
                MemberId = r.MemberId,
                CustomerName = r.Member != null ? (r.Member.Profile != null ? r.Member.Profile.Name : (r.Member.Email ?? "會員")) : "匿名用戶",
                Rating = r.Rating,
                Comment = r.Comment,
                IsVerified = r.IsVerified ?? false,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToListAsync();

            return View(list);
        }

        // POST: /ProductReviews/ToggleVerify
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVerify(int id)
        {
            var review = await _db.Reviews.FindAsync(id);
            if (review == null) return NotFound(new { message = "找不到評價" });

            review.IsVerified = !(review.IsVerified ?? false);
            review.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return Json(new { success = true, isVerified = review.IsVerified });
        }

        // POST: /ProductReviews/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _db.Reviews.FindAsync(id);
            if (review == null) return NotFound(new { message = "找不到評價" });

            _db.Reviews.Remove(review);
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
