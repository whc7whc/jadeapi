using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class ProductImagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductImagesController> _logger;

        public ProductImagesController(AppDbContext context, ILogger<ProductImagesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/ProductImages
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProductImages()
        {
            try
            {
                _logger.LogInformation("=== GetProductImages 開始 ===");

                // 🔥 修復：避免循環引用，使用 Select 投影而不是 Include
                var productImages = await _context.ProductImages
                    // 2. 修改: 移除 .Include(pi => pi.Product)，因為 DTO 不需要它，可以提升效能
                    .OrderBy(pi => pi.ProductId)
                    .ThenBy(pi => pi.SortOrder)
                    .Select(pi => new
                    {
                        id = pi.Id,
                        productId = pi.ProductId,
                        skuId = pi.SkuId,
                        imagesUrl = pi.ImagesUrl,
                        sortOrder = pi.SortOrder,
                        // 🔥 只取商品的基本資訊，避免循環引用
                        productName = pi.Product != null ? pi.Product.Name : null
                    })
                    .ToListAsync();

                _logger.LogInformation($"✅ 回傳 {productImages.Count} 個商品圖片");
                return Ok(productImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GetProductImages 失敗");
                return StatusCode(500, new { message = "取得商品圖片失敗", error = ex.Message });
            }
        }

        // GET: api/ProductImages/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProductImage(int id)
        {
            try
            {
                _logger.LogInformation($"=== GetProductImage 開始，ID: {id} ===");

                // 🔥 修復：使用 Select 投影避免循環引用
                var productImage = await _context.ProductImages
                    .Where(pi => pi.Id == id)
                    .Select(pi => new
                    {
                        id = pi.Id,
                        productId = pi.ProductId,
                        skuId = pi.SkuId,
                        imagesUrl = pi.ImagesUrl,
                        sortOrder = pi.SortOrder,
                        productName = pi.Product != null ? pi.Product.Name : null
                    })
                    .FirstOrDefaultAsync();

                if (productImage == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的商品圖片");
                    return NotFound(new { message = "找不到指定的商品圖片" });
                }

                _logger.LogInformation($"✅ 成功取得商品圖片: {productImage.imagesUrl}");
                return Ok(productImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 取得 ID 為 {id} 的商品圖片失敗");
                return StatusCode(500, new { message = "取得商品圖片失敗", error = ex.Message });
            }
        }

        // GET: api/ProductImages/product/5
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductImagesByProductId(int productId)
        {
            try
            {
                _logger.LogInformation($"=== GetProductImagesByProductId 開始，商品ID: {productId} ===");

                // 首先檢查商品是否存在
                var productExists = await _context.Products.AnyAsync(p => p.Id == productId);
                if (!productExists)
                {
                    _logger.LogWarning($"找不到 ID 為 {productId} 的商品");
                    return NotFound(new { message = "找不到指定的商品" });
                }

                // 🔥 修復：使用簡單的查詢避免循環引用
                var productImages = await _context.ProductImages
                    .Where(pi => pi.ProductId == productId)
                    .OrderBy(pi => pi.SortOrder)
                    .Select(pi => new
                    {
                        id = pi.Id,
                        productId = pi.ProductId,
                        skuId = pi.SkuId,
                        imagesUrl = pi.ImagesUrl,
                        sortOrder = pi.SortOrder
                    })
                    .ToListAsync();

                _logger.LogInformation($"✅ 找到 {productImages.Count} 個商品圖片");
                return Ok(productImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 取得商品 ID 為 {productId} 的圖片失敗");
                return StatusCode(500, new { message = "取得商品圖片失敗", error = ex.Message });
            }
        }

        // PUT: api/ProductImages/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProductImage(int id, ProductImage productImage)
        {
            if (id != productImage.Id)
            {
                return BadRequest(new { message = "ID 不匹配" });
            }

            try
            {
                _logger.LogInformation($"=== PutProductImage 開始，ID: {id} ===");

                // 檢查商品圖片是否存在
                var existingProductImage = await _context.ProductImages.FindAsync(id);
                if (existingProductImage == null)
                {
                    return NotFound(new { message = "找不到指定的商品圖片" });
                }

                // 檢查商品是否存在
                var productExists = await _context.Products.AnyAsync(p => p.Id == productImage.ProductId);
                if (!productExists)
                {
                    return BadRequest(new { message = "指定的商品不存在" });
                }

                // 更新商品圖片
                existingProductImage.ImagesUrl = productImage.ImagesUrl;
                existingProductImage.SortOrder = productImage.SortOrder;
                existingProductImage.ProductId = productImage.ProductId;
                existingProductImage.SkuId = productImage.SkuId;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 商品圖片更新成功，ID: {id}");
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductImageExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新商品圖片失敗，ID: {id}");
                return StatusCode(500, new { message = "更新商品圖片失敗", error = ex.Message });
            }
        }

        // POST: api/ProductImages
        [HttpPost]
        public async Task<ActionResult<object>> PostProductImage(ProductImage productImage)
        {
            // 注意：這裡的輸入參數 productImage 仍然可以是 ProductImage 實體，
            // 因為前端傳來的是完整的資料用於創建。也可以建立一個 CreateProductImageDto 來接收，會更嚴謹。
            try
            {
                _logger.LogInformation("=== PostProductImage 開始 ===");
                _logger.LogInformation($"商品ID: {productImage.ProductId}, 圖片URL: {productImage.ImagesUrl}");

                // 檢查商品是否存在
                var productExists = await _context.Products.AnyAsync(p => p.Id == productImage.ProductId);
                if (!productExists)
                {
                    _logger.LogWarning($"商品 ID {productImage.ProductId} 不存在");
                    return BadRequest(new { message = "指定的商品不存在" });
                }

                // 驗證必要欄位
                if (string.IsNullOrEmpty(productImage.ImagesUrl))
                {
                    return BadRequest(new { message = "圖片 URL 不能為空" });
                }

                // 如果沒有指定排序，自動設定為最後一個
                if (productImage.SortOrder <= 0)
                {
                    var maxSortOrder = await _context.ProductImages
                        .Where(pi => pi.ProductId == productImage.ProductId)
                        .MaxAsync(pi => (int?)pi.SortOrder) ?? 0;

                    productImage.SortOrder = maxSortOrder + 1;
                }


                _context.ProductImages.Add(productImage);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 商品圖片建立成功，ID: {productImage.Id}");

                // 🔥 修復：回傳簡化的物件避免循環引用
                var result = new
                {
                    id = productImage.Id,
                    productId = productImage.ProductId,
                    skuId = productImage.SkuId,
                    imagesUrl = productImage.ImagesUrl,
                    sortOrder = productImage.SortOrder
                };

                return CreatedAtAction("GetProductImage", new { id = productImage.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 建立商品圖片失敗");
                return StatusCode(500, new { message = "建立商品圖片失敗", error = ex.Message });
            }
        }

        // POST: api/ProductImages/batch
        [HttpPost("batch")]
        public async Task<ActionResult> PostProductImagesBatch([FromBody] List<ProductImage> productImages)
        {
            try
            {
                _logger.LogInformation($"=== PostProductImagesBatch 開始，批量建立 {productImages.Count} 個商品圖片 ===");

                if (productImages == null || !productImages.Any())
                {
                    return BadRequest(new { message = "商品圖片列表不能為空" });
                }

                var results = new List<object>();
                var failedImages = new List<object>();

                foreach (var productImage in productImages)
                {
                    try
                    {
                        // 檢查商品是否存在
                        var productExists = await _context.Products.AnyAsync(p => p.Id == productImage.ProductId);
                        if (!productExists)
                        {
                            failedImages.Add(new
                            {
                                productId = productImage.ProductId,
                                imagesUrl = productImage.ImagesUrl,
                                error = "商品不存在"
                            });
                            continue;
                        }

                        // 驗證圖片 URL
                        if (string.IsNullOrEmpty(productImage.ImagesUrl))
                        {
                            failedImages.Add(new
                            {
                                productId = productImage.ProductId,
                                imagesUrl = productImage.ImagesUrl,
                                error = "圖片 URL 不能為空"
                            });
                            continue;
                        }

                        // 如果沒有指定排序，自動設定
                        if (productImage.SortOrder <= 0)
                        {
                            var maxSortOrder = await _context.ProductImages
                                .Where(pi => pi.ProductId == productImage.ProductId)
                                .MaxAsync(pi => (int?)pi.SortOrder) ?? 0;

                            productImage.SortOrder = maxSortOrder + 1;
                        }

                        _context.ProductImages.Add(productImage);
                        await _context.SaveChangesAsync();

                        // 🔥 修復：結果物件避免循環引用
                        results.Add(new
                        {
                            id = productImage.Id,
                            productId = productImage.ProductId,
                            skuId = productImage.SkuId,
                            imagesUrl = productImage.ImagesUrl,
                            sortOrder = productImage.SortOrder,
                            success = true
                        });

                        _logger.LogInformation($"✅ 商品圖片建立成功，ID: {productImage.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ 建立商品圖片失敗: {productImage.ImagesUrl}");
                        failedImages.Add(new
                        {
                            productId = productImage.ProductId,
                            imagesUrl = productImage.ImagesUrl,
                            error = ex.Message
                        });
                    }
                }

                _logger.LogInformation($"✅ 批量建立完成，成功: {results.Count}，失敗: {failedImages.Count}");

                return Ok(new
                {
                    success = true,
                    message = $"成功建立 {results.Count} 個商品圖片" +
                             (failedImages.Count > 0 ? $"，{failedImages.Count} 個失敗" : ""),
                    createdImages = results,
                    failedImages = failedImages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 批量建立商品圖片失敗");
                return StatusCode(500, new { message = "批量建立商品圖片失敗", error = ex.Message });
            }
        }

        // DELETE: api/ProductImages/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductImage(int id)
        {
            try
            {
                _logger.LogInformation($"=== DeleteProductImage 開始，ID: {id} ===");

                var productImage = await _context.ProductImages.FindAsync(id);
                if (productImage == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的商品圖片");
                    return NotFound(new { message = "找不到指定的商品圖片" });
                }

                _context.ProductImages.Remove(productImage);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 商品圖片刪除成功，ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 刪除商品圖片失敗，ID: {id}");
                return StatusCode(500, new { message = "刪除商品圖片失敗", error = ex.Message });
            }
        }

        // DELETE: api/ProductImages/product/5
        [HttpDelete("product/{productId}")]
        public async Task<IActionResult> DeleteProductImagesByProductId(int productId)
        {
            try
            {
                _logger.LogInformation($"=== DeleteProductImagesByProductId 開始，商品ID: {productId} ===");

                var productImages = await _context.ProductImages
                    .Where(pi => pi.ProductId == productId)
                    .ToListAsync();

                if (!productImages.Any())
                {
                    _logger.LogInformation($"商品 ID {productId} 沒有圖片需要刪除");
                    return NoContent();
                }

                _context.ProductImages.RemoveRange(productImages);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 成功刪除商品 ID {productId} 的 {productImages.Count} 個圖片");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 刪除商品 ID {productId} 的圖片失敗");
                return StatusCode(500, new { message = "刪除商品圖片失敗", error = ex.Message });
            }
        }

        // PUT: api/ProductImages/reorder
        [HttpPut("reorder")]
        public async Task<IActionResult> ReorderProductImages([FromBody] List<ProductImageReorderRequest> reorderRequests)
        {
            try
            {
                _logger.LogInformation($"=== ReorderProductImages 開始，重新排序 {reorderRequests.Count} 個圖片 ===");

                foreach (var request in reorderRequests)
                {
                    var productImage = await _context.ProductImages.FindAsync(request.Id);
                    if (productImage != null)
                    {
                        productImage.SortOrder = request.SortOrder;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ 商品圖片重新排序成功");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 重新排序商品圖片失敗");
                return StatusCode(500, new { message = "重新排序商品圖片失敗", error = ex.Message });
            }
        }

        private bool ProductImageExists(int id)
        {
            return _context.ProductImages.Any(e => e.Id == id);
        }
    }

    // 重新排序請求模型
    public class ProductImageReorderRequest
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}