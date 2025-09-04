using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.ViewModels.Logistics;
using Team.Backend.Services;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Controllers
{
    /// <summary>
    /// 物流管理控制器
    /// </summary>
    public class AdminLogisticsController : BaseController
    {
        private readonly ILogisticsService _logisticsService;
        private readonly ILogger<AdminLogisticsController> _logger;

        public AdminLogisticsController(
            ILogisticsService logisticsService,
            ILogger<AdminLogisticsController> logger,
            AppDbContext context)
            : base(context, logger)
        {
            _logisticsService = logisticsService;
            _logger = logger;
        }

        /// <summary>
        /// 物流管理首頁
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] LogisticsQueryVm query)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Index] 開始載入, Query: {query?.CarrierId}");
                
                var vm = await _logisticsService.SearchCarriersAsync(query ?? new());
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Index] Service 回傳: Items={vm.Items?.Count()}, CanConnect={vm.CanConnect}");
                
                // 取得所有物流商供下拉選單使用
                var allCarriers = await _logisticsService.GetAllCarrierOptionsAsync();
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Index] 物流商選項: {allCarriers?.Count ?? 0} 個");
                ViewBag.CarrierOptions = allCarriers;
                
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入物流管理頁面發生錯誤");
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Index] 發生錯誤: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Index] 錯誤詳情: {ex}");
                
                TempData["ErrorMessage"] = "載入頁面時發生錯誤，請稍後再試";
                return View(new LogisticsIndexVm());
            }
        }

        /// <summary>
        /// 物流商列表部分視圖 (AJAX 用)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ListPartial([FromQuery] LogisticsQueryVm query)
        {
            try
            {
                var vm = await _logisticsService.SearchCarriersAsync(query ?? new());
                return PartialView("_CarrierList", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入物流商列表發生錯誤");
                return PartialView("_CarrierList", new LogisticsIndexVm());
            }
        }

        /// <summary>
        /// 物流商詳細資訊 Modal (AJAX 用)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetailPartial(int id)
        {
            try
            {
                var vm = await _logisticsService.GetCarrierDetailAsync(id);
                if (vm == null)
                {
                    return NotFound();
                }
                return PartialView("_CarrierDetailModal", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入物流商詳細資訊發生錯誤");
                return NotFound();
            }
        }

        /// <summary>
        /// 新增物流商頁面
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CarrierCreateVm());
        }

        /// <summary>
        /// 處理新增物流商
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CarrierCreateVm model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var (success, message) = await _logisticsService.CreateCarrierAsync(model);
                
                if (success)
                {
                    TempData["SuccessMessage"] = message;
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, message);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新增物流商發生錯誤");
                ModelState.AddModelError(string.Empty, "新增失敗，請稍後再試");
                return View(model);
            }
        }

        /// <summary>
        /// 編輯物流商頁面
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var detail = await _logisticsService.GetCarrierDetailAsync(id);
                if (detail == null)
                {
                    TempData["ErrorMessage"] = "找不到指定的物流商";
                    return RedirectToAction(nameof(Index));
                }

                var model = new CarrierEditVm
                {
                    Name = detail.Name,
                    Contact = detail.Contact
                };

                ViewBag.CarrierId = id;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入編輯頁面發生錯誤");
                TempData["ErrorMessage"] = "載入編輯頁面時發生錯誤";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// 處理編輯物流商
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CarrierEditVm model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.CarrierId = id;
                    return View(model);
                }

                var (success, message) = await _logisticsService.UpdateCarrierAsync(id, model);
                
                if (success)
                {
                    TempData["SuccessMessage"] = message;
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, message);
                    ViewBag.CarrierId = id;
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "編輯物流商發生錯誤");
                ModelState.AddModelError(string.Empty, "編輯失敗，請稍後再試");
                ViewBag.CarrierId = id;
                return View(model);
            }
        }

        /// <summary>
        /// 刪除物流商 (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var (success, message) = await _logisticsService.DeleteCarrierAsync(id);
                
                return Json(new 
                { 
                    success = success, 
                    message = message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除物流商發生錯誤");
                return Json(new 
                { 
                    success = false, 
                    message = "刪除失敗，請稍後再試" 
                });
            }
        }

        /// <summary>
        /// 運費設定頁面
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            try
            {
                var vm = await _logisticsService.GetShippingSettingsAsync();
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入運費設定頁面發生錯誤");
                TempData["ErrorMessage"] = "載入設定頁面時發生錯誤";
                return View(new ShippingSettingsVm());
            }
        }

        /// <summary>
        /// 更新運費設定
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(ShippingSettingsVm model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var (success, message) = await _logisticsService.UpdateShippingSettingsAsync(model);
                
                if (success)
                {
                    TempData["SuccessMessage"] = message;
                }
                else
                {
                    TempData["ErrorMessage"] = message;
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新運費設定發生錯誤");
                TempData["ErrorMessage"] = "更新失敗，請稍後再試";
                return View(model);
            }
        }

        /// <summary>
        /// 物流統計頁面
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[AdminLogisticsController.Statistics] 開始載入統計頁面");
                
                var vm = await _logisticsService.GetStatisticsAsync();
                
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Statistics] Service 回傳: TotalOrders={vm.TotalOrders}, CarrierStats={vm.CarrierStats?.Count ?? 0}");
                
                // 輸出每個 CarrierStat 的詳細資訊
                if (vm.CarrierStats != null)
                {
                    foreach (var stat in vm.CarrierStats)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Statistics] CarrierStat: {stat.CarrierName}, Orders: {stat.OrderCount}, Revenue: {stat.TotalRevenue}");
                    }
                }
                
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入統計頁面發生錯誤");
                System.Diagnostics.Debug.WriteLine($"[AdminLogisticsController.Statistics] 發生錯誤: {ex.Message}");
                TempData["ErrorMessage"] = "載入統計頁面時發生錯誤";
                
                // 即使發生錯誤，也回傳一個空的 ViewModel 而不是讓頁面崩潰
                return View(new LogisticsStatisticsVm());
            }
        }

        /// <summary>
        /// 匯出物流商清單
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportCarriers()
        {
            try
            {
                var (fileBytes, fileName, contentType) = await _logisticsService.ExportCarriersAsync();
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "匯出物流商清單發生錯誤");
                TempData["ErrorMessage"] = "匯出失敗，請稍後再試";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// 切換物流商啟用/停用狀態
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var result = await _logisticsService.ToggleCarrierStatusAsync(id);
                
                return Json(new
                {
                    success = result.Success,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切換物流商狀態發生錯誤，ID: {Id}", id);
                return Json(new
                {
                    success = false,
                    message = "切換狀態失敗，請稍後再試"
                });
            }
        }

        #region 訂單統一發送管理

        /// <summary>
        /// 待發送訂單列表頁面
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PendingShipments()
        {
            try
            {
                // 查詢所有賣家商品都已準備好，但訂單狀態還不是 shipped 的訂單
                var pendingOrders = await _context.Orders
                    .Where(o => o.OrderStatus == "paid" || o.OrderStatus == "pending")
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                    .Include(o => o.Member)
                        .ThenInclude(m => m.Profile)
                    .ToListAsync();

                // 可以進一步篩選：檢查是否所有商品的賣家都已經標記為準備出貨
                // 這裡先簡化，顯示所有已付款的訂單

                return View(pendingOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入待發送訂單列表發生錯誤");
                TempData["ErrorMessage"] = "載入列表時發生錯誤";
                return View(new List<Order>());
            }
        }

        /// <summary>
        /// 統一發送選定的訂單
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessShipments([FromForm] int[] orderIds)
        {
            try
            {
                if (orderIds == null || orderIds.Length == 0)
                {
                    return Json(new { success = false, message = "請選擇要發送的訂單" });
                }

                var successCount = 0;
                var failCount = 0;

                foreach (var orderId in orderIds)
                {
                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null && (order.OrderStatus == "paid" || order.OrderStatus == "pending"))
                    {
                        // 更新訂單狀態為已出貨
                        order.OrderStatus = "shipped";
                        order.UpdatedAt = DateTime.Now;

                        // 創建出貨記錄
                        var shipment = new Shipment
                        {
                            OrderId = orderId,
                            TrackingNumber = $"TW{DateTime.Now:yyyyMMdd}{orderId:D6}",
                            ShippedAt = DateTime.Now,
                            Status = "shipped",
                            CarrierId = 1, // 預設物流商，實際應該可選擇
                            UpdatedAt = DateTime.Now
                        };

                        _context.Shipments.Add(shipment);
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new 
                { 
                    success = true, 
                    message = $"成功發送 {successCount} 個訂單" + (failCount > 0 ? $"，失敗 {failCount} 個" : "") 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "統一發送訂單發生錯誤");
                return Json(new { success = false, message = "發送失敗，請稍後再試" });
            }
        }

        /// <summary>
        /// 取得訂單的賣家出貨狀態詳情
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrderVendorStatus(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.Sellers)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "找不到訂單" });
                }

                // 按賣家分組統計出貨狀態
                var vendorStatus = order.OrderDetails
                    .GroupBy(od => new { od.Product.SellersId, od.Product.Sellers.RealName })
                    .Select(g => new
                    {
                        VendorId = g.Key.SellersId,
                        VendorName = g.Key.RealName,
                        ItemCount = g.Count(),
                        // 這裡需要根據實際的商品級出貨狀態來判斷
                        // 目前先假設都是準備中
                        Status = "ready", // ready, pending, shipped
                        Items = g.Select(od => new
                        {
                            ProductName = od.Product.Name,
                            Quantity = od.Quantity
                        }).ToList()
                    }).ToList();

                return Json(new { success = true, data = vendorStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得訂單賣家狀態發生錯誤，OrderId: {OrderId}", orderId);
                return Json(new { success = false, message = "取得狀態失敗" });
            }
        }

        #endregion
    }
}