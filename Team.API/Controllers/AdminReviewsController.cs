using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

namespace Team.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class AdminReviewsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AdminReviewsController> _logger;

        public AdminReviewsController(AppDbContext db, ILogger<AdminReviewsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // PATCH: /api/admin/reviews/{id}/toggle-verify
        [HttpPatch("{id:int}/toggle-verify")]
        public async Task<IActionResult> ToggleVerify([FromRoute] int id)
        {
            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id);
            if (review == null) return NotFound(new { message = "找不到評論" });

            review.IsVerified = !review.IsVerified;
            review.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { id = review.Id, isVerified = review.IsVerified });
        }
    }
}
