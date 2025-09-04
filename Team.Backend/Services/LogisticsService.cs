using Team.Backend.Models.ViewModels.Logistics;
using Team.Backend.Models.EfModel;
using Team.Backend.Repositories;

namespace Team.Backend.Services
{
    /// <summary>
    /// 物流服務介面
    /// </summary>
    public interface ILogisticsService
    {
        // 物流商管理
        Task<LogisticsIndexVm> SearchCarriersAsync(LogisticsQueryVm query);
        Task<CarrierDetailVm?> GetCarrierDetailAsync(int id);
        Task<(bool Success, string Message)> CreateCarrierAsync(CarrierCreateVm model);
        Task<(bool Success, string Message)> UpdateCarrierAsync(int id, CarrierEditVm model);
        Task<(bool Success, string Message)> DeleteCarrierAsync(int id);
        
        // 取得物流商選項
        Task<List<CarrierOptionVm>> GetAllCarrierOptionsAsync();
        
        // 切換物流商狀態
        Task<(bool Success, string Message)> ToggleCarrierStatusAsync(int id);

        // 統計報表
        Task<LogisticsStatisticsVm> GetStatisticsAsync();
        Task<(byte[] FileBytes, string FileName, string ContentType)> ExportCarriersAsync();

        // 設定管理
        Task<ShippingSettingsVm> GetShippingSettingsAsync();
        Task<(bool Success, string Message)> UpdateShippingSettingsAsync(ShippingSettingsVm model);
    }

    /// <summary>
    /// 物流服務實作
    /// </summary>
    public class LogisticsService : ILogisticsService
    {
        private readonly ILogisticsRepository _repository;

        public LogisticsService(ILogisticsRepository repository)
        {
            _repository = repository;
        }

        public async Task<LogisticsIndexVm> SearchCarriersAsync(LogisticsQueryVm query)
        {
            try
            {
                var canConnect = await _repository.CanConnectAsync();
                if (!canConnect)
                {
                    return new LogisticsIndexVm
                    {
                        Query = query,
                        Items = Enumerable.Empty<CarrierListItemVm>(),
                        TotalCount = 0,
                        CanConnect = false
                    };
                }

                var carriers = await _repository.GetAllCarriersAsync();
                var filtered = ApplyFilters(carriers, query);

                var items = new List<CarrierListItemVm>();
                foreach (var carrier in filtered)
                {
                    var orderCount = await _repository.GetOrderCountByCarrierAsync(carrier.Id);
                    items.Add(new CarrierListItemVm
                    {
                        Id = carrier.Id,
                        Name = carrier.Name ?? "未命名物流商",
                        Contact = carrier.Contact ?? "無聯絡資訊",
                        ECPayCode = GetECPayCodeByCarrierName(carrier.Name ?? ""),
                        IsActive = carrier.IsActive,
                        OrderCount = orderCount,
                        CreatedAt = carrier.CreatedAt ?? DateTime.Now
                    });
                }

                var pagedItems = items
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                return new LogisticsIndexVm
                {
                    Query = query,
                    Items = pagedItems,
                    TotalCount = items.Count,
                    CanConnect = true
                };
            }
            catch (Exception)
            {
                return new LogisticsIndexVm
                {
                    Query = query,
                    Items = Enumerable.Empty<CarrierListItemVm>(),
                    TotalCount = 0,
                    CanConnect = false
                };
            }
        }

        public async Task<CarrierDetailVm?> GetCarrierDetailAsync(int id)
        {
            var carrier = await _repository.GetCarrierByIdAsync(id);
            if (carrier == null) return null;

            var orderCount = await _repository.GetOrderCountByCarrierAsync(id);

            return new CarrierDetailVm
            {
                Id = carrier.Id,
                Name = carrier.Name,
                Contact = carrier.Contact,
                ECPayCode = GetECPayCodeByCarrierName(carrier.Name),
                IsActive = carrier.IsActive,
                OrderCount = orderCount,
                CreatedAt = carrier.CreatedAt ?? DateTime.Now
            };
        }

        public async Task<(bool Success, string Message)> CreateCarrierAsync(CarrierCreateVm model)
        {
            if (await _repository.CarrierExistsByNameAsync(model.Name))
            {
                return (false, "物流商名稱已存在");
            }

            var carrier = new Carrier
            {
                Name = model.Name,
                Contact = model.Contact,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var success = await _repository.CreateCarrierAsync(carrier);
            return success 
                ? (true, "物流商新增成功") 
                : (false, "新增失敗，請稍後再試");
        }

        public async Task<(bool Success, string Message)> UpdateCarrierAsync(int id, CarrierEditVm model)
        {
            var carrier = await _repository.GetCarrierByIdAsync(id);
            if (carrier == null)
            {
                return (false, "找不到指定的物流商");
            }

            if (await _repository.CarrierExistsByNameAsync(model.Name, id))
            {
                return (false, "物流商名稱已存在");
            }

            carrier.Name = model.Name;
            carrier.Contact = model.Contact;

            var success = await _repository.UpdateCarrierAsync(carrier);
            return success 
                ? (true, "物流商更新成功") 
                : (false, "更新失敗，請稍後再試");
        }

        public async Task<(bool Success, string Message)> DeleteCarrierAsync(int id)
        {
            var orderCount = await _repository.GetOrderCountByCarrierAsync(id);
            if (orderCount > 0)
            {
                return (false, $"無法刪除，此物流商還有 {orderCount} 筆訂單記錄");
            }

            var success = await _repository.DeleteCarrierAsync(id);
            return success 
                ? (true, "物流商刪除成功") 
                : (false, "刪除失敗，請稍後再試");
        }

        public async Task<List<CarrierOptionVm>> GetAllCarrierOptionsAsync()
        {
            var carriers = await _repository.GetAllCarriersAsync();
            
            return carriers.Select(c => new CarrierOptionVm
            {
                Id = c.Id,
                Name = c.Name,
                ECPayCode = GetECPayCodeByCarrierName(c.Name)
            }).ToList();
        }

        public async Task<(bool Success, string Message)> ToggleCarrierStatusAsync(int id)
        {
            try
            {
                var carrier = await _repository.GetCarrierByIdAsync(id);
                if (carrier == null)
                {
                    return (false, "找不到指定的物流商");
                }

                carrier.IsActive = !carrier.IsActive;
                var success = await _repository.UpdateCarrierAsync(carrier);
                
                if (success)
                {
                    var status = carrier.IsActive ? "啟用" : "停用";
                    return (true, $"物流商「{carrier.Name}」已{status}");
                }
                else
                {
                    return (false, "更新狀態失敗");
                }
            }
            catch (Exception ex)
            {
                return (false, $"切換狀態時發生錯誤：{ex.Message}");
            }
        }

        public async Task<LogisticsStatisticsVm> GetStatisticsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GetStatisticsAsync] 開始獲取統計資料");
                
                var carrierStats = await _repository.GetCarrierUsageStatsAsync();
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] Repository 回傳 CarrierStats: {carrierStats?.Count ?? 0} 筆");
                
                var regionStats = await _repository.GetRegionStatsAsync();
                var avgShippingFee = await _repository.GetAverageShippingFeeAsync();

                // 不使用模擬資料，直接使用資料庫的真實結果
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] 使用真實資料庫資料: CarrierStats={carrierStats.Count}, RegionStats={regionStats.Count}");

                // 計算使用百分比
                var totalOrders = carrierStats.Sum(s => s.OrderCount);
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] 計算總訂單數: {totalOrders}");
                
                foreach (var stat in carrierStats)
                {
                    stat.UsagePercentage = totalOrders > 0 
                        ? Math.Round((decimal)stat.OrderCount / totalOrders * 100, 1) 
                        : 0;
                }

                var result = new LogisticsStatisticsVm
                {
                    CarrierStats = carrierStats,
                    RegionStats = regionStats,
                    TotalOrders = totalOrders,
                    AverageShippingFee = avgShippingFee,
                    TotalRevenue = carrierStats.Sum(s => s.TotalRevenue),
                    LastUpdateTime = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] 完成，總訂單: {result.TotalOrders}，物流商數量: {result.CarrierStats.Count}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] 錯誤: {ex.Message}");
                
                // 即使發生錯誤，也返回空的統計資料而不是模擬資料
                return new LogisticsStatisticsVm
                {
                    CarrierStats = new List<LogisticsStatDto>(),
                    RegionStats = new List<RegionStatDto>(),
                    TotalOrders = 0,
                    AverageShippingFee = 0,
                    TotalRevenue = 0,
                    LastUpdateTime = DateTime.Now
                };
            }
        }

        public async Task<(byte[] FileBytes, string FileName, string ContentType)> ExportCarriersAsync()
        {
            var carriers = await _repository.GetAllCarriersAsync();
            
            var csv = "ID,物流商名稱,聯絡資訊,綠界代碼,建立時間\n";
            foreach (var carrier in carriers)
            {
                var ecpayCode = GetECPayCodeByCarrierName(carrier.Name);
                csv += $"{carrier.Id},{carrier.Name},{carrier.Contact},{ecpayCode},{carrier.CreatedAt:yyyy-MM-dd}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"物流商清單_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            
            return (bytes, fileName, "text/csv");
        }

        public async Task<ShippingSettingsVm> GetShippingSettingsAsync()
        {
            return new ShippingSettingsVm
            {
                FreeShippingThreshold = 1000,
                VipMemberDiscount = 20,
                RemoteAreaSurcharge = 100,
                StandardShippingFee = 60,
                ExpressShippingFee = 120
            };
        }

        public async Task<(bool Success, string Message)> UpdateShippingSettingsAsync(ShippingSettingsVm model)
        {
            await Task.Delay(100);
            return (true, "運費設定更新成功");
        }

        /// <summary>
        /// 應用篩選條件
        /// </summary>
        private List<Carrier> ApplyFilters(List<Carrier> carriers, LogisticsQueryVm query)
        {
            var filtered = carriers.AsEnumerable();

            if (query.CarrierId.HasValue && query.CarrierId.Value > 0)
            {
                filtered = filtered.Where(c => c.Id == query.CarrierId.Value);
            }

            return filtered.ToList();
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
                _ => "STANDARD"
            };
        }
    }
}