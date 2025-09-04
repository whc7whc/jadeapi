// Controllers/AdminOrdersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.ViewModels.Orders;
using Team.Backend.Services;
using Team.Backend.Models.EfModel;
using Microsoft.Extensions.Logging;



namespace Team.Backend.Controllers
{
    public class AdminOrdersController : BaseController
    {
        private readonly IOrderService _svc;
        private readonly ILogger<AdminOrdersController> _logger;

        public AdminOrdersController(IOrderService svc, AppDbContext context, ILogger<AdminOrdersController> logger)
            : base(context, logger)
        {
            _svc = svc;
            _logger = logger;
        }

        // /AdminOrders
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] OrderQueryVm query)
        {
            var vm = await _svc.SearchAsync(query ?? new());
            return View(vm); // View 要用 OrderIndexVm（非 List<Order>）
        }

        // /AdminOrders/DetailPartial/123（清單上的「詳細」Modal 用）
        [HttpGet]
        public async Task<IActionResult> DetailPartial(int id)
        {
            var vm = await _svc.GetDetailAsync(id);
            if (vm == null) return NotFound();
            return PartialView("_OrderDetailModalBody", vm);
        }

        // 新增：獲取賣家子訂單列表
        [HttpGet]
        public async Task<IActionResult> VendorOrders(int masterId)
        {
            var mainOrder = await _context.Orders
                .Include(o => o.Member)
                .FirstOrDefaultAsync(o => o.Id == masterId);
            
            if (mainOrder == null) return NotFound();

            var relatedOrders = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Sellers)
                .Include(o => o.Shipments)
                .Where(o => o.MemberId == mainOrder.MemberId && 
                           o.CreatedAt.Date == mainOrder.CreatedAt.Date &&
                           o.CreatedAt.Hour == mainOrder.CreatedAt.Hour &&
                           o.CreatedAt.Minute == mainOrder.CreatedAt.Minute)
                .OrderBy(o => o.Id)
                .Select(o => new
                {
                    OrderId = o.Id,
                    SellerId = o.SellersId ?? 0,
                    SellerName = o.Sellers != null ? o.Sellers.RealName ?? "未知賣家" : "未知賣家",
                    Status = o.OrderStatus,
                    Amount = o.TotalAmount,
                    ItemCount = o.OrderDetails.Count,
                    ShippedAt = o.Shipments.FirstOrDefault().ShippedAt,
                    TrackingNumber = o.Shipments.FirstOrDefault().TrackingNumber
                })
                .ToListAsync();

            return Json(new { success = true, data = relatedOrders });
        }

        // 回傳局部 HTML 的 Action
        [HttpGet]
        public async Task<IActionResult> ListPartial([FromQuery] OrderQueryVm query)
        {
            var vm = await _svc.SearchAsync(query ?? new());
            return PartialView("_OrderList", vm); // 只回表格+分頁
        }



        // /AdminOrders/ExportCsv?...
        [HttpGet]
        public async Task<IActionResult> ExportCsv([FromQuery] OrderQueryVm query)
        {
            var (bytes, fileName, contentType) = await _svc.ExportCsvAsync(query ?? new());
            return File(bytes, contentType, fileName);
        }
    }
}