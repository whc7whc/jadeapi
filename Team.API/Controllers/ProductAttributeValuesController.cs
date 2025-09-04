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
    public class ProductAttributeValuesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductAttributeValuesController(AppDbContext context)
        {
            _context = context;
        }

        // 🔥 修復：GET: api/ProductAttributeValues - 使用 DTO 避免循環引用
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProductAttributeValues()
        {
            var productAttributeValues = await _context.ProductAttributeValues
                .Include(pav => pav.AttributeValue)
                    .ThenInclude(av => av.Attribute)
                .Include(pav => pav.Product)
                .Select(pav => new
                {
                    Id = pav.Id,
                    ProductId = pav.ProductId,
                    AttributeValueId = pav.AttributeValueId,
                    Stock = pav.Stock,
                    Sku = pav.Sku,
                    SkuGroupId = pav.SkuGroupId,
                    AdditionalPrice = pav.AdditionalPrice,
                    CreatedAt = pav.CreatedAt,
                    UpdatedAt = pav.UpdatedAt,
                    // 🔥 只包含需要的屬性值資料，避免循環引用
                    AttributeValue = new
                    {
                        Id = pav.AttributeValue.Id,
                        Value = pav.AttributeValue.Value,
                        HexCode = pav.AttributeValue.HexCode,
                        AttributeId = pav.AttributeValue.AttributeId,
                        // 🔥 只包含基本屬性資料，不包含 AttributeValues 集合
                        Attribute = new
                        {
                            Id = pav.AttributeValue.Attribute.Id,
                            Name = pav.AttributeValue.Attribute.Name,
                            Description = pav.AttributeValue.Attribute.Description
                        }
                    },
                    // 🔥 只包含基本商品資料
                    Product = new
                    {
                        Id = pav.Product.Id,
                        Name = pav.Product.Name
                    }
                })
                .ToListAsync();

            return Ok(productAttributeValues);
        }

        // 🔥 修復：GET: api/ProductAttributeValues/5 - 使用 DTO
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProductAttributeValue(int id)
        {
            var productAttributeValue = await _context.ProductAttributeValues
                .Include(pav => pav.AttributeValue)
                    .ThenInclude(av => av.Attribute)
                .Include(pav => pav.Product)
                .Where(pav => pav.Id == id)
                .Select(pav => new
                {
                    Id = pav.Id,
                    ProductId = pav.ProductId,
                    AttributeValueId = pav.AttributeValueId,
                    Stock = pav.Stock,
                    Sku = pav.Sku,
                    SkuGroupId = pav.SkuGroupId,
                    AdditionalPrice = pav.AdditionalPrice,
                    CreatedAt = pav.CreatedAt,
                    UpdatedAt = pav.UpdatedAt,
                    AttributeValue = new
                    {
                        Id = pav.AttributeValue.Id,
                        Value = pav.AttributeValue.Value,
                        HexCode = pav.AttributeValue.HexCode,
                        AttributeId = pav.AttributeValue.AttributeId,
                        Attribute = new
                        {
                            Id = pav.AttributeValue.Attribute.Id,
                            Name = pav.AttributeValue.Attribute.Name,
                            Description = pav.AttributeValue.Attribute.Description
                        }
                    },
                    Product = new
                    {
                        Id = pav.Product.Id,
                        Name = pav.Product.Name
                    }
                })
                .FirstOrDefaultAsync();

            if (productAttributeValue == null)
            {
                return NotFound();
            }

            return Ok(productAttributeValue);
        }

        // 🔥 新增：根據商品 ID 取得所有屬性值 - 使用 DTO
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetByProductId(int productId)
        {
            try
            {
                Console.WriteLine($"🔍 查詢商品 {productId} 的屬性值...");

                var productAttributeValues = await _context.ProductAttributeValues
                    .Where(pav => pav.ProductId == productId)
                    .Include(pav => pav.AttributeValue)
                        .ThenInclude(av => av.Attribute)
                    .OrderBy(pav => pav.AttributeValue.AttributeId) // 先按屬性類型排序
                    .ThenBy(pav => pav.SkuGroupId) // 再按 SkuGroupId 排序
                    .ToListAsync();

                Console.WriteLine($"✅ 查詢到 {productAttributeValues.Count} 個屬性值記錄");

                var result = productAttributeValues.Select(pav => new
                {
                    id = pav.Id,
                    productId = pav.ProductId,
                    attributeValueId = pav.AttributeValueId,
                    stock = pav.Stock,
                    sku = pav.Sku,
                    skuGroupId = pav.SkuGroupId, // 🔥 確保包含此欄位
                    additionalPrice = pav.AdditionalPrice,
                    createdAt = pav.CreatedAt,
                    updatedAt = pav.UpdatedAt,
                    attributeValue = pav.AttributeValue != null ? new
                    {
                        id = pav.AttributeValue.Id,
                        value = pav.AttributeValue.Value,
                        hexCode = pav.AttributeValue.HexCode,
                        attributeId = pav.AttributeValue.AttributeId,
                        attribute = pav.AttributeValue.Attribute != null ? new
                        {
                            id = pav.AttributeValue.Attribute.Id,
                            name = pav.AttributeValue.Attribute.Name,
                            description = pav.AttributeValue.Attribute.Description
                        } : null
                    } : null
                }).ToList();

                // 🔥 調試：打印結果
                foreach (var item in result)
                {
                    Console.WriteLine($"📊 記錄 ID:{item.id}, AttributeId:{item.attributeValue?.attributeId}, Value:{item.attributeValue?.value}, Stock:{item.stock}, SkuGroupId:{item.skuGroupId}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 查詢商品屬性值失敗: {ex.Message}");
                return StatusCode(500, new { message = "查詢失敗", error = ex.Message });
            }
        }

        // 🔥 新增：根據商品 ID 取得庫存總計
        [HttpGet("product/{productId}/stock-summary")]
        public async Task<ActionResult<object>> GetProductStockSummary(int productId)
        {
            var stockData = await _context.ProductAttributeValues
                .Where(pav => pav.ProductId == productId)
                .Include(pav => pav.AttributeValue)
                    .ThenInclude(av => av.Attribute)
                .GroupBy(pav => new { pav.ProductId })
                .Select(g => new
                {
                    ProductId = g.Key.ProductId,
                    TotalStock = g.Sum(pav => pav.Stock),
                    VariantCount = g.Count(),
                    StockByAttribute = g.GroupBy(pav => pav.AttributeValue.Attribute.Name)
                        .Select(ag => new
                        {
                            AttributeName = ag.Key,
                            TotalStock = ag.Sum(pav => pav.Stock),
                            Variants = ag.Select(pav => new
                            {
                                AttributeValue = pav.AttributeValue.Value,
                                Stock = pav.Stock,
                                Sku = pav.Sku
                            })
                        })
                })
                .FirstOrDefaultAsync();

            if (stockData == null)
            {
                return NotFound($"找不到商品 ID {productId} 的庫存資料");
            }

            return Ok(stockData);
        }

        // PUT: api/ProductAttributeValues/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProductAttributeValue(int id, ProductAttributeValue productAttributeValue)
        {
            if (id != productAttributeValue.Id)
            {
                return BadRequest("ID 不匹配");
            }

            // 🔥 改善：添加更新時間
            productAttributeValue.UpdatedAt = DateTime.Now;

            _context.Entry(productAttributeValue).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductAttributeValueExists(id))
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

        // POST: api/ProductAttributeValues
        [HttpPost]
        public async Task<ActionResult<ProductAttributeValue>> PostProductAttributeValue(ProductAttributeValue productAttributeValue)
        {
            try
            {
                // 🔥 改善：添加創建和更新時間
                productAttributeValue.CreatedAt = DateTime.Now;
                productAttributeValue.UpdatedAt = DateTime.Now;

                // 🔥 驗證：檢查商品和屬性值是否存在
                var productExists = await _context.Products.AnyAsync(p => p.Id == productAttributeValue.ProductId);
                if (!productExists)
                {
                    return BadRequest($"商品 ID {productAttributeValue.ProductId} 不存在");
                }

                var attributeValueExists = await _context.AttributeValues.AnyAsync(av => av.Id == productAttributeValue.AttributeValueId);
                if (!attributeValueExists)
                {
                    return BadRequest($"屬性值 ID {productAttributeValue.AttributeValueId} 不存在");
                }

                // 🔥 驗證：檢查 SKU 唯一性
                if (!string.IsNullOrEmpty(productAttributeValue.Sku))
                {
                    var skuExists = await _context.ProductAttributeValues
                        .AnyAsync(pav => pav.Sku == productAttributeValue.Sku && pav.Id != productAttributeValue.Id);

                    if (skuExists)
                    {
                        return BadRequest($"SKU '{productAttributeValue.Sku}' 已存在");
                    }
                }

                _context.ProductAttributeValues.Add(productAttributeValue);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetProductAttributeValue", new { id = productAttributeValue.Id }, productAttributeValue);
            }
            catch (Exception ex)
            {
                return BadRequest($"創建失敗: {ex.Message}");
            }
        }

        [HttpPost("batch")]
        public async Task<ActionResult<IEnumerable<ProductAttributeValue>>> PostProductAttributeValues(
      [FromBody] IEnumerable<ProductAttributeValue> productAttributeValues)
        {
            if (productAttributeValues == null || !productAttributeValues.Any())
            {
                return BadRequest("請提供至少一個商品屬性值");
            }

            Console.WriteLine($"💾 收到批量保存請求，共 {productAttributeValues.Count()} 個記錄");

            var createdValues = new List<ProductAttributeValue>();
            var errors = new List<string>();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int index = 0;
                foreach (var pav in productAttributeValues)
                {
                    index++;
                    Console.WriteLine($"📝 處理第 {index} 個記錄: AttributeValueId={pav.AttributeValueId}, Stock={pav.Stock}, SKU={pav.Sku}, SkuGroupId={pav.SkuGroupId}");

                    // 設定時間戳
                    pav.CreatedAt = DateTime.Now;
                    pav.UpdatedAt = DateTime.Now;

                    // 驗證商品存在
                    var productExists = await _context.Products.AnyAsync(p => p.Id == pav.ProductId);
                    if (!productExists)
                    {
                        errors.Add($"商品 ID {pav.ProductId} 不存在");
                        continue;
                    }

                    // 驗證屬性值存在
                    var attributeValueExists = await _context.AttributeValues.AnyAsync(av => av.Id == pav.AttributeValueId);
                    if (!attributeValueExists)
                    {
                        errors.Add($"屬性值 ID {pav.AttributeValueId} 不存在");
                        continue;
                    }

                    // 🔥 改進：檢查 SKU 唯一性（但允許相同商品的不同組合）
                    if (!string.IsNullOrEmpty(pav.Sku))
                    {
                        var skuExists = await _context.ProductAttributeValues
                            .AnyAsync(existing => existing.Sku == pav.Sku && existing.ProductId != pav.ProductId);

                        if (skuExists)
                        {
                            errors.Add($"SKU '{pav.Sku}' 已被其他商品使用");
                            continue;
                        }
                    }

                    // 🔥 重要：驗證 SkuGroupId（如果提供的話）
                    if (pav.SkuGroupId.HasValue)
                    {
                        var skuGroupExists = await _context.AttributeValues.AnyAsync(av => av.Id == pav.SkuGroupId.Value);
                        if (!skuGroupExists)
                        {
                            errors.Add($"SkuGroup ID {pav.SkuGroupId} 不存在");
                            continue;
                        }
                    }

                    _context.ProductAttributeValues.Add(pav);
                    createdValues.Add(pav);
                    Console.WriteLine($"✅ 記錄 {index} 準備保存");
                }

                if (errors.Any())
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"❌ 批量保存失敗，發現 {errors.Count} 個錯誤");
                    return BadRequest(new
                    {
                        success = false,
                        message = "批量創建過程中發現錯誤",
                        errors = errors
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"✅ 批量保存成功，共創建 {createdValues.Count} 個記錄");

                return Ok(new
                {
                    success = true,
                    message = $"成功創建 {createdValues.Count} 個商品屬性值",
                    createdCount = createdValues.Count,
                    data = createdValues.Select(pav => new {
                        id = pav.Id,
                        productId = pav.ProductId,
                        attributeValueId = pav.AttributeValueId,
                        stock = pav.Stock,
                        sku = pav.Sku,
                        skuGroupId = pav.SkuGroupId,
                        additionalPrice = pav.AdditionalPrice
                    })
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ 批量保存過程中發生錯誤: {ex.Message}");
                return BadRequest($"批量創建失敗: {ex.Message}");
            }
        }

        // 🔥 新增：根據商品 ID 和 SkuGroupId 查詢庫存摘要
        [HttpGet("product/{productId}/stock-by-group")]
        public async Task<ActionResult<object>> GetProductStockByGroup(int productId)
        {
            var stockData = await _context.ProductAttributeValues
                .Where(pav => pav.ProductId == productId)
                .Include(pav => pav.AttributeValue)
                    .ThenInclude(av => av.Attribute)
                .GroupBy(pav => pav.SkuGroupId)
                .Select(g => new
                {
                    SkuGroupId = g.Key,
                    TotalStock = g.Sum(pav => pav.Stock),
                    Details = g.Select(pav => new
                    {
                        Id = pav.Id,
                        AttributeValue = pav.AttributeValue.Value,
                        AttributeName = pav.AttributeValue.Attribute.Name,
                        Stock = pav.Stock,
                        Sku = pav.Sku
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                ProductId = productId,
                TotalStock = stockData.Sum(g => g.TotalStock),
                StockGroups = stockData
            });
        }

        // DELETE: api/ProductAttributeValues/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductAttributeValue(int id)
        {
            var productAttributeValue = await _context.ProductAttributeValues.FindAsync(id);
            if (productAttributeValue == null)
            {
                return NotFound();
            }

            _context.ProductAttributeValues.Remove(productAttributeValue);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 🔥 新增：根據商品 ID 刪除所有屬性值（編輯商品時清理用）
        [HttpDelete("product/{productId}")]
        public async Task<IActionResult> DeleteByProductId(int productId)
        {
            try
            {
                Console.WriteLine($"🗑️ 準備刪除商品 {productId} 的所有屬性值記錄...");

                var existingValues = await _context.ProductAttributeValues
                    .Where(pav => pav.ProductId == productId)
                    .ToListAsync();

                Console.WriteLine($"🔍 找到 {existingValues.Count} 個現有記錄");

                if (existingValues.Any())
                {
                    // 🔥 詳細記錄要刪除的記錄
                    foreach (var record in existingValues)
                    {
                        Console.WriteLine($"📝 將刪除記錄: ID={record.Id}, AttributeValueId={record.AttributeValueId}, Stock={record.Stock}, SKU={record.Sku}, SkuGroupId={record.SkuGroupId}");
                    }

                    _context.ProductAttributeValues.RemoveRange(existingValues);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✅ 成功刪除商品 {productId} 的 {existingValues.Count} 個屬性值記錄");
                }
                else
                {
                    Console.WriteLine($"ℹ️ 商品 {productId} 沒有現有的屬性值記錄");
                }

                return Ok(new
                {
                    success = true,
                    message = $"成功刪除商品 {productId} 的 {existingValues.Count} 個屬性值記錄",
                    deletedCount = existingValues.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刪除商品 {productId} 的屬性值失敗: {ex.Message}");
                return BadRequest(new
                {
                    success = false,
                    message = $"刪除失敗: {ex.Message}"
                });
            }
        }


        // 🔥 新增：批量更新庫存
        [HttpPut("batch-update-stock")]
        public async Task<IActionResult> BatchUpdateStock([FromBody] IEnumerable<StockUpdateRequest> updates)
        {
            if (updates == null || !updates.Any())
            {
                return BadRequest("請提供庫存更新資料");
            }

            var errors = new List<string>();
            var updatedCount = 0;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var update in updates)
                {
                    var pav = await _context.ProductAttributeValues.FindAsync(update.Id);
                    if (pav == null)
                    {
                        errors.Add($"找不到 ID {update.Id} 的屬性值記錄");
                        continue;
                    }

                    pav.Stock = update.NewStock;
                    pav.UpdatedAt = DateTime.Now;
                    updatedCount++;
                }

                if (errors.Any())
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { errors = errors });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = $"成功更新 {updatedCount} 個商品的庫存",
                    updatedCount = updatedCount
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"批量更新庫存失敗: {ex.Message}");
            }
        }

        private bool ProductAttributeValueExists(int id)
        {
            return _context.ProductAttributeValues.Any(e => e.Id == id);
        }
    }

    // 🔥 新增：庫存更新請求 DTO
    public class StockUpdateRequest
    {
        public int Id { get; set; }
        public int NewStock { get; set; }
    }
}