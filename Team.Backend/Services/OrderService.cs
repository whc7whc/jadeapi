using System.Text;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;            // EF 實體（BuildItemName 會用到 OrderDetail）
using Team.Backend.Models.ViewModels.Orders;  // ViewModels
using Team.Backend.Repositories;              // IOrderRepository
using Team.Backend.Constants;

namespace Team.Backend.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orders;
        public OrderService(IOrderRepository orders) => _orders = orders;

        public async Task<OrderIndexVm> SearchAsync(OrderQueryVm q)
        {
            // 連線檢查
            var can = await _orders.CanConnectAsync();
            if (!can)
            {
                return new OrderIndexVm
                {
                    Query = q,
                    Items = Enumerable.Empty<OrderListItemVm>(),
                    TotalCount = 0,
                    CanConnect = false
                };
            }

            var src = ApplyFilters(_orders.Query(), q);

            // 簡化：直接取得總數
            var totalCount = await src.CountAsync();

            // 應用排序
            var sortBy = q.SortBy?.ToLower();
            var isDesc = q.SortDirection?.ToLower() == "desc";

            var orderedQuery = sortBy switch
            {
                "id" => isDesc ? src.OrderByDescending(o => o.Id) : src.OrderBy(o => o.Id),
                "amount" => isDesc ? src.OrderByDescending(o => o.TotalAmount) : src.OrderBy(o => o.TotalAmount),
                "createdat" => isDesc ? src.OrderByDescending(o => o.CreatedAt) : src.OrderBy(o => o.CreatedAt),
                _ => src.OrderByDescending(o => o.CreatedAt) // 預設按建立時間倒序
            };

            // 分頁並取得訂單
            var orders = await orderedQuery
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Include(o => o.Member)
                    .ThenInclude(m => m.Profile)
                .Include(o => o.Sellers)
                .Include(o => o.OrderDetails)
                .Include(o => o.Shipments)
                .ToListAsync();

            // 轉換為 ViewModel
            var items = orders.Select(o => new OrderListItemVm
            {
                Id = o.Id,
                Code = $"#{o.Id}",
                MemberName = (o.Member?.Profile?.Name ?? o.RecipientName ?? "unknown"),
                Total = o.TotalAmount,
                PaymentStatus = o.PaymentStatus ?? "pending",
                OrderStatus = o.OrderStatus ?? "pending",
                CreatedAt = o.CreatedAt,
                ShippedAt = o.Shipments?.Where(s => s.ShippedAt.HasValue)
                                      .OrderBy(s => s.ShippedAt)
                                      .Select(s => s.ShippedAt)
                                      .FirstOrDefault(),
                VendorSummary = new List<VendorOrderSummary>
                {
                    new VendorOrderSummary
                    {
                        SellerId = o.SellersId ?? 0,
                        VendorName = o.Sellers?.RealName ?? "未知賣家",
                        Amount = o.TotalAmount,
                        OrderStatus = o.OrderStatus ?? "pending",
                        ItemCount = o.OrderDetails?.Count ?? 0
                    }
                }
            }).ToList();

            return new OrderIndexVm
            {
                Query = q,
                Items = items,
                TotalCount = totalCount,
                CanConnect = true
            };
        }

        public async Task<OrderDetailVm?> GetDetailAsync(int id)
        {
            // 先取得主訂單
            var mainOrder = await _orders.GetByIdWithRelationsAsync(id);
            if (mainOrder == null) return null;

            // 查找同一次購買的所有訂單（按時間和會員分組）
            var relatedOrders = await _orders.Query()
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Sellers)
                .Include(o => o.Shipments)
                .Where(o => o.MemberId == mainOrder.MemberId && 
                           o.CreatedAt.Date == mainOrder.CreatedAt.Date &&
                           o.CreatedAt.Hour == mainOrder.CreatedAt.Hour &&
                           o.CreatedAt.Minute == mainOrder.CreatedAt.Minute)
                .OrderBy(o => o.Id)
                .ToListAsync();

            // 按賣家分組
            var vendorGroups = relatedOrders.Select(o => new VendorOrderGroup
            {
                SellerId = o.SellersId ?? 0,
                VendorName = o.Sellers != null ? o.Sellers.RealName ?? "未知賣家" : "未知賣家",
                OrderCode = $"#{o.Id}",
                OrderStatus = o.OrderStatus ?? "pending",
                SubTotal = o.OrderDetails.Sum(d => d.Subtotal ?? 0),
                ShippingFee = o.ShippingFee,
                Total = o.TotalAmount,
                ShippedAt = o.Shipments.FirstOrDefault()?.ShippedAt,
                TrackingNumber = o.Shipments.FirstOrDefault()?.TrackingNumber ?? "",
                Items = o.OrderDetails.Select(d => new OrderDetailItemVm
                {
                    Name = BuildItemName(d),
                    Qty = d.Quantity ?? 0,
                    Price = d.UnitPrice ?? 0,
                    Subtotal = d.Subtotal ?? 0,
                    SellerName = o.Sellers != null ? o.Sellers.RealName ?? "未知賣家" : "未知賣家"
                }).ToList()
            }).ToList();

            return new OrderDetailVm
            {
                Id = mainOrder.Id,
                Code = relatedOrders.Count > 1 ? $"#{mainOrder.Id} (含{relatedOrders.Count}個賣家訂單)" : $"#{mainOrder.Id}",
                RecipientName = mainOrder.RecipientName,
                Phone = mainOrder.PhoneNumber,
                Address = $"{mainOrder.City}{mainOrder.District}{mainOrder.AddressDetail}",
                ShippingFee = relatedOrders.Sum(o => o.ShippingFee),
                Total = relatedOrders.Sum(o => o.TotalAmount),
                CreatedAt = mainOrder.CreatedAt,
                PaymentStatus = mainOrder.PaymentStatus ?? "pending",
                OverallStatus = mainOrder.OrderStatus ?? "pending",
                VendorOrderGroups = vendorGroups
                // 移除過時的 Items 屬性賦值，改用 VendorOrderGroups
            };
        }

        // 匯出 CSV：沿用同一套篩選，不分頁
        public async Task<(byte[] Content, string FileName, string ContentType)> ExportCsvAsync(OrderQueryVm q)
        {
            // 連線檢查：無法連線也回傳只有表頭的空 CSV
            var can = await _orders.CanConnectAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Code,MemberName,Total,PaymentStatus,OrderStatus,CreatedAt,ShippedAt");
            if (!can) return MakeCsv(sb.ToString());

            var src = ApplyFilters(_orders.Query(), q);

            var rows = await src.OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    Code = "#" + o.Id,
                    MemberName = (o.Member != null && o.Member.Profile != null) ? o.Member.Profile.Name : o.RecipientName,
                    o.TotalAmount,
                    o.PaymentStatus,
                    o.OrderStatus,
                    o.CreatedAt,
                    ShippedAt = o.Shipments.OrderBy(s => s.ShippedAt).Select(s => s.ShippedAt).FirstOrDefault()
                })
                .ToListAsync();

            foreach (var r in rows)
            {
                static string Esc(string? s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";
                var created = r.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                var shipped = r.ShippedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                sb.AppendLine(string.Join(",",
                    Esc(r.Code),
                    Esc(r.MemberName),
                    r.TotalAmount.ToString("0.########"),
                    Esc(r.PaymentStatus),
                    Esc(r.OrderStatus),
                    Esc(created),
                    Esc(shipped)
                ));
            }

            return MakeCsv(sb.ToString());
        }

        // ---- 私有共用 ----

        // 把所有查詢條件集中一處，Search / Export 共用
        private static IQueryable<Order> ApplyFilters(IQueryable<Order> src, OrderQueryVm q)
        {
            if (!string.IsNullOrWhiteSpace(q.Q))
            {
                var key = q.Q.Trim();
                src = src.Where(o =>
                    o.Id.ToString().Contains(key) ||
                    o.RecipientName.Contains(key) ||
                    o.PhoneNumber.Contains(key) ||
                    (o.Member != null && o.Member.Profile != null && o.Member.Profile.Name.Contains(key)));
            }

            // 付款狀態
            if (!string.IsNullOrWhiteSpace(q.PaymentStatus))
                src = src.Where(o => o.PaymentStatus == q.PaymentStatus);

            // 訂單狀態
            if (!string.IsNullOrWhiteSpace(q.OrderStatus))
                src = src.Where(o => o.OrderStatus == q.OrderStatus);

            // 舊欄位後援（兩個新欄位都沒填才用）
            if (string.IsNullOrWhiteSpace(q.PaymentStatus) &&
                string.IsNullOrWhiteSpace(q.OrderStatus) &&
                !string.IsNullOrWhiteSpace(q.Status))
            {
                src = src.Where(o => o.OrderStatus == q.Status || o.PaymentStatus == q.Status);
            }

            // 建立日期（含迄日）
            if (q.DateFrom.HasValue) src = src.Where(o => o.CreatedAt >= q.DateFrom.Value.Date);
            if (q.DateTo.HasValue) src = src.Where(o => o.CreatedAt < q.DateTo.Value.Date.AddDays(1));

            return src;
        }

        private static (byte[] Content, string FileName, string ContentType) MakeCsv(string csv)
        {
            var bom = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(csv);
            var bytes = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);
            var fileName = $"orders_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return (bytes, fileName, "text/csv; charset=utf-8");
        }

        private static string BuildItemName(OrderDetail d)
        {
            var baseName = d.Product?.Name ?? "";
            var variant = d.AttributeValue?.AttributeValue?.Value; // 規格值（可為 null）
            return string.IsNullOrWhiteSpace(variant) ? baseName : $"{baseName}/{variant}";
        }

    }
}
