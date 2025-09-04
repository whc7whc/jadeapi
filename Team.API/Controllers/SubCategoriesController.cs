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
    public class SubCategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubCategoriesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/SubCategories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetSubCategories()
        {
            var subCategories = await _context.SubCategories
                .Include(sc => sc.Category)
                .Select(sc => new
                {
                    id = sc.Id,
                    categoryId = sc.CategoryId,
                    name = sc.Name,
                    description = sc.Description,
                    isVisible = true, // 預設值
                    sortOrder = 0, // 預設值
                    category = new
                    {
                        id = sc.Category.Id,
                        name = sc.Category.Name,
                        description = sc.Category.Description
                    }
                })
                .ToListAsync();

            return Ok(subCategories);
        }

        // GET: api/SubCategories/by-category/5
        [HttpGet("by-category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetSubCategoriesByCategory(int categoryId)
        {
            var subCategories = await _context.SubCategories
                .Include(sc => sc.Category)
                .Where(sc => sc.CategoryId == categoryId)
                .Select(sc => new
                {
                    id = sc.Id,
                    categoryId = sc.CategoryId,
                    name = sc.Name,
                    description = sc.Description,
                    isVisible = true,
                    sortOrder = 0
                })
                .ToListAsync();

            return Ok(subCategories);
        }

        // GET: api/SubCategories/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetSubCategory(int id)
        {
            var subCategory = await _context.SubCategories
                .Include(sc => sc.Category)
                .Where(sc => sc.Id == id)
                .Select(sc => new
                {
                    id = sc.Id,
                    categoryId = sc.CategoryId,
                    name = sc.Name,
                    description = sc.Description,
                    isVisible = true,
                    sortOrder = 0,
                    category = new
                    {
                        id = sc.Category.Id,
                        name = sc.Category.Name,
                        description = sc.Category.Description
                    }
                })
                .FirstOrDefaultAsync();

            if (subCategory == null)
            {
                return NotFound();
            }

            return Ok(subCategory);
        }

        // POST: api/SubCategories
        [HttpPost]
        public async Task<ActionResult<SubCategory>> PostSubCategory(SubCategory subCategory)
        {
            _context.SubCategories.Add(subCategory);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSubCategory", new { id = subCategory.Id }, subCategory);
        }

        // PUT: api/SubCategories/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSubCategory(int id, SubCategory subCategory)
        {
            if (id != subCategory.Id)
            {
                return BadRequest();
            }

            _context.Entry(subCategory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SubCategoryExists(id))
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

        // DELETE: api/SubCategories/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubCategory(int id)
        {
            var subCategory = await _context.SubCategories.FindAsync(id);
            if (subCategory == null)
            {
                return NotFound();
            }

            _context.SubCategories.Remove(subCategory);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SubCategoryExists(int id)
        {
            return _context.SubCategories.Any(e => e.Id == id);
        }
    }
}