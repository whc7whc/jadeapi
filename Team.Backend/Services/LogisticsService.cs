using Team.Backend.Models.ViewModels.Logistics;
using Team.Backend.Models.EfModel;
using Team.Backend.Repositories;

namespace Team.Backend.Services
{
    /// <summary>
    /// ���y�A�Ȥ���
    /// </summary>
    public interface ILogisticsService
    {
        // ���y�Ӻ޲z
        Task<LogisticsIndexVm> SearchCarriersAsync(LogisticsQueryVm query);
        Task<CarrierDetailVm?> GetCarrierDetailAsync(int id);
        Task<(bool Success, string Message)> CreateCarrierAsync(CarrierCreateVm model);
        Task<(bool Success, string Message)> UpdateCarrierAsync(int id, CarrierEditVm model);
        Task<(bool Success, string Message)> DeleteCarrierAsync(int id);
        
        // ���o���y�ӿﶵ
        Task<List<CarrierOptionVm>> GetAllCarrierOptionsAsync();
        
        // �������y�Ӫ��A
        Task<(bool Success, string Message)> ToggleCarrierStatusAsync(int id);

        // �έp����
        Task<LogisticsStatisticsVm> GetStatisticsAsync();
        Task<(byte[] FileBytes, string FileName, string ContentType)> ExportCarriersAsync();

        // �]�w�޲z
        Task<ShippingSettingsVm> GetShippingSettingsAsync();
        Task<(bool Success, string Message)> UpdateShippingSettingsAsync(ShippingSettingsVm model);
    }

    /// <summary>
    /// ���y�A�ȹ�@
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
                        Name = carrier.Name ?? "���R�W���y��",
                        Contact = carrier.Contact ?? "�L�p����T",
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
                return (false, "���y�ӦW�٤w�s�b");
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
                ? (true, "���y�ӷs�W���\") 
                : (false, "�s�W���ѡA�еy��A��");
        }

        public async Task<(bool Success, string Message)> UpdateCarrierAsync(int id, CarrierEditVm model)
        {
            var carrier = await _repository.GetCarrierByIdAsync(id);
            if (carrier == null)
            {
                return (false, "�䤣����w�����y��");
            }

            if (await _repository.CarrierExistsByNameAsync(model.Name, id))
            {
                return (false, "���y�ӦW�٤w�s�b");
            }

            carrier.Name = model.Name;
            carrier.Contact = model.Contact;

            var success = await _repository.UpdateCarrierAsync(carrier);
            return success 
                ? (true, "���y�ӧ�s���\") 
                : (false, "��s���ѡA�еy��A��");
        }

        public async Task<(bool Success, string Message)> DeleteCarrierAsync(int id)
        {
            var orderCount = await _repository.GetOrderCountByCarrierAsync(id);
            if (orderCount > 0)
            {
                return (false, $"�L�k�R���A�����y���٦� {orderCount} ���q��O��");
            }

            var success = await _repository.DeleteCarrierAsync(id);
            return success 
                ? (true, "���y�ӧR�����\") 
                : (false, "�R�����ѡA�еy��A��");
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
                    return (false, "�䤣����w�����y��");
                }

                carrier.IsActive = !carrier.IsActive;
                var success = await _repository.UpdateCarrierAsync(carrier);
                
                if (success)
                {
                    var status = carrier.IsActive ? "�ҥ�" : "����";
                    return (true, $"���y�ӡu{carrier.Name}�v�w{status}");
                }
                else
                {
                    return (false, "��s���A����");
                }
            }
            catch (Exception ex)
            {
                return (false, $"�������A�ɵo�Ϳ��~�G{ex.Message}");
            }
        }

        public async Task<LogisticsStatisticsVm> GetStatisticsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GetStatisticsAsync] �}�l����έp���");
                
                var carrierStats = await _repository.GetCarrierUsageStatsAsync();
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] Repository �^�� CarrierStats: {carrierStats?.Count ?? 0} ��");
                
                var regionStats = await _repository.GetRegionStatsAsync();
                var avgShippingFee = await _repository.GetAverageShippingFeeAsync();

                // ���ϥμ�����ơA�����ϥθ�Ʈw���u�굲�G
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] �ϥίu���Ʈw���: CarrierStats={carrierStats.Count}, RegionStats={regionStats.Count}");

                // �p��ϥΦʤ���
                var totalOrders = carrierStats.Sum(s => s.OrderCount);
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] �p���`�q���: {totalOrders}");
                
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

                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] �����A�`�q��: {result.TotalOrders}�A���y�Ӽƶq: {result.CarrierStats.Count}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetStatisticsAsync] ���~: {ex.Message}");
                
                // �Y�ϵo�Ϳ��~�A�]��^�Ū��έp��ƦӤ��O�������
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
            
            var csv = "ID,���y�ӦW��,�p����T,��ɥN�X,�إ߮ɶ�\n";
            foreach (var carrier in carriers)
            {
                var ecpayCode = GetECPayCodeByCarrierName(carrier.Name);
                csv += $"{carrier.Id},{carrier.Name},{carrier.Contact},{ecpayCode},{carrier.CreatedAt:yyyy-MM-dd}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"���y�ӲM��_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            
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
            return (true, "�B�O�]�w��s���\");
        }

        /// <summary>
        /// ���οz�����
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
        /// �ھڪ��y�ӦW�٨��o��ɥN�X
        /// </summary>
        private string GetECPayCodeByCarrierName(string carrierName)
        {
            return carrierName switch
            {
                var name when name.Contains("�¿�") => "HOME_TCAT",
                var name when name.Contains("7-11") || name.Contains("7-ELEVEN") => "UNIMART",
                var name when name.Contains("���a") => "FAMI",
                _ => "STANDARD"
            };
        }
    }
}