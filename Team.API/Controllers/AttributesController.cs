using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using AttributeEntity = Team.API.Models.EfModel.Attribute; // 使用別名避免衝突

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttributesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttributesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Attributes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAttributes()
        {
            var attributes = await _context.Attributes
                .Include(a => a.AttributeValues)
                .Where(a => a.IsApproved == true)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    sellersId = a.SellersId,
                    isApproved = a.IsApproved,
                    attributeValues = a.AttributeValues.Select(av => new
                    {
                        id = av.Id,
                        value = av.Value,
                        hexCode = av.HexCode,
                        attributeId = av.AttributeId,
                        sellersId = av.SellersId
                    })
                })
                .ToListAsync();

            return Ok(attributes);
        }

        // GET: api/Attributes/style
        [HttpGet("style")]
        public async Task<ActionResult<IEnumerable<object>>> GetStyleAttributes()
        {
            var styleAttributes = await _context.Attributes
                .Include(a => a.AttributeValues)
                .Where(a => a.IsApproved == true && a.Name.Contains("風格"))
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    attributeValues = a.AttributeValues.Select(av => new
                    {
                        id = av.Id,
                        value = av.Value,
                        hexCode = av.HexCode,
                        attributeId = av.AttributeId
                    })
                })
                .ToListAsync();

            return Ok(styleAttributes);
        }

        // GET: api/Attributes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetAttribute(int id)
        {
            var attribute = await _context.Attributes
                .Include(a => a.AttributeValues)
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    sellersId = a.SellersId,
                    isApproved = a.IsApproved,
                    attributeValues = a.AttributeValues.Select(av => new
                    {
                        id = av.Id,
                        value = av.Value,
                        hexCode = av.HexCode,
                        attributeId = av.AttributeId,
                        sellersId = av.SellersId
                    })
                })
                .FirstOrDefaultAsync();

            if (attribute == null)
            {
                return NotFound();
            }

            return Ok(attribute);
        }

        // POST: api/Attributes
        [HttpPost]
        public async Task<ActionResult<AttributeEntity>> PostAttribute(AttributeEntity attribute)
        {
            _context.Attributes.Add(attribute);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAttribute", new { id = attribute.Id }, attribute);
        }

        // PUT: api/Attributes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAttribute(int id, AttributeEntity attribute)
        {
            if (id != attribute.Id)
            {
                return BadRequest();
            }

            _context.Entry(attribute).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AttributeExists(id))
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

        // DELETE: api/Attributes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttribute(int id)
        {
            var attribute = await _context.Attributes.FindAsync(id);
            if (attribute == null)
            {
                return NotFound();
            }

            _context.Attributes.Remove(attribute);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AttributeExists(int id)
        {
            return _context.Attributes.Any(e => e.Id == id);
        }
    }
}