using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels.Logistics;

namespace Team.Backend.Repositories
{
    /// <summary>
    /// 物流倉儲介面
    /// </summary>
    public interface ILogisticsRepository
    {
        // 物流商管理
        Task<List<Carrier>> GetAllCarriersAsync();
        Task<Carrier?> GetCarrierByIdAsync(int id);
        Task<bool> CreateCarrierAsync(Carrier carrier);
        Task<bool> UpdateCarrierAsync(Carrier carrier);
        Task<bool> DeleteCarrierAsync(int id);
        Task<bool> CarrierExistsByNameAsync(string name, int? excludeId = null);

        // 統計查詢
        Task<int> GetOrderCountByCarrierAsync(int carrierId);
        Task<List<LogisticsStatDto>> GetCarrierUsageStatsAsync();
        Task<List<RegionStatDto>> GetRegionStatsAsync();
        Task<decimal> GetAverageShippingFeeAsync();

        // 設定管理
        Task<bool> CanConnectAsync();
    }

    /// <summary>
    /// 物流倉儲實作
    /// </summary>
    public class LogisticsRepository : ILogisticsRepository
    {
        private readonly AppDbContext _context;

        public LogisticsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Carrier>> GetAllCarriersAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GetAllCarriersAsync] 開始查詢所有物流商");
                
                var carriers = await _context.Carriers
                    .OrderBy(c => c.Id)
                    .ToListAsync();
                
                System.Diagnostics.Debug.WriteLine($"[GetAllCarriersAsync] 查詢完成，找到 {carriers.Count} 筆資料");
                
                foreach (var carrier in carriers)
                {
                    System.Diagnostics.Debug.WriteLine($"[GetAllCarriersAsync] 物流商: ID={carrier.Id}, Name={carrier.Name}, IsActive={carrier.IsActive}");
                }
                
                return carriers;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetAllCarriersAsync] 查詢失敗: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GetAllCarriersAsync] 錯誤詳情: {ex}");
                throw;
            }
        }

        public async Task<Carrier?> GetCarrierByIdAsync(int id)
        {
            return await _context.Carriers
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<bool> CreateCarrierAsync(Carrier carrier)
        {
            try
            {
                _context.Carriers.Add(carrier);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCarrierAsync(Carrier carrier)
        {
            try
            {
                _context.Carriers.Update(carrier);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteCarrierAsync(int id)
        {
            try
            {
                var carrier = await _context.Carriers.FindAsync(id);
                if (carrier != null)
                {
                    _context.Carriers.Remove(carrier);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CarrierExistsByNameAsync(string name, int? excludeId = null)
        {
            var query = _context.Carriers.Where(c => c.Name == name);
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<int> GetOrderCountByCarrierAsync(int carrierId)
        {
            // 直接根據 Shipments 表的 CarrierId 統計
            return await _context.Shipments
                .Where(s => s.CarrierId == carrierId)
                .CountAsync();
        }

        public async Task<List<LogisticsStatDto>> GetCarrierUsageStatsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GetCarrierUsageStatsAsync] 開始統計物流商使用資料");
                
                var carriers = await _context.Carriers.ToListAsync();
                var stats = new List<LogisticsStatDto>();

                foreach (var carrier in carriers)
                {
                    // 根據 Shipments 表統計訂單數量
                    var orderCount = await _context.Shipments
                        .Where(s => s.CarrierId == carrier.Id)
                        .CountAsync();

                    // 計算總運費收入 (從相關的 Orders 計算)
                    var totalRevenue = await _context.Shipments
                        .Where(s => s.CarrierId == carrier.Id)
                        .Join(_context.Orders, 
                              s => s.OrderId, 
                              o => o.Id, 
                              (s, o) => o.ShippingFee)
                        .SumAsync();

                    System.Diagnostics.Debug.WriteLine($"[GetCarrierUsageStatsAsync] 物流商 {carrier.Name}: 訂單數={orderCount}, 收入={totalRevenue}");

                    stats.Add(new LogisticsStatDto
                    {
                        CarrierId = carrier.Id,
                        CarrierName = carrier.Name,
                        OrderCount = orderCount,
                        TotalRevenue = totalRevenue,
                        UsagePercentage = 0 // 會在 Service 層計算
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[GetCarrierUsageStatsAsync] 統計完成，共 {stats.Count} 筆物流商統計");
                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetCarrierUsageStatsAsync] 統計失敗: {ex.Message}");
                return new List<LogisticsStatDto>();
            }
        }

        public async Task<List<RegionStatDto>> GetRegionStatsAsync()
        {
            try
            {
                // 根據 Shipments 與 Orders 的關聯來統計地區資料
                return await _context.Shipments
                    .Join(_context.Orders, 
                          s => s.OrderId, 
                          o => o.Id, 
                          (s, o) => new { Shipment = s, Order = o })
                    .Join(_context.Carriers,
                          so => so.Shipment.CarrierId,
                          c => c.Id,
                          (so, c) => new { so.Shipment, so.Order, Carrier = c })
                    .GroupBy(x => x.Order.City)
                    .Select(g => new RegionStatDto
                    {
                        RegionName = g.Key ?? "未知地區",
                        OrderCount = g.Count(),
                        AvgShippingFee = g.Average(x => x.Order.ShippingFee),
                        TopCarrier = g.GroupBy(x => x.Carrier.Name)
                                      .OrderByDescending(cg => cg.Count())
                                      .Select(cg => cg.Key)
                                      .FirstOrDefault() ?? "未知"
                    })
                    .OrderByDescending(r => r.OrderCount)
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetRegionStatsAsync] 統計失敗: {ex.Message}");
                return new List<RegionStatDto>();
            }
        }

        public async Task<decimal> GetAverageShippingFeeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GetAverageShippingFeeAsync] 開始計算平均運費");
                
                // 改用分步查詢來避免 LINQ 翻譯問題
                var shippingFees = await _context.Shipments
                    .Join(_context.Orders, 
                          s => s.OrderId, 
                          o => o.Id, 
                          (s, o) => o.ShippingFee)
                    .Where(fee => fee > 0)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"[GetAverageShippingFeeAsync] 找到 {shippingFees.Count} 筆有效運費資料");
                
                if (shippingFees.Any())
                {
                    var average = shippingFees.Average();
                    System.Diagnostics.Debug.WriteLine($"[GetAverageShippingFeeAsync] 平均運費: {average}");
                    return average;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[GetAverageShippingFeeAsync] 沒有有效的運費資料");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetAverageShippingFeeAsync] 計算失敗: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> CanConnectAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 根據物流商名稱取得綠界代碼
        /// </summary>
        private string GetECPayCodeByCarrierName(string carrierName)
        {
            return carrierName switch
            {
                var name when name.Contains("黑貓") => "HOME_TCAT",
                var name when name.Contains("7-11") || name.Contains("7-ELEVEN") => "UNIMART",
                var name when name.Contains("全家") => "FAMI",
                _ => "UNKNOWN"
            };
        }
    }
}