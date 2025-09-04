using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using System.IO;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(AppDbContext context, Cloudinary cloudinary, ILogger<ProductsController> logger)
        {
            _context = context;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        // GET: api/Products  (回傳含圖片與規格的 DTO)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProducts()
        {
            try
            {
                _logger.LogInformation("=== GetProducts 開始 ===");

                var productsQuery = await _context.Products
                    .Include(p => p.SubCategory)
                        .ThenInclude(sc => sc.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductAttributeValues)
                        .ThenInclude(pav => pav.AttributeValue)
                            .ThenInclude(av => av.Attribute)
                    .Include(p => p.Reviews)
                    .Where(p => p.IsActive == true)
                    .ToListAsync();

                var products = productsQuery.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    description = p.Description,
                    price = p.IsDiscount == true && p.DiscountPrice.HasValue ? p.DiscountPrice.Value : p.Price,
                    originalPrice = p.Price,
                    isDiscount = p.IsDiscount,
                    discountPrice = p.DiscountPrice,
                    stock = p.ProductAttributeValues?.Sum(pav => pav.Stock) ?? 0,
                    categoryId = p.SubCategory?.Category?.Id,
                    subCategoryId = p.SubCategoryId,
                    sellerId = p.SellersId,
                    rating = p.Reviews != null && p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 4.0,
                    soldCount = 0,
                    isNew = p.CreatedAt >= DateTime.Now.AddDays(-30),
                    isActive = p.IsActive,
                    createdAt = p.CreatedAt,
                    updatedAt = p.UpdatedAt,
                    productImages = p.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .Select(pi => new
                        {
                            id = pi.Id,
                            imagePath = ValidateImageUrl(pi.ImagesUrl),
                            imagesUrl = pi.ImagesUrl,
                            sortOrder = pi.SortOrder
                        })
                        .ToList(),
                    productAttributeValues = p.ProductAttributeValues
                        .Select(pav => new
                        {
                            id = pav.Id,
                            productId = pav.ProductId,
                            attributeValueId = pav.AttributeValueId,
                            stock = pav.Stock,
                            sku = pav.Sku,
                            additionalPrice = pav.AdditionalPrice,
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
                        })
                        .Where(pav => pav.attributeValue != null)
                        .ToList()
                }).ToList();

                _logger.LogInformation($"✅ 回傳 {products.Count} 個商品");
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GetProducts 失敗");
                return StatusCode(500, new { message = "取得商品失敗", error = ex.Message });
            }
        }

        // GET: api/Products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProduct(int id)
        {
            try
            {
                _logger.LogInformation($"=== GetProduct 開始，ID: {id} ===");

                var productEntity = await _context.Products
                    .Include(p => p.SubCategory)
                        .ThenInclude(sc => sc.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductAttributeValues)
                        .ThenInclude(pav => pav.AttributeValue)
                            .ThenInclude(av => av.Attribute)
                    .Include(p => p.Reviews)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (productEntity == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的商品");
                    return NotFound(new { message = "找不到指定的商品" });
                }

                var product = new
                {
                    id = productEntity.Id,
                    name = productEntity.Name,
                    description = productEntity.Description,
                    price = productEntity.IsDiscount == true ? productEntity.DiscountPrice ?? productEntity.Price : productEntity.Price,
                    originalPrice = productEntity.Price,
                    isDiscount = productEntity.IsDiscount,
                    discountPrice = productEntity.DiscountPrice,
                    stock = productEntity.ProductAttributeValues?.Sum(pav => pav.Stock) ?? 0,
                    categoryId = productEntity.SubCategory?.Category?.Id,
                    subCategoryId = productEntity.SubCategoryId,
                    sellerId = productEntity.SellersId,
                    rating = productEntity.Reviews != null && productEntity.Reviews.Any() ? productEntity.Reviews.Average(r => r.Rating) : 4.0,
                    soldCount = 0,
                    isNew = productEntity.CreatedAt >= DateTime.Now.AddDays(-30),
                    isActive = productEntity.IsActive,
                    createdAt = productEntity.CreatedAt,
                    updatedAt = productEntity.UpdatedAt,
                    productImages = productEntity.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .Select(pi => new
                        {
                            id = pi.Id,
                            imagePath = ValidateImageUrl(pi.ImagesUrl),
                            imagesUrl = pi.ImagesUrl,
                            sortOrder = pi.SortOrder
                        })
                        .ToList(),
                    // 🔥 確保完整包含 SkuGroupId 欄位
                    productAttributeValues = productEntity.ProductAttributeValues
                        .Select(pav => new
                        {
                            id = pav.Id,
                            productId = pav.ProductId,
                            attributeValueId = pav.AttributeValueId,
                            stock = pav.Stock,
                            sku = pav.Sku,
                            skuGroupId = pav.SkuGroupId, // 🔥 確保包含此欄位
                            additionalPrice = pav.AdditionalPrice,
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
                        })
                        .Where(pav => pav.attributeValue != null)
                        .ToList()
                };

                // 🔥 調試：打印屬性值資料
                _logger.LogInformation($"🔍 商品 {id} 的屬性值數量: {product.productAttributeValues.Count}");
                foreach (var pav in product.productAttributeValues)
                {
                    _logger.LogInformation($"📊 AttributeId: {pav.attributeValue?.attributeId}, Value: {pav.attributeValue?.value}, Stock: {pav.stock}, SkuGroupId: {pav.skuGroupId}");
                }

                _logger.LogInformation($"✅ 成功取得商品: {product.name}");
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 取得 ID 為 {id} 的商品失敗");
                return StatusCode(500, new { message = "取得商品失敗", error = ex.Message });
            }
        }

        // GET: api/Products/by-style/{styleAttributeValueId}
        [HttpGet("by-style/{styleAttributeValueId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductsByStyle(int styleAttributeValueId)
        {
            try
            {
                _logger.LogInformation($"=== GetProductsByStyle 開始，風格ID: {styleAttributeValueId} ===");

                var productsQuery = await _context.Products
                    .Include(p => p.SubCategory)
                        .ThenInclude(sc => sc.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductAttributeValues)
                        .ThenInclude(pav => pav.AttributeValue)
                            .ThenInclude(av => av.Attribute)
                    .Include(p => p.Reviews)
                    .Where(p => p.IsActive == true &&
                               p.ProductAttributeValues.Any(pav => pav.AttributeValueId == styleAttributeValueId))
                    .ToListAsync();

                var products = productsQuery.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    description = p.Description,
                    price = p.IsDiscount == true ? p.DiscountPrice ?? p.Price : p.Price,
                    originalPrice = p.Price,
                    stock = p.ProductAttributeValues?.Sum(pav => pav.Stock) ?? 0,
                    categoryId = p.SubCategory?.Category?.Id,
                    subCategoryId = p.SubCategoryId,
                    rating = p.Reviews != null && p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 4.0,
                    soldCount = 0,
                    isNew = p.CreatedAt >= DateTime.Now.AddDays(-30),
                    productImages = p.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .Select(pi => new
                        {
                            id = pi.Id,
                            imagePath = ValidateImageUrl(pi.ImagesUrl),
                            sortOrder = pi.SortOrder
                        })
                        .ToList()
                }).ToList();

                _logger.LogInformation($"✅ 找到 {products.Count} 個風格商品");
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 取得風格商品失敗");
                return StatusCode(500, new { message = "取得風格商品失敗", error = ex.Message });
            }
        }

        // GET: api/Products/by-category/{categoryId}
        [HttpGet("by-category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductsByCategory(int categoryId)
        {
            try
            {
                _logger.LogInformation($"=== GetProductsByCategory 開始，分類ID: {categoryId} ===");

                var productsQuery = await _context.Products
                    .Include(p => p.SubCategory)
                        .ThenInclude(sc => sc.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.Reviews)
                    .Where(p => p.IsActive == true &&
                               (p.SubCategory.CategoryId == categoryId || p.SubCategory.Category.Id == categoryId))
                    .ToListAsync();

                var products = productsQuery.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    price = p.IsDiscount == true ? p.DiscountPrice ?? p.Price : p.Price,
                    originalPrice = p.Price,
                    categoryId = p.SubCategory?.Category?.Id,
                    subCategoryId = p.SubCategoryId,
                    rating = p.Reviews != null && p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 4.0,
                    soldCount = 0,
                    isNew = p.CreatedAt >= DateTime.Now.AddDays(-30),
                    productImages = p.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .Select(pi => new
                        {
                            id = pi.Id,
                            imagePath = ValidateImageUrl(pi.ImagesUrl),
                            sortOrder = pi.SortOrder
                        })
                        .ToList()
                }).ToList();

                _logger.LogInformation($"✅ 找到 {products.Count} 個分類商品");
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 取得分類商品失敗");
                return StatusCode(500, new { message = "取得分類商品失敗", error = ex.Message });
            }
        }

        // 🔥 商品圖片上傳 API（參考 PostsController）
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadProductImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "沒有選擇檔案" });
            }

            try
            {
                _logger.LogInformation($"開始上傳商品圖片: {file.FileName}");

                var imageUrl = await UploadToCloudinary(file);
                if (imageUrl != null)
                {
                    _logger.LogInformation($"✅ 商品圖片上傳成功: {imageUrl}");
                    return Ok(new
                    {
                        success = true,
                        url = imageUrl,
                        fileName = Path.GetFileName(file.FileName),
                        size = file.Length,
                        uploadedAt = DateTime.Now
                    });
                }
                else
                {
                    _logger.LogWarning("❌ 商品圖片上傳失敗");
                    return BadRequest(new { success = false, message = "上傳失敗，請檢查檔案格式和大小" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品圖片上傳過程發生錯誤");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 🔥 批量上傳商品圖片
        [HttpPost("upload-multiple-images")]
        public async Task<IActionResult> UploadMultipleProductImages(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { success = false, message = "沒有選擇檔案" });
            }

            if (files.Count > 10)
            {
                return BadRequest(new { success = false, message = "一次最多只能上傳 10 張圖片" });
            }

            try
            {
                _logger.LogInformation($"開始批量上傳 {files.Count} 張商品圖片");

                var uploadResults = new List<object>();
                var failedUploads = new List<string>();

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    try
                    {
                        var imageUrl = await UploadToCloudinary(file);
                        if (imageUrl != null)
                        {
                            uploadResults.Add(new
                            {
                                success = true,
                                url = imageUrl,
                                fileName = Path.GetFileName(file.FileName),
                                size = file.Length,
                                sortOrder = i + 1
                            });
                        }
                        else
                        {
                            failedUploads.Add(file.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"上傳檔案 {file.FileName} 失敗");
                        failedUploads.Add(file.FileName);
                    }
                }

                _logger.LogInformation($"✅ 批量上傳完成，成功: {uploadResults.Count}，失敗: {failedUploads.Count}");

                return Ok(new
                {
                    success = true,
                    message = $"成功上傳 {uploadResults.Count} 張圖片" +
                             (failedUploads.Count > 0 ? $"，{failedUploads.Count} 張失敗" : ""),
                    uploadedImages = uploadResults,
                    failedFiles = failedUploads
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量上傳商品圖片過程發生錯誤");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // PUT: api/Products/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduct(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
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

        [HttpPost]
        public async Task<ActionResult<object>> PostProduct([FromBody] ProductCreateRequest request)
        {
            try
            {
                _logger.LogInformation("=== 開始創建商品 ===");
                _logger.LogInformation($"收到的資料: {System.Text.Json.JsonSerializer.Serialize(request)}");

                // 🔥 驗證必要欄位
                if (string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(new { message = "商品名稱不能為空" });
                }

                if (request.SubCategoryId <= 0)
                {
                    return BadRequest(new { message = "必須選擇子分類" });
                }

                if (request.Price <= 0)
                {
                    return BadRequest(new { message = "商品價格必須大於 0" });
                }

                // 檢查子分類是否存在
                var subCategoryExists = await _context.SubCategories.AnyAsync(sc => sc.Id == request.SubCategoryId);
                if (!subCategoryExists)
                {
                    return BadRequest(new { message = "指定的子分類不存在" });
                }

                // 創建商品
                var product = new Product
                {
                    Name = request.Name,
                    Description = request.Description,
                    Price = request.Price,
                    IsDiscount = request.IsDiscount,
                    DiscountPrice = request.DiscountPrice,
                    SubCategoryId = request.SubCategoryId,
                    SellersId = request.SellersId,
                    IsActive = request.IsActive,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 商品建立成功，ID: {product.Id}");

                // 建立商品圖片
                var createdProductImages = new List<object>();
                if (request.ProductImages != null && request.ProductImages.Any())
                {
                    _logger.LogInformation($"📸 開始保存 {request.ProductImages.Count} 個商品圖片");

                    foreach (var imageData in request.ProductImages)
                    {
                        var productImage = new ProductImage
                        {
                            ProductId = product.Id,
                            ImagesUrl = imageData.ImagesUrl,
                            SortOrder = imageData.SortOrder
                        };
                        _context.ProductImages.Add(productImage);
                        await _context.SaveChangesAsync();

                        createdProductImages.Add(new
                        {
                            id = productImage.Id,
                            productId = productImage.ProductId,
                            imagesUrl = productImage.ImagesUrl,
                            sortOrder = productImage.SortOrder
                        });
                    }
                    _logger.LogInformation("✅ 商品圖片保存完成");
                }

                // 🔥 重要修改：不在這裡創建 ProductAttributeValues
                // 商品屬性值（包括風格、顏色、尺寸組合）將由前端在第二步驟中通過專用 API 創建
                _logger.LogInformation("⚠️ 商品屬性值將由前端另外處理");

                var result = new
                {
                    id = product.Id,
                    name = product.Name,
                    description = product.Description,
                    price = product.Price,
                    isDiscount = product.IsDiscount,
                    discountPrice = product.DiscountPrice,
                    subCategoryId = product.SubCategoryId,
                    sellersId = product.SellersId,
                    isActive = product.IsActive,
                    createdAt = product.CreatedAt,
                    updatedAt = product.UpdatedAt,
                    productImages = createdProductImages,
                    productAttributeValues = new List<object>() // 空陣列，後續填入
                };

                return CreatedAtAction("GetProduct", new { id = product.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 創建商品失敗");
                return StatusCode(500, new { message = "創建商品失敗", error = ex.Message });
            }
        }

        // 🔥 更新請求模型，加入 StyleId
        public class ProductCreateRequest
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int Price { get; set; }
            public bool IsDiscount { get; set; }
            public int? DiscountPrice { get; set; }
            public int SubCategoryId { get; set; }
            public int SellersId { get; set; }
            public bool IsActive { get; set; }
            public int? StyleId { get; set; } // 🔥 新增風格 ID 欄位
            public List<ProductImageData> ProductImages { get; set; }
        }

        public class ProductImageData
        {
            public string ImagesUrl { get; set; }
            public int SortOrder { get; set; }
        }

        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                _logger.LogInformation($"=== DeleteProduct 開始，ID: {id} ===");

                var product = await _context.Products
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    _logger.LogWarning($"找不到 ID 為 {id} 的商品");
                    return NotFound(new { message = "找不到指定的商品" });
                }

                // 🔥 刪除關聯的圖片
                if (product.ProductImages != null && product.ProductImages.Any())
                {
                    foreach (var productImage in product.ProductImages)
                    {
                        if (!string.IsNullOrEmpty(productImage.ImagesUrl))
                        {
                            await DeleteCloudinaryImage(productImage.ImagesUrl);
                        }
                    }
                }

                // 軟刪除 - 設為不活躍而不是真的刪除
                product.IsActive = false;
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 商品 {product.Name} 已成功刪除（軟刪除）");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 刪除 ID 為 {id} 的商品失敗");
                return StatusCode(500, new { message = "刪除商品失敗", error = ex.Message });
            }
        }

        #region 私有方法（參考 PostsController 實作）

        private static string ValidateImageUrl(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return GetDefaultImageUrl();
            }
            if (imageUrl.Contains("cloudinary.com") && imageUrl.Contains("sample_tshirt"))
            {
                return GetDefaultImageUrl();
            }
            return imageUrl;
        }

        private static string GetDefaultImageUrl()
        {
            return "https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=500&h=500&fit=crop&auto=format";
        }

        private async Task<string?> UploadToCloudinary(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                    return null;

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
                {
                    _logger.LogWarning($"不支援的檔案類型: {imageFile.ContentType}");
                    return null;
                }

                const int maxFileSize = 5 * 1024 * 1024;
                if (imageFile.Length > maxFileSize)
                {
                    _logger.LogWarning($"檔案過大: {imageFile.Length} bytes (限制: {maxFileSize} bytes)");
                    return null;
                }

                using var stream = imageFile.OpenReadStream();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    PublicId = $"jade_product_{DateTime.Now.Ticks}",
                    Folder = "jade-products",
                    Transformation = new Transformation()
                        .Width(800).Height(800).Crop("limit")
                        .Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.LogInformation($"✅ 商品圖片上傳成功: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    _logger.LogWarning($"❌ 商品圖片上傳失敗: {uploadResult.Error?.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品圖片上傳過程發生錯誤");
                return null;
            }
        }

        private async Task<bool> DeleteCloudinaryImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("cloudinary.com"))
                    return true;

                var uri = new Uri(imageUrl);
                var pathParts = uri.AbsolutePath.Split('/');
                var versionIndex = Array.FindIndex(pathParts, part => part.StartsWith("v"));

                if (versionIndex > 0 && versionIndex < pathParts.Length - 1)
                {
                    var publicIdParts = pathParts.Skip(versionIndex + 1).ToArray();
                    var publicId = string.Join("/", publicIdParts);

                    var lastDotIndex = publicId.LastIndexOf('.');
                    if (lastDotIndex > 0)
                    {
                        publicId = publicId.Substring(0, lastDotIndex);
                    }

                    var deleteParams = new DeletionParams(publicId)
                    {
                        ResourceType = ResourceType.Image
                    };

                    var result = await _cloudinary.DestroyAsync(deleteParams);

                    if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        _logger.LogInformation($"✅ Cloudinary 商品圖片刪除成功: {publicId}");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"❌ Cloudinary 商品圖片刪除失敗: {result.Error?.Message}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除 Cloudinary 商品圖片時發生錯誤");
                return false;
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        #endregion
    }
}
