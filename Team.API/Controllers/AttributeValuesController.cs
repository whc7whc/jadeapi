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
    public class AttributeValuesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttributeValuesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/AttributeValues
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAttributeValues()
        {
            var attributeValues = await _context.AttributeValues
                .Include(av => av.Attribute)
                .Select(av => new
                {
                    id = av.Id,
                    value = av.Value,
                    hexCode = av.HexCode,
                    attributeId = av.AttributeId,
                    sellersId = av.SellersId,
                    attribute = new
                    {
                        id = av.Attribute.Id,
                        name = av.Attribute.Name,
                        description = av.Attribute.Description
                    }
                })
                .ToListAsync();

            return Ok(attributeValues);
        }

        // GET: api/AttributeValues/style
        [HttpGet("style")]
        public async Task<ActionResult<IEnumerable<object>>> GetStyleAttributeValues()
        {
            var styleAttributeValues = await _context.AttributeValues
                .Include(av => av.Attribute)
                .Where(av => av.Attribute.Name.Contains("風格"))
                .Select(av => new
                {
                    id = av.Id,
                    value = av.Value,
                    hexCode = av.HexCode,
                    attributeId = av.AttributeId,
                    attribute = new
                    {
                        id = av.Attribute.Id,
                        name = av.Attribute.Name
                    }
                })
                .ToListAsync();

            return Ok(styleAttributeValues);
        }

        // GET: api/AttributeValues/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetAttributeValue(int id)
        {
            var attributeValue = await _context.AttributeValues
                .Include(av => av.Attribute)
                .Where(av => av.Id == id)
                .Select(av => new
                {
                    id = av.Id,
                    value = av.Value,
                    hexCode = av.HexCode,
                    attributeId = av.AttributeId,
                    sellersId = av.SellersId,
                    attribute = new
                    {
                        id = av.Attribute.Id,
                        name = av.Attribute.Name,
                        description = av.Attribute.Description
                    }
                })
                .FirstOrDefaultAsync();

            if (attributeValue == null)
            {
                return NotFound();
            }

            return Ok(attributeValue);
        }

        // POST: api/AttributeValues
        [HttpPost]
        public async Task<ActionResult<AttributeValue>> PostAttributeValue(AttributeValue attributeValue)
        {
            _context.AttributeValues.Add(attributeValue);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAttributeValue", new { id = attributeValue.Id }, attributeValue);
        }

        // PUT: api/AttributeValues/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAttributeValue(int id, AttributeValue attributeValue)
        {
            if (id != attributeValue.Id)
            {
                return BadRequest();
            }

            _context.Entry(attributeValue).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AttributeValueExists(id))
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

        // DELETE: api/AttributeValues/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttributeValue(int id)
        {
            var attributeValue = await _context.AttributeValues.FindAsync(id);
            if (attributeValue == null)
            {
                return NotFound();
            }

            _context.AttributeValues.Remove(attributeValue);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AttributeValueExists(int id)
        {
            return _context.AttributeValues.Any(e => e.Id == id);
        }
    }
}