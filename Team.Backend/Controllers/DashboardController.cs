using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Team.Backend.Models.ViewModels;
using Team.Backend.Services;
using Team.Backend.Models.EfModel;
using Microsoft.EntityFrameworkCore;

namespace Team.Backend.Controllers
{
    public class DashboardController : BaseController
    {
        private readonly AdminFinanceService _financeService;
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardController> _logger;
        
        public DashboardController(AdminFinanceService financeService, AppDbContext context, ILogger<DashboardController> logger)
            : base(context, logger)
        {
            _financeService = financeService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// �D����O
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var dashboardViewModel = new MainDashboardViewModel
            {
                TotalOrders = await GetTotalOrdersCount(),
                NewOrders = await GetNewOrdersCount(),
                TotalRevenue = await GetTotalRevenueAsync(),
                TotalMembers = await GetTotalMembersCount(),
                NewMembers = await GetNewMembersCount(),
                TotalProducts = await GetTotalProductsCount(),
                LowStockProducts = await GetLowStockProductsCount(),
                TotalNotifications = await GetTotalNotificationsCount(),
                TotalArticles = await GetTotalArticlesCount(),
                TotalCoupons = await GetTotalCouponsCount(),
                TotalAds = await GetTotalAdsCount(),
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                EndDate = DateTime.Now
            };

            return View(dashboardViewModel);
        }
        
        /// <summary>
        /// ����O�έp�ƾ�API
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats(DateTime? startDate, DateTime? endDate, int? days)
        {
            try
            {
                DateTime end = endDate ?? DateTime.Now;
                DateTime start;
                
                if (startDate.HasValue)
                {
                    // �ϥδ��Ѫ��}�l���
                    start = startDate.Value;
                }
                else if (days.HasValue && days.Value > 0)
                {
                    // �ھګ��w���Ѽƭp��}�l���
                    start = end.AddDays(-(days.Value - 1)); // �� 1 �O�]���]�t���
                }
                else
                {
                    // �w�]�� 15 ��
                    start = end.AddDays(-14); // �@15��
                }
                
                var stats = new
                {
                    totalOrders = await GetTotalOrdersCount(start, end),
                    newOrders = await GetNewOrdersCount(start, end),
                    totalRevenue = await GetTotalRevenueAsync(start, end),
                    totalMembers = await GetTotalMembersCount(),
                    newMembers = await GetNewMembersCount(start, end),
                    totalProducts = await GetTotalProductsCount(),
                    lowStockProducts = await GetLowStockProductsCount(),
                    totalNotifications = await GetTotalNotificationsCount(start, end),
                    totalArticles = await GetTotalArticlesCount(),
                    totalCoupons = await GetTotalCouponsCount(),
                    totalAds = await GetTotalAdsCount(),
                    salesChart = await GetSalesChartData(start, end),
                    categoryChart = await GetCategoryDistributionData(),
                    dateRange = new { start, end, days = (end - start).Days + 1 }
                };
                
                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        #region �p����k - �έp�ƾ�
        
        private async Task<int> GetTotalOrdersCount(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Orders.AsQueryable();
                
                if (startDate.HasValue)
                    query = query.Where(o => o.CreatedAt >= startDate.Value);
                
                if (endDate.HasValue)
                    query = query.Where(o => o.CreatedAt <= endDate.Value);
                
                return await query.CountAsync();
            }
            catch (Exception)
            {
                return 0;
            }
        }
        
        private async Task<int> GetNewOrdersCount(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Orders.AsQueryable();
                
                if (!startDate.HasValue && !endDate.HasValue)
                    query = query.Where(o => o.CreatedAt.Date == DateTime.Today);
                else
                {
                    if (startDate.HasValue)
                        query = query.Where(o => o.CreatedAt >= startDate.Value);
                    
                    if (endDate.HasValue)
                        query = query.Where(o => o.CreatedAt <= endDate.Value);
                }
                
                return await query.CountAsync();
            }
            catch (Exception)
            {
                return 0;
            }
        }
        
        private async Task<decimal> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Orders
                    .Where(o => o.OrderStatus == "Completed");
                
                if (startDate.HasValue)
                    query = query.Where(o => o.CreatedAt >= startDate.Value);
                
                if (endDate.HasValue)
                    query = query.Where(o => o.CreatedAt <= endDate.Value);
                
                var result = await query.SumAsync(o => o.TotalAmount);
                
                if (result == 0)
                {
                    var orderCount = await query.CountAsync();
                    if (orderCount > 0)
                    {
                        var orderIds = await query.Select(o => o.Id).ToListAsync();
                        result = await _context.OrderDetails
                            .Where(od => orderIds.Contains(od.OrderId))
                            .SumAsync(od => (od.UnitPrice ?? 0) * (od.Quantity ?? 0));
                    }
                }
                
                return result;
            }
            catch
            {
                try
                {
                    var dashboard = await _financeService.GetDashboardAsync(DateTime.Now.Year, DateTime.Now.Month);
                    return dashboard?.TotalRevenue ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }
        
        private async Task<int> GetTotalMembersCount()
        {
            try
            {
                return await _context.Members.CountAsync();
            }
            catch
            {
                return 0;
            }
        }
        
        private async Task<int> GetNewMembersCount(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Members.AsQueryable();
                
                if (!startDate.HasValue && !endDate.HasValue)
                    query = query.Where(m => m.CreatedAt.Date == DateTime.Today);
                else
                {
                    if (startDate.HasValue)
                        query = query.Where(m => m.CreatedAt >= startDate.Value);
                    
                    if (endDate.HasValue)
                        query = query.Where(m => m.CreatedAt <= endDate.Value);
                }
                
                return await query.CountAsync();
            }
            catch
            {
                return 0;
            }
        }
        
        private async Task<int> GetTotalProductsCount()
        {
            try
            {
                return await _context.Products.CountAsync();
            }
            catch
            {
                return 0;
            }
        }
        
        private async Task<int> GetLowStockProductsCount()
        {
            try
            {
                const int lowStockThreshold = 10;
                return await _context.ProductAttributeValues
                    .CountAsync(pav => pav.Stock <= lowStockThreshold);
            }
            catch
            {
                return 0;
            }
        }
        
        private async Task<int> GetTotalNotificationsCount(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Only count notifications that are not deleted
                var query = _context.Notifications.AsQueryable().Where(n => !n.Is_Deleted);
                
                if (startDate.HasValue)
                    query = query.Where(n => n.Created_At >= startDate.Value);
                
                if (endDate.HasValue)
                    query = query.Where(n => n.Created_At <= endDate.Value);
                
                return await query.CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetTotalArticlesCount()
        {
            try
            {
                return await _context.OfficialPosts.CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetTotalCouponsCount()
        {
            try
            {
                return await _context.Coupons.CountAsync();
            }
            catch
            {
                return 0;
            }
        }
        
        private async Task<int> GetTotalAdsCount()
        {
            try
            {
                return await _context.Banners.CountAsync();
            }
            catch
            {
                return 0;
            }
        }
        
        private async Task<object> GetSalesChartData(DateTime startDate, DateTime endDate)
        {
            try
            {
                var dateRange = (endDate - startDate).Days + 1;
                var labels = new List<string>();
                var salesData = new List<decimal>();
                var ordersData = new List<int>();
                
                for (int i = 0; i < dateRange; i++)
                {
                    var date = startDate.AddDays(i);
                    var dayStart = date.Date;
                    var dayEnd = date.Date.AddDays(1).AddSeconds(-1);
                    
                    var dailySales = await _context.Orders
                        .Where(o => o.CreatedAt >= dayStart && o.CreatedAt <= dayEnd && o.OrderStatus == "Completed")
                        .SumAsync(o => o.TotalAmount);
                    
                    var dailyOrders = await _context.Orders
                        .Where(o => o.CreatedAt >= dayStart && o.CreatedAt <= dayEnd)
                        .CountAsync();
                    
                    labels.Add(date.ToString("MM/dd"));
                    salesData.Add(dailySales);
                    ordersData.Add(dailyOrders);
                }
                
                return new
                {
                    labels,
                    datasets = new object[]
                    {
                        new { 
                            label = "�P���B", 
                            data = salesData,
                            borderColor = "#4e73df",
                            backgroundColor = "rgba(78, 115, 223, 0.05)",
                            borderWidth = 2
                        },
                        new { 
                            label = "�q���", 
                            data = ordersData,
                            borderColor = "#1cc88a",
                            backgroundColor = "rgba(28, 200, 138, 0.05)",
                            borderWidth = 2
                        }
                    }
                };
            }
            catch
            {
                return new
                {
                    labels = Array.Empty<string>(),
                    datasets = new object[]
                    {
                        new { label = "�P���B", data = Array.Empty<decimal>() },
                        new { label = "�q���", data = Array.Empty<int>() }
                    }
                };
            }
        }
        
        private async Task<object> GetCategoryDistributionData()
        {
            try
            {
                var categoryData = await _context.Products
                    .Include(p => p.SubCategory)
                    .ThenInclude(sc => sc.Category)
                    .Where(p => p.SubCategory != null && p.SubCategory.Category != null)
                    .GroupBy(p => p.SubCategory.Category.Name)
                    .Select(g => new
                    {
                        Category = g.Key,
                        ProductCount = g.Count()
                    })
                    .ToListAsync();

                var labels = categoryData.Select(c => c.Category).ToArray();
                var counts = categoryData.Select(c => (decimal)c.ProductCount).ToArray();
                var colors = new[]
                {
                    "#4e73df", "#1cc88a", "#36b9cc", "#f6c23e",
                    "#e74a3b", "#5a5c69", "#858796", "#f8f9fc"
                };

                // �Y�L��ơA���Ű}�C
                if (labels.Length == 0)
                {
                    labels = new[] { "�k��", "�k��" };
                    counts = new decimal[labels.Length];
                }

                return new
                {
                    labels,
                    datasets = new[]
                    {
                        new
                        {
                            data = counts,
                            backgroundColor = colors.Take(labels.Length).ToArray()
                        }
                    }
                };
            }
            catch (Exception)
            {
                var labels = new[] { "�k��", "�k��" };
                var counts = new decimal[labels.Length];
                var colors = new[]
                {
                    "#4e73df", "#1cc88a", "#36b9cc", "#f6c23e",
                    "#e74a3b", "#5a5c69", "#858796", "#f8f9fc"
                };
                return new
                {
                    labels,
                    datasets = new object[]
                    {
                        new
                        {
                            data = counts,
                            backgroundColor = colors
                        }
                    }
                };
            }
        }

        #endregion
    }
}