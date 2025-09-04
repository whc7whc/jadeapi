using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using static Team.Backend.Constants.FinanceConstants;

namespace Team.Backend.Repositories
{
    public class FinanceRepository : IFinanceRepository
    {
        private readonly AppDbContext _context;

        public FinanceRepository(AppDbContext context)
        {
            _context = context;
        }

        #region 基本統計
        public async Task<int> GetTotalMembersAsync()
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

        public async Task<decimal> GetTotalRevenueAsync(int year, int month)
        {
            try
            {
                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt.Year == year &&
                                o.CreatedAt.Month == month)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // 修正：新增語意清楚的全部歷史點數方法
        public async Task<int> GetTotalPointsIssuedAsync(int year, int month)
        {
            try
            {
                // 當 year 和 month 為 0 時，查詢全部歷史發放點數
                if (year == 0 && month == 0)
                {
                    // 修正：確保計算所有獲得點數的類型，與餘額計算保持一致
                    var totalEarned = await _context.PointsLogs
                        .Where(p => p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund")
                        .SumAsync(p => (int?)p.Amount) ?? 0;
                    
                    // 確保返回正數（與其他方法保持一致）
                    return Math.Abs(totalEarned);
                }
                
                // 否則查詢指定年月
                var monthlyEarned = await _context.PointsLogs
                    .Where(p => (p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund") &&
                                p.CreatedAt.Year == year &&
                                p.CreatedAt.Month == month)
                    .SumAsync(p => (int?)p.Amount) ?? 0;
                
                return Math.Abs(monthlyEarned);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTotalPointsIssuedAsync: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 獲取當月發放點數（用於月報統計）
        /// </summary>
        public async Task<int> GetMonthlyPointsIssuedAsync(int year, int month)
        {
            try
            {
                return await _context.PointsLogs
                    .Where(p => (p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund") &&
                                p.CreatedAt.Year == year &&
                                p.CreatedAt.Month == month)
                    .SumAsync(p => (int?)p.Amount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetTotalPointsUsedAsync()
        {
            try
            {
                // 修正：包含所有使用點數的類型，並確保結果為正數
                var totalUsed = await _context.PointsLogs
                    .Where(p => p.Type == PointsType.Used || p.Type == "expired")
                    .SumAsync(p => (int?)p.Amount) ?? 0;

                // 確保返回正數 - 統一處理邏輯
                return Math.Abs(totalUsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTotalPointsUsedAsync: {ex.Message}");
                return 0;
            }
        }
        #endregion

        #region 關鍵指標
        public async Task<decimal> GetAverageOrderValueAsync(int year, int month)
        {
            try
            {
                var orders = _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt.Year == year &&
                                o.CreatedAt.Month == month);

                var count = await orders.CountAsync();
                if (count == 0) return 0;

                var total = await orders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
                return total / count;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetActiveMembersThisMonthAsync(int year, int month)
        {
            try
            {
                return await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month)
                    .Select(o => o.MemberId)
                    .Distinct()
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<decimal> GetGrowthRateAsync(int year, int month)
        {
            try
            {
                // 計算當月營收
                var currentMonthRevenue = await GetTotalRevenueAsync(year, month);
                
                // 計算上月營收
                var previousMonth = month == 1 ? 12 : month - 1;
                var previousYear = month == 1 ? year - 1 : year;
                var previousMonthRevenue = await GetTotalRevenueAsync(previousYear, previousMonth);

                if (previousMonthRevenue == 0) return currentMonthRevenue > 0 ? 100 : 0;

                return ((currentMonthRevenue - previousMonthRevenue) / previousMonthRevenue) * 100;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetNewMembersThisMonthAsync(int year, int month)
        {
            try
            {
                return await _context.Members
                    .Where(m => m.CreatedAt.Year == year && m.CreatedAt.Month == month)
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<decimal> GetRefundAmountAsync(int year, int month)
        {
            try
            {
                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Canceled" || o.OrderStatus == "已取消") &&
                                o.CreatedAt.Year == year &&
                                o.CreatedAt.Month == month)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetCouponUsageAsync(int year, int month)
        {
            try
            {
                return await _context.Orders
                    .Where(o => o.CreatedAt.Year == year &&
                                o.CreatedAt.Month == month &&
                                o.CouponId != null)
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<decimal> GetCouponDiscountAsync(int year, int month)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year &&
                                o.CreatedAt.Month == month &&
                                o.CouponId != null)
                    .Include(o => o.Coupon)
                    .ToListAsync();

                decimal totalDiscount = 0;
                foreach (var order in orders)
                {
                    if (order.Coupon != null)
                    {
                        totalDiscount += order.Coupon.DiscountAmount;
                    }
                }
                return totalDiscount;
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region 趨勢分析
        // 修正：使用單次 GroupBy 查詢，提升效能並解決圖表數據為 0 的問題
        public async Task<List<(string Month, decimal Revenue, int OrderCount)>> GetMonthlyRevenuesAsync(int year)
        {
            try
            {
                // 方案 A：一次 GroupBy，最簡潔且高效
                var grouped = await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") && 
                                o.CreatedAt.Year == year)
                    .GroupBy(o => o.CreatedAt.Month)
                    .Select(g => new { 
                        Month = g.Key, 
                        Revenue = g.Sum(o => (decimal)o.TotalAmount), 
                        OrderCount = g.Count() 
                    })
                    .ToListAsync();

                // 補齊 1~12 月的數據
                return Enumerable.Range(1, 12)
                    .Select(m => {
                        var hit = grouped.FirstOrDefault(x => x.Month == m);
                        return ($"{m}月", hit?.Revenue ?? 0m, hit?.OrderCount ?? 0);
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMonthlyRevenuesAsync: {ex.Message}");
                return Enumerable.Range(1, 12).Select(m => ($"{m}月", 0m, 0)).ToList();
            }
        }

        // 修正：改善分類銷售查詢邏輯，解決數據為 0 和重複問題
        public async Task<List<(string CategoryName, decimal Sales, int ProductCount)>> GetCategorySalesAsync(int year, int month)
        {
            try
            {
                // 修正：狀態兼容中英文，金額處理更安全，加入完整的空值防護
                var categorySales = await _context.OrderDetails
                    .Where(od => (od.Order.OrderStatus == "Completed" || od.Order.OrderStatus == "已完成") &&
                                od.Order.CreatedAt.Year == year &&
                                od.Order.CreatedAt.Month == month &&
                                od.UnitPrice.HasValue &&
                                od.Quantity.HasValue)
                    .Include(od => od.Product)
                    .ThenInclude(p => p.SubCategory)
                    .ThenInclude(sc => sc.Category)
                    .GroupBy(od => 
                        od.Product != null && od.Product.SubCategory != null && od.Product.SubCategory.Category != null
                            ? od.Product.SubCategory.Category.Name
                            : "其他"
                    )
                    .Select(g => new
                    {
                        CategoryName = g.Key,
                        // 修正：全 decimal 計算，避免型別混算導致 EF 翻譯錯誤
                        Sales = g.Sum(od => (od.UnitPrice ?? 0m) * (od.Quantity ?? 0)),
                        ProductCount = g.Select(od => od.ProductId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.Sales)
                    .ToListAsync();

                // 如果有銷售數據，直接返回，不需要補齊所有分類（讓前端圖表更乾淨）
                if (categorySales.Any())
                {
                    return categorySales.Select(cs => (
                        cs.CategoryName ?? "其他", 
                        cs.Sales, 
                        cs.ProductCount
                    )).ToList();
                }

                // 沒有銷售數據時返回預設分類（避免空圖表）
                return new List<(string, decimal, int)>
                {
                    ("服飾", 0, 0),
                    ("鞋子", 0, 0),
                    ("配件", 0, 0),
                    ("其他", 0, 0)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCategorySalesAsync: {ex.Message}");                
                return new List<(string, decimal, int)>
                {
                    ("服飾", 0, 0),
                    ("鞋子", 0, 0),
                    ("配件", 0, 0),
                    ("其他", 0, 0)
                };
            }
        }
        #endregion

        #region 營運效率
        public async Task<decimal> GetProfitMarginAsync(int year, int month)
        {
            try
            {
                var revenue = await GetTotalRevenueAsync(year, month);
                var refund = await GetRefundAmountAsync(year, month);
                var couponDiscount = await GetCouponDiscountAsync(year, month);

                if (revenue == 0) return 0;

                var netRevenue = revenue - refund - couponDiscount;
                return (netRevenue / revenue) * 100;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetTopSellingProductsCountAsync(int year, int month)
        {
            try
            {
                return await _context.OrderDetails
                    .Where(od => (od.Order.OrderStatus == "Completed" || od.Order.OrderStatus == "已完成") &&
                                 od.Order.CreatedAt.Year == year &&
                                 od.Order.CreatedAt.Month == month)
                    .Select(od => od.ProductId)
                    .Distinct()
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<decimal> GetMemberRetentionRateAsync(int year, int month)
        {
            try
            {
                var totalMembers = await GetTotalMembersAsync();
                var activeMembers = await GetActiveMembersThisMonthAsync(year, month);

                if (totalMembers == 0) return 0;

                return ((decimal)activeMembers / totalMembers) * 100;
            }
            catch
            {
                return 0;
            }
        }

        // 新增：今日營收
        public async Task<decimal> GetTodayRevenueAsync()
        {
            try
            {
                var today = DateTime.Today;
                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt.Date == today)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // 新增：昨日營收
        public async Task<decimal> GetYesterdayRevenueAsync()
        {
            try
            {
                var yesterday = DateTime.Today.AddDays(-1);
                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt.Date == yesterday)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // 新增：本週營收
        public async Task<decimal> GetWeekRevenueAsync()
        {
            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek); // 本週日
                var endOfWeek = startOfWeek.AddDays(7); // 下週日

                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt >= startOfWeek &&
                                o.CreatedAt < endOfWeek)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // 新增：上週營收
        public async Task<decimal> GetLastWeekRevenueAsync()
        {
            try
            {
                var today = DateTime.Today;
                var startOfLastWeek = today.AddDays(-(int)today.DayOfWeek - 7); // 上週日
                var endOfLastWeek = startOfLastWeek.AddDays(7); // 本週日

                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt >= startOfLastWeek &&
                                o.CreatedAt < endOfLastWeek)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // 新增：年度營收
        public async Task<decimal> GetYearRevenueAsync(int year)
        {
            try
            {
                return await _context.Orders
                    .Where(o => (o.OrderStatus == "Completed" || o.OrderStatus == "已完成") &&
                                o.CreatedAt.Year == year)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region 訂單統計方法
        public async Task<int> GetTodayOrdersCountAsync()
        {
            try
            {
                var today = DateTime.Today;
                return await _context.Orders
                    .Where(o => o.CreatedAt.Date == today)
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetPendingOrdersCountAsync()
        {
            try
            {
                // 修正：使用正確的英文狀態值
                return await _context.Orders
                    .Where(o => o.OrderStatus == "Pending" || o.OrderStatus == "Processing")
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<decimal> GetOrderCompletionRateAsync(int year, int month)
        {
            try
            {
                var totalOrders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month)
                    .CountAsync();

                if (totalOrders == 0) return 0;

                var completedOrders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month &&
                                (o.OrderStatus == "Completed" || o.OrderStatus == "已完成"))
                    .CountAsync();

                return ((decimal)completedOrders / totalOrders) * 100;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<List<(string Date, int OrderCount, int CompletedCount)>> GetDailyOrderTrendAsync(int days = 7)
        {
            try
            {
                var result = new List<(string Date, int OrderCount, int CompletedCount)>();
                var today = DateTime.Today;

                for (int i = days - 1; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    
                    var orderCount = await _context.Orders
                        .Where(o => o.CreatedAt.Date == date.Date)
                        .CountAsync();

                    var completedCount = await _context.Orders
                        .Where(o => o.CreatedAt.Date == date.Date &&
                                    (o.OrderStatus == "Completed" || o.OrderStatus == "已完成"))
                        .CountAsync();

                    result.Add(($"{date.Month}/{date.Day}", orderCount, completedCount));
                }

                return result;
            }
            catch
            {
                return Enumerable.Range(0, days).Select(i => ($"{DateTime.Today.AddDays(-i).Month}/{DateTime.Today.AddDays(-i).Day}", 0, 0)).ToList();
            }
        }

        // 修正：先標準化再分組，避免重複狀態被拆分
        public async Task<List<(string Status, int Count, decimal Percentage)>> GetOrderStatusDistributionAsync(int year, int month)
        {
            try
            {
                // 修正：先做標準化映射，避免 "Completed" 和 "已完成" 被分成兩筆
                var rawOrders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month)
                    .Select(o => new { 
                        StandardizedStatus = 
                            o.OrderStatus == "Completed" ? "已完成" :
                            o.OrderStatus == "已完成" ? "已完成" :
                            o.OrderStatus == "Canceled" ? "已取消" :
                            o.OrderStatus == "已取消" ? "已取消" :
                            o.OrderStatus == "Processing" ? "處理中" :
                            o.OrderStatus == "Pending" ? "待處理" :
                            o.OrderStatus == "Shipping" ? "配送中" :
                            o.OrderStatus // 其他狀態保持原樣
                    })
                    .ToListAsync();

                if (!rawOrders.Any()) 
                    return new List<(string, int, decimal)>();

                // 再進行分組聚合
                var grouped = rawOrders
                    .GroupBy(x => x.StandardizedStatus)
                    .Select(g => new { 
                        Status = g.Key, 
                        Count = g.Count() 
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var totalCount = grouped.Sum(x => x.Count);
                
                return grouped.Select(x => (
                    x.Status,
                    x.Count,
                    totalCount > 0 ? (decimal)x.Count / totalCount * 100 : 0
                )).ToList();
            }
            catch
            {
                return new List<(string, int, decimal)>
                {
                    ("已完成", 0, 0),
                    ("處理中", 0, 0),
                    ("待處理", 0, 0),
                    ("已取消", 0, 0)
                };
            }
        }

        public async Task<List<(string Hour, int OrderCount)>> GetOrderTimeDistributionAsync(int year, int month)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month)
                    .ToListAsync();

                var timeGroups = orders
                    .GroupBy(o => o.CreatedAt.Hour / 6) // 分成4個時段：0-5, 6-11, 12-17, 18-23
                    .Select(g => new { TimeGroup = g.Key, Count = g.Count() })
                    .ToList();

                var timeLabels = new[] { "00-06", "06-12", "12-18", "18-24" };
                var result = new List<(string Hour, int OrderCount)>();

                for (int i = 0; i < 4; i++)
                {
                    var count = timeGroups.FirstOrDefault(t => t.TimeGroup == i)?.Count ?? 0;
                    result.Add((timeLabels[i], count));
                }

                return result;
            }
            catch
            {
                return new List<(string, int)>
                {
                    ("00-06", 0),
                    ("06-12", 0),
                    ("12-18", 0),
                    ("18-24", 0)
                };
            }
        }

        public async Task<decimal> GetReturnRateAsync(int year, int month)
        {
            try
            {
                var totalOrders = await _context.Orders
                    .Where(o => o.CreatedAt.Year == year && o.CreatedAt.Month == month)
                    .CountAsync();

                if (totalOrders == 0) return 0;

                // 如果 Returns 表的 CreatedAt 是可為 null 的，需要特別處理
                var returnedOrders = await _context.Returns
                    .Where(r => r.CreatedAt.HasValue && 
                                r.CreatedAt.Value.Year == year && 
                                r.CreatedAt.Value.Month == month)
                    .Select(r => r.OrderId)
                    .Distinct()
                    .CountAsync();

                return ((decimal)returnedOrders / totalOrders) * 100;
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region 點數管理方法 - 只保留真實數據
        public async Task<int> GetTotalPointsBalanceAsync()
        {
            try
            {
                // 修正：包含所有獲得點數的類型，與發放計算保持完全一致
                var totalEarned = await _context.PointsLogs
                    .Where(p => p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund")
                    .SumAsync(p => (int?)p.Amount) ?? 0;

                // 修正：包含所有使用點數的類型，確保計算正確
                var totalUsed = await _context.PointsLogs
                    .Where(p => p.Type == PointsType.Used || p.Type == "expired")
                    .SumAsync(p => (int?)p.Amount) ?? 0;

                // 確保計算邏輯：餘額 = 發放總數 - 使用總數
                // 注意：如果資料庫中 used 記錄是負數，需要取絕對值
                var netBalance = Math.Abs(totalEarned) - Math.Abs(totalUsed);
                
                // 餘額不能為負數，最小為 0
                return Math.Max(0, netBalance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTotalPointsBalanceAsync: {ex.Message}");
                return 0;
            }
        }

        public async Task<int> GetTodayPointsChangeAsync()
        {
            try
            {
                var today = DateTime.Today;
                
                // 修正：包含所有增加點數的類型（earned + signin + refund）
                var todayEarned = await _context.PointsLogs
                    .Where(p => (p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund") &&
                                p.CreatedAt.Date == today)
                    .SumAsync(p => (int?)p.Amount) ?? 0;

                var todayUsed = await _context.PointsLogs
                    .Where(p => (p.Type == PointsType.Used || p.Type == "expired") &&
                                p.CreatedAt.Date == today)
                    .SumAsync(p => (int?)p.Amount) ?? 0;

                // 修正：確保計算邏輯一致 - 今日淨變動 = 今日發放 - 今日使用
                return Math.Abs(todayEarned) - Math.Abs(todayUsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTodayPointsChangeAsync: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<(string MemberName, string Email, int Points, string Level)>> GetTopPointsHoldersAsync(int top = 10)
        {
            try
            {
                // 第一步：獲取所有點數記錄並進行基本檢查
                
                var totalPointsLogs = await _context.PointsLogs.CountAsync();
                
                if (totalPointsLogs == 0)
                {
                    return new List<(string, string, int, string)>();
                }

                // 第二步：檢查會員和 Profile 關聯
                var membersWithProfiles = await _context.Members
                    .Include(m => m.Profile)
                    .Where(m => m.Profile != null)
                    .CountAsync();

                // 第三步：修正查詢邏輯 - 分步驟進行
                var pointsStatsByMember = await _context.PointsLogs
                    .GroupBy(p => p.MemberId)
                    .Select(g => new
                    {
                        MemberId = g.Key,
                        TotalEarned = g.Where(p => p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund").Sum(p => p.Amount),
                        TotalUsed = g.Where(p => p.Type == PointsType.Used || p.Type == "expired").Sum(p => p.Amount)
                    })
                    .ToListAsync();

                // 第四步：計算淨點數並過濾出有正點數的會員
                var membersWithPositivePoints = pointsStatsByMember
                    .Select(mp => new
                    {
                        mp.MemberId,
                        NetPoints = mp.TotalEarned - Math.Abs(mp.TotalUsed)
                    })
                    .Where(mp => mp.NetPoints > 0)
                    .OrderByDescending(mp => mp.NetPoints)
                    .Take(top)
                    .ToList();

                if (!membersWithPositivePoints.Any())
                {
                    return new List<(string, string, int, string)>();
                }

                // 第五步：獲取會員詳細信息 - 包含會員真實等級
                var memberIds = membersWithPositivePoints.Select(mp => mp.MemberId).ToList();
                var memberDetails = await _context.Members
                    .Include(m => m.Profile)
                    .Where(m => memberIds.Contains(m.Id))
                    .ToListAsync();

                // 第六步：組合結果 - 使用會員真實等級
                var result = new List<(string, string, int, string)>();
                
                foreach (var memberWithPoints in membersWithPositivePoints)
                {
                    var member = memberDetails.FirstOrDefault(m => m.Id == memberWithPoints.MemberId);
                    if (member != null)
                    {
                        var memberName = member.Profile?.Name ?? "未設定姓名";
                        var memberEmail = member.Email ?? "無Email";
                        var points = memberWithPoints.NetPoints;
                        var level = GetRealMemberLevel(member.Level);

                        result.Add((memberName, memberEmail, points, level));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetTopPointsHoldersAsync 發生錯誤: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return new List<(string, string, int, string)>();
            }
        }

        public async Task<List<(DateTime Date, string MemberEmail, string Type, int Amount, int Balance, string Description)>> GetRecentPointsLogsAsync(int count = 20)
        {
            try
            {
                var logs = await _context.PointsLogs
                    .Include(p => p.Member)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                var result = new List<(DateTime, string, string, int, int, string)>();
                
                foreach (var log in logs)
                {
                    var typeLabel = GetPointsTypeLabel(log.Type);
                    var memberEmail = log.Member?.Email ?? "系統";
                    var description = GetPointsDescription(log.Type, log.Note);
                    var balance = 0; // 簡化計算，實際可根據需要實現更精確的餘額計算
                    
                    result.Add((log.CreatedAt, memberEmail, typeLabel, log.Amount, balance, description));
                }

                return result;
            }
            catch
            {
                return new List<(DateTime, string, string, int, int, string)>();
            }
        }
        #endregion

        #region 報表數據方法
        public async Task<object> GetRevenueReportDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate &&
                                (o.OrderStatus == "Completed" || o.OrderStatus == "已完成"))
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .Include(o => o.Member)
                    .ToListAsync();

                var totalRevenue = orders.Sum(o => o.TotalAmount);
                var totalOrders = orders.Count;
                var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

                var dailyRevenue = orders
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Revenue = g.Sum(o => o.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .OrderBy(dr => dr.Date)
                    .ToList();

                return new
                {
                    Summary = new
                    {
                        TotalRevenue = totalRevenue,
                        TotalOrders = totalOrders,
                        AverageOrderValue = averageOrderValue,
                        StartDate = startDate,
                        EndDate = endDate
                    },
                    DailyData = dailyRevenue,
                    Orders = orders.Select(o => new
                    {
                        o.Id,
                        o.CreatedAt,
                        o.TotalAmount,
                        o.OrderStatus,
                        MemberEmail = o.Member?.Email ?? "訪客",
                        ItemCount = o.OrderDetails?.Count ?? 0
                    }).ToList()
                };
            }
            catch
            {
                return new { Summary = new { TotalRevenue = 0, TotalOrders = 0 }, DailyData = new object[0], Orders = new object[0] };
            }
        }

        public async Task<object> GetOrderReportDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                    .Include(o => o.Member)
                    .Include(o => o.OrderDetails)
                    .ToListAsync();

                var statusDistribution = orders
                    .GroupBy(o => o.OrderStatus)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        Percentage = orders.Count > 0 ? (decimal)g.Count() / orders.Count * 100 : 0
                    })
                    .ToList();

                var dailyOrders = orders
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        TotalOrders = g.Count(),
                        CompletedOrders = g.Count(o => o.OrderStatus == "Completed" || o.OrderStatus == "已完成"),
                        CanceledOrders = g.Count(o => o.OrderStatus == "Canceled" || o.OrderStatus == "已取消")
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                return new
                {
                    Summary = new
                    {
                        TotalOrders = orders.Count,
                        CompletedOrders = orders.Count(o => o.OrderStatus == "Completed" || o.OrderStatus == "已完成"),
                        CanceledOrders = orders.Count(o => o.OrderStatus == "Canceled" || o.OrderStatus == "已取消"),
                        CompletionRate = orders.Count > 0 ? (decimal)orders.Count(o => o.OrderStatus == "Completed" || o.OrderStatus == "已完成") / orders.Count * 100 : 0
                    },
                    StatusDistribution = statusDistribution,
                    DailyData = dailyOrders,
                    DetailedOrders = orders.Take(100).Select(o => new
                    {
                        o.Id,
                        o.CreatedAt,
                        o.TotalAmount,
                        o.OrderStatus,
                        MemberEmail = o.Member?.Email ?? "訪客",
                        ItemCount = o.OrderDetails?.Count ?? 0
                    }).ToList()
                };
            }
            catch
            {
                return new { Summary = new { TotalOrders = 0 }, StatusDistribution = new object[0], DailyData = new object[0] };
            }
        }

        public async Task<object> GetMemberReportDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var newMembers = await _context.Members
                    .Where(m => m.CreatedAt >= startDate && m.CreatedAt <= endDate)
                    .Include(m => m.Profile)
                    .ToListAsync();

                var activeMembers = await _context.Orders
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                    .Select(o => o.MemberId)
                    .Distinct()
                    .CountAsync();

                var memberActivity = await _context.Orders
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                    .Include(o => o.Member)
                    .GroupBy(o => o.MemberId)
                    .Select(g => new
                    {
                        MemberId = g.Key,
                        Member = g.First().Member,
                        OrderCount = g.Count(),
                        TotalEarned = g.Sum(o => o.TotalAmount)
                    })
                    .OrderByDescending(ma => ma.TotalEarned)
                    .Take(50)
                    .ToListAsync();

                return new
                {
                    Summary = new
                    {
                        NewMembers = newMembers.Count,
                        ActiveMembers = activeMembers,
                        TotalMembers = await _context.Members.CountAsync()
                    },
                    NewMembersList = newMembers.Select(m => new
                    {
                        m.Id,
                        m.Email,
                        m.CreatedAt,
                        Name = m.Profile?.Name ?? "未設定"
                    }).ToList(),
                    TopSpenders = memberActivity.Select(ma => new
                    {
                        MemberEmail = ma.Member?.Email ?? "未知",
                        ma.OrderCount,
                        ma.TotalEarned
                    }).ToList()
                };
            }
            catch
            {
                return new { Summary = new { NewMembers = 0, ActiveMembers = 0 }, NewMembersList = new object[0] };
            }
        }

        public async Task<object> GetPointsReportDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var pointsLogs = await _context.PointsLogs
                    .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                    .Include(p => p.Member)
                    .ToListAsync();

                // 修正：正確分類獲得和使用的點數
                var totalEarned = pointsLogs.Where(p => p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund").Sum(p => p.Amount);
                var totalUsed = pointsLogs.Where(p => p.Type == PointsType.Used || p.Type == "expired").Sum(p => p.Amount);

                var dailyPoints = pointsLogs
                    .GroupBy(p => p.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Earned = g.Where(p => p.Type == PointsType.Earned || p.Type == "signin" || p.Type == "refund").Sum(p => p.Amount),
                        Used = g.Where(p => p.Type == PointsType.Used || p.Type == "expired").Sum(p => p.Amount)
                    })
                    .OrderBy(dp => dp.Date)
                    .ToList();

                return new
                {
                    Summary = new
                    {
                        TotalEarned = totalEarned,
                        TotalUsed = totalUsed,
                        NetChange = totalEarned - totalUsed,
                        TransactionCount = pointsLogs.Count
                    },
                    DailyData = dailyPoints,
                    RecentTransactions = pointsLogs.OrderByDescending(p => p.CreatedAt).Take(100).Select(p => new
                    {
                        p.CreatedAt,
                        Type = GetPointsTypeLabel(p.Type),
                        p.Amount,
                        MemberEmail = p.Member?.Email ?? "系統",
                        Description = GetPointsDescription(p.Type, p.Note)
                    }).ToList()
                };
            }
            catch
            {
                return new { Summary = new { TotalEarned = 0, TotalUsed = 0 }, DailyData = new object[0] };
            }
        }

        public async Task<object> GetComprehensiveReportDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var revenueData = await GetRevenueReportDataAsync(startDate, endDate);
                var orderData = await GetOrderReportDataAsync(startDate, endDate);
                var memberData = await GetMemberReportDataAsync(startDate, endDate);
                var pointsData = await GetPointsReportDataAsync(startDate, endDate);

                return new
                {
                    ReportPeriod = new { StartDate = startDate, EndDate = endDate },
                    Revenue = revenueData,
                    Orders = orderData,
                    Members = memberData,
                    Points = pointsData,
                    GeneratedAt = DateTime.Now
                };
            }
            catch
            {
                return new { Error = "綜合報表生成失敗" };
            }
        }
        #endregion

        #region 輔助方法
        // 修正：加入 default 分支，避免回傳 null
        private string GetRealMemberLevel(int? memberLevel)
        {
            return memberLevel switch
            {
                1 => "銅卡",
                2 => "銀卡", 
                3 => "金卡",
                _ => "一般"   // 加入預設值，避免 null
            };
        }

        private string GetPointsTypeLabel(string type)
        {
            return type switch
            {
                PointsType.Earned => "獲得",
                "signin" => "獲得",
                "refund" => "獲得",
                PointsType.Used => "使用",
                "expired" => "使用",
                _ => "其他"
            };
        }

        private string GetPointsDescription(string type, string note)
        {
            return type switch
            {
                "signin" => "簽到獲得",
                PointsType.Earned => "點數獲得", 
                "refund" => "點數退款",
                PointsType.Used => "點數使用",
                "expired" => "點數過期",
                _ => note ?? "點數異動"
            };
        }
        #endregion
    }
}
