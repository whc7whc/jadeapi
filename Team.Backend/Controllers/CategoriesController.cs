using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace Team.Backend.Controllers
{
    public class CategoriesController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(AppDbContext context, ILogger<CategoriesController> logger)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Categories/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                // 分別查詢分類和子分類，避免複雜的 Include 操作
                var categories = await _context.Categories
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var subCategories = await _context.SubCategories
                    .Include(sc => sc.Category)
                    .OrderBy(sc => sc.Category.Name)
                    .ThenBy(sc => sc.Name)
                    .ToListAsync();

                // 創建階層式顯示的視圖模型
                var hierarchicalData = new List<CategoryHierarchyViewModel>();

                foreach (var category in categories)
                {
                    // 添加父分類行
                    hierarchicalData.Add(new CategoryHierarchyViewModel
                    {
                        CategoryId = category.Id,
                        CategoryName = category.Name,
                        SubCategoryId = null,
                        SubCategoryName = null,
                        Level = 0, // 父分類層級
                        IsParent = true
                    });

                    // 添加該分類下的子分類行
                    var categorySubCategories = subCategories
                        .Where(sc => sc.CategoryId == category.Id)
                        .OrderBy(sc => sc.Name);

                    foreach (var subCategory in categorySubCategories)
                    {
                        hierarchicalData.Add(new CategoryHierarchyViewModel
                        {
                            CategoryId = category.Id,
                            CategoryName = category.Name,
                            SubCategoryId = subCategory.Id,
                            SubCategoryName = subCategory.Name,
                            SubCategoryDescription = subCategory.Description,
                            Level = 1, // 子分類層級
                            IsParent = false
                        });
                    }
                }

                return View(hierarchicalData);
            }
            catch (Exception)
			{
                // 記錄錯誤並返回空列表
                // 在實際應用中，你可能想要記錄到日誌系統
                ViewBag.ErrorMessage = "載入分類資料時發生錯誤，請稍後再試。";
                return View(new List<CategoryHierarchyViewModel>());
            }
        }

        // GET: /Categories/GetCategories (用於Modal下拉選單)
        [HttpGet]
        public async Task<JsonResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Select(c => new { Id = c.Id, Name = c.Name })
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Json(categories);
            }
            catch (Exception)
			{
                return Json(new { error = "載入分類失敗" });
            }
        }

        // POST: /Categories/CreateSubCategory (新增子分類)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubCategory(int CategoryId, string SubCategoryName, string? Description)
        {
            if (string.IsNullOrEmpty(SubCategoryName) || CategoryId <= 0)
            {
                TempData["ErrorMessage"] = "請確保選擇了父分類並輸入了子分類名稱";
                return RedirectToAction("Index");
            }

            try
            {
                // 檢查父分類是否存在
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == CategoryId);
                if (!categoryExists)
                {
                    TempData["ErrorMessage"] = "所選的父分類不存在";
                    return RedirectToAction("Index");
                }

                // 檢查同一父分類下是否已存在相同名稱的子分類
                var duplicateExists = await _context.SubCategories
                    .AnyAsync(sc => sc.CategoryId == CategoryId && sc.Name == SubCategoryName);

                if (duplicateExists)
                {
                    TempData["ErrorMessage"] = "該分類下已存在相同名稱的子分類";
                    return RedirectToAction("Index");
                }

                // 內容長度限制（對應 DB 既有慣例)
                if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 255)
                {
                    Description = Description.Substring(0, 255);
                }

                var subCategory = new SubCategory { CategoryId = CategoryId, Name = SubCategoryName, Description = Description };
                _context.SubCategories.Add(subCategory);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "子分類新增成功";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "新增子分類時發生錯誤，請稍後再試";
            }

            return RedirectToAction("Index");
        }

        // POST: /Categories/EditSubCategory (編輯子分類)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubCategory(int SubCategoryId, int CategoryId, string SubCategoryName, string? Description)
        {
            if (string.IsNullOrEmpty(SubCategoryName) || CategoryId <= 0 || SubCategoryId <= 0)
            {
                TempData["ErrorMessage"] = "請確保所有欄位都已正確填寫";
                return RedirectToAction("Index");
            }

            try
            {
                var subCategory = await _context.SubCategories.FindAsync(SubCategoryId);
                if (subCategory == null)
                {
                    TempData["ErrorMessage"] = "找不到要編輯的子分類";
                    return RedirectToAction("Index");
                }

                // 檢查父分類是否存在
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == CategoryId);
                if (!categoryExists)
                {
                    TempData["ErrorMessage"] = "所選的父分類不存在";
                    return RedirectToAction("Index");
                }

                // 檢查同一父分類下是否已存在相同名稱的子分類（排除當前編輯的子分類)
                var duplicateExists = await _context.SubCategories
                    .AnyAsync(sc => sc.CategoryId == CategoryId && sc.Name == SubCategoryName && sc.Id != SubCategoryId);

                if (duplicateExists)
                {
                    TempData["ErrorMessage"] = "該分類下已存在相同名稱的子分類";
                    return RedirectToAction("Index");
                }

                if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 255)
                {
                    Description = Description.Substring(0, 255);
                }

                subCategory.CategoryId = CategoryId;
                subCategory.Name = SubCategoryName;
                subCategory.Description = Description;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "子分類編輯成功";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "編輯子分類時發生錯誤，請稍後再試";
            }

            return RedirectToAction("Index");
        }

        // POST: /Categories/DeleteSubCategory (刪除子分類)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubCategory(int SubCategoryId)
        {
            if (SubCategoryId <= 0)
            {
                TempData["ErrorMessage"] = "無效的子分類ID";
                return RedirectToAction("Index");
            }

            try
            {
                var subCategory = await _context.SubCategories.FindAsync(SubCategoryId);
                if (subCategory == null)
                {
                    TempData["ErrorMessage"] = "找不到要刪除的子分類";
                    return RedirectToAction("Index");
                }

                // 檢查是否有商品使用此子分類
                var hasProducts = await _context.Products.AnyAsync(p => p.SubCategoryId == SubCategoryId);
                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "此子分類下還有商品，無法刪除";
                    return RedirectToAction("Index");
                }

                _context.SubCategories.Remove(subCategory);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "子分類刪除成功";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "刪除子分類時發生錯誤，請稍後再試";
            }

            return RedirectToAction("Index");
        }
    }
}