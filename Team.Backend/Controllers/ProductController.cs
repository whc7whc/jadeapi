using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Team.Backend.Models;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using ClosedXML.Excel; // Excel 匯出
using System.IO;
using System;

namespace Team.Backend.Controllers
{
    public class ProductController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductController> _logger;

        public ProductController(AppDbContext context, ILogger<ProductController> logger)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }
        //public IActionResult Categories()
        //{
        //    return View();
        //}
        [HttpGet]
        public async Task<IActionResult> Products()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.SubCategory)
                        .ThenInclude(sc => sc.Category)
                    .Include(p => p.Sellers)
                    .Include(p => p.ProductImages)  // 🔥 關鍵：載入產品圖片
                    .Include(p => p.ProductAttributeValues)  // 🔥 載入庫存資料
                    .ToListAsync();

                var productViewModels = products.Select(p => new ProductViewModel
                {
                    Id = p.Id,  // 🔥 確保 ID 有對應
                    Sku = GenerateSkuFromProduct(p), // 🔥 從 ProductAttributeValues 生成 SKU
                    Name = p.Name,
                    Price = p.Price,
                    Stock = CalculateTotalStock(p), // 🔥 從 ProductAttributeValues 計算總庫存
                    SafetyStock = 10, // 🔥 預設安全庫存，您可以調整
                    StockStatus = GetStockStatus(CalculateTotalStock(p)),
                    SellerName = p.Sellers?.RealName ?? "未知供應商",
                    ImageUrl = GetPrimaryImageUrl(p), // 🔥 取得主要圖片 URL
                    CategoryId = p.SubCategory?.CategoryId ?? 0,
                    IsActive = p.IsActive // 🔥 商品狀態
                }).ToList();

                return View(productViewModels);
            }
            catch (Exception ex)
            {
                // 記錄錯誤
                Console.WriteLine($"載入商品資料錯誤: {ex.Message}");
                return View(new List<ProductViewModel>());
            }
        }

        // 🔥 匯出目前篩選的商品至 Excel
        [HttpPost]
        public async Task<IActionResult> Export([FromBody] ExportRequest request)
        {
            if (request == null || request.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest("沒有要匯出的商品");
            }

            var products = await _context.Products
                .Where(p => request.Ids.Contains(p.Id))
                .Include(p => p.Sellers)
                .Include(p => p.ProductAttributeValues)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Products");

            // 標題列
            ws.Cell(1, 1).Value = "商品ID";
            ws.Cell(1, 2).Value = "SKU";
            ws.Cell(1, 3).Value = "商品名稱";
            ws.Cell(1, 4).Value = "價格";
            ws.Cell(1, 5).Value = "庫存數量";
            ws.Cell(1, 6).Value = "安全庫存量";
            ws.Cell(1, 7).Value = "庫存狀態";
            ws.Cell(1, 8).Value = "供應商";
            ws.Cell(1, 9).Value = "商品狀態";
            ws.Range(1, 1, 1, 9).Style.Font.SetBold();

            var row = 2;
            foreach (var p in products)
            {
                var stock = CalculateTotalStock(p);
                ws.Cell(row, 1).Value = p.Id;
                ws.Cell(row, 2).Value = GenerateSkuFromProduct(p);
                ws.Cell(row, 3).Value = p.Name;
                ws.Cell(row, 4).Value = p.Price;
                ws.Cell(row, 5).Value = stock;
                ws.Cell(row, 6).Value = 10; // 與畫面一致的 SafetyStock 預設值
                ws.Cell(row, 7).Value = GetStockStatus(stock);
                ws.Cell(row, 8).Value = p.Sellers?.RealName ?? "未知供應商";
                ws.Cell(row, 9).Value = p.IsActive ? "上架" : "下架";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);
            var fileName = $"products_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public class ExportRequest
        {
            public List<int> Ids { get; set; } = new();
        }

        // 🔥 新增：切換商品狀態（上架/下架）
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "不合法的商品 ID" });

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return Json(new { success = false, message = "找不到商品" });

            product.IsActive = !product.IsActive;
            // 若有 UpdatedAt 欄位可更新時間（避免無此屬性編譯錯誤，使用動態方式處理）
            try
            {
                var prop = product.GetType().GetProperty("UpdatedAt");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(product, DateTime.Now);
                }
            }
            catch { }

            await _context.SaveChangesAsync();

            return Json(new { success = true, isActive = product.IsActive });
        }

        // 🔥 新增：永久刪除（真刪除）
        [HttpPost]
        public async Task<IActionResult> DeletePermanent(int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "不合法的商品 ID" });

            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.ProductAttributeValues)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return Json(new { success = false, message = "找不到商品" });

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 先刪關聯資料，避免外鍵限制
                if (product.ProductImages?.Any() == true)
                {
                    _context.ProductImages.RemoveRange(product.ProductImages);
                }
                if (product.ProductAttributeValues?.Any() == true)
                {
                    _context.ProductAttributeValues.RemoveRange(product.ProductAttributeValues);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return Json(new { success = false, message = $"刪除失敗：{ex.Message}" });
            }
        }

        // 🔥 新增：輔助方法來取得主要圖片 URL
        private string GetPrimaryImageUrl(Product product)
        {
            try
            {
                if (product.ProductImages != null && product.ProductImages.Any())
                {
                    // 取得排序第一的圖片
                    var primaryImage = product.ProductImages
                        .OrderBy(pi => pi.SortOrder)
                        .FirstOrDefault();

                    if (primaryImage != null && !string.IsNullOrEmpty(primaryImage.ImagesUrl))
                    {
                        return primaryImage.ImagesUrl; // 🔥 這裡應該是完整的 Cloudinary URL
                    }
                }
                return string.Empty; // 🔥 沒有圖片時返回空字串
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取得圖片 URL 錯誤: {ex.Message}");
                return string.Empty;
            }
        }

        // 🔥 新增：計算總庫存
        private int CalculateTotalStock(Product product)
        {
            try
            {
                if (product.ProductAttributeValues != null && product.ProductAttributeValues.Any())
                {
                    return product.ProductAttributeValues.Sum(pav => pav.Stock);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"計算庫存錯誤: {ex.Message}");
                return 0;
            }
        }

        // 🔥 新增：生成 SKU
        private string GenerateSkuFromProduct(Product product)
        {
            try
            {
                if (product.ProductAttributeValues != null && product.ProductAttributeValues.Any())
                {
                    // 取得第一個 SKU 作為代表
                    var firstSku = product.ProductAttributeValues.FirstOrDefault()?.Sku;
                    if (!string.IsNullOrEmpty(firstSku))
                    {
                        // 如果 SKU 格式是 "productId-color-size"，只取前面兩部分
                        var skuParts = firstSku.Split('-');
                        if (skuParts.Length >= 2)
                        {
                            return $"{skuParts[0]}-{skuParts[1]}"; // 返回 "productId-color"
                        }
                        return firstSku;
                    }
                }
                return $"PRD-{product.Id:D6}"; // 預設 SKU 格式
            }
            catch (Exception ex)
            {
                Console.WriteLine($"生成 SKU 錯誤: {ex.Message}");
                return $"PRD-{product.Id:D6}";
            }
        }

        // 🔥 新增：取得庫存狀態
        private string GetStockStatus(int stock)
        {
            if (stock <= 0) return "缺貨";
            if (stock <= 10) return "低庫存"; // 您可以調整這個閾值
            return "正常";
        }

    }
}
