using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CouponsController> _logger;

        public CouponsController(AppDbContext context, ILogger<CouponsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Coupons
        // 支援完整的查詢參數，包括廠商篩選
        [HttpGet]
        public async Task<ActionResult<PagedResponseDto<CouponDto>>> GetCoupons([FromQuery] CouponQueryDto query)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                _logger.LogInformation("API: 開始獲取優惠券列表，參數：{@Query}", query);

                var queryable = _context.Coupons.AsNoTracking();

                // 廠商篩選 - API的核心功能
                if (query.SellerId.HasValue)
                {
                    queryable = queryable.Where(c => c.SellersId == query.SellerId.Value);
                    _logger.LogInformation("API: 篩選廠商ID {SellerId} 的優惠券", query.SellerId.Value);
                }

                // 搜尋條件
                if (!string.IsNullOrEmpty(query.Search))
                {
                    queryable = queryable.Where(c =>
                        EF.Functions.Like(c.Title, $"%{query.Search}%") ||
                        EF.Functions.Like(c.DiscountType, $"%{query.Search}%"));
                }

                // 篩選條件
                if (!string.IsNullOrEmpty(query.DiscountType))
                {
                    queryable = queryable.Where(c => c.DiscountType == query.DiscountType);
                }

                if (!string.IsNullOrEmpty(query.Status))
                {
                    var now = DateTime.Now;
                    queryable = query.Status switch
                    {
                        "啟用" => queryable.Where(c => c.IsActive && c.StartAt <= now && c.ExpiredAt >= now),
                        "未開始" => queryable.Where(c => c.StartAt > now),
                        "已過期" => queryable.Where(c => c.ExpiredAt < now),
                        _ => queryable
                    };
                }

                if (query.StartDate.HasValue)
                {
                    var start = query.StartDate.Value;
                    queryable = queryable.Where(c => c.StartAt >= start);
                }

                if (query.EndDate.HasValue)
                {
                    var end = query.EndDate.Value.AddDays(1);
                    queryable = queryable.Where(c => c.StartAt <= end);
                }

                // 排序
                var isDesc = query.SortDirection.ToLower() == "desc";
                queryable = query.SortBy.ToLower() switch
                {
                    "title" => isDesc ? queryable.OrderByDescending(c => c.Title) : queryable.OrderBy(c => c.Title),
                    "discounttype" => isDesc ? queryable.OrderByDescending(c => c.DiscountType) : queryable.OrderBy(c => c.DiscountType),
                    "discountamount" => isDesc ? queryable.OrderByDescending(c => c.DiscountAmount) : queryable.OrderBy(c => c.DiscountAmount),
                    "expiredat" => isDesc ? queryable.OrderByDescending(c => c.ExpiredAt) : queryable.OrderBy(c => c.ExpiredAt),
                    "usedcount" => isDesc ? queryable.OrderByDescending(c => c.UsedCount) : queryable.OrderBy(c => c.UsedCount),
                    "usagelimit" => isDesc ? queryable.OrderByDescending(c => c.UsageLimit) : queryable.OrderBy(c => c.UsageLimit),
                    _ => isDesc ? queryable.OrderByDescending(c => c.StartAt) : queryable.OrderBy(c => c.StartAt)
                };

                var totalCount = await queryable.CountAsync();

                var coupons = await queryable
                    .Skip((query.Page - 1) * query.ItemsPerPage)
                    .Take(query.ItemsPerPage)
                    .ToListAsync();

                var couponDtos = coupons.Select(c => c.ToDto()).ToList();

                var response = new PagedResponseDto<CouponDto>
                {
                    Success = true,
                    Message = "獲取優惠券列表成功",
                    Data = couponDtos,
                    TotalCount = totalCount,
                    CurrentPage = query.Page,
                    TotalPages = (int)Math.Ceiling((double)totalCount / query.ItemsPerPage),
                    ItemsPerPage = query.ItemsPerPage
                };

                _logger.LogInformation("API: 成功返回 {Count} 筆優惠券，總數 {Total}", couponDtos.Count, totalCount);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 獲取優惠券列表失敗");
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("獲取優惠券列表失敗：" + ex.Message));
            }
        }

        // GET: api/Coupons/BySeller/{sellerId}
        // 專門用於取得特定廠商的優惠券
        [HttpGet("BySeller/{sellerId}")]
        public async Task<ActionResult<ApiResponseDto<IEnumerable<CouponDto>>>> GetCouponsBySeller(int sellerId)
        {
            try
            {
                var coupons = await _context.Coupons
                    .Where(c => c.SellersId == sellerId)
                    .AsNoTracking()
                    .ToListAsync();

                var couponDtos = coupons.Select(c => c.ToDto()).ToList();

                _logger.LogInformation("API: 廠商 {SellerId} 共有 {Count} 張優惠券", sellerId, couponDtos.Count);

                return Ok(ApiResponseDto<IEnumerable<CouponDto>>.SuccessResult(
                    couponDtos, 
                    $"成功獲取廠商 {sellerId} 的優惠券"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 獲取廠商 {SellerId} 優惠券失敗", sellerId);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("獲取廠商優惠券失敗：" + ex.Message));
            }
        }

        // GET: api/Coupons/ByMember/{memberId}
        // 賣家中心專用：透過 memberId 取得該賣家的優惠券
        [HttpGet("ByMember/{memberId}")]
        public async Task<ActionResult<ApiResponseDto<IEnumerable<CouponDto>>>> GetCouponsByMember(int memberId, [FromQuery] bool onlyActive = false)
        {
            try
            {
                _logger.LogInformation("API: 開始查詢會員 {MemberId} 的賣家優惠券，僅啟用: {OnlyActive}", memberId, onlyActive);

                // 先通過 memberId 找出對應的 sellerId
                var seller = await _context.Sellers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.MembersId == memberId);

                if (seller == null)
                {
                    _logger.LogWarning("API: 會員 {MemberId} 不是賣家或賣家資料不存在", memberId);
                    return NotFound(ApiResponseDto<object>.ErrorResult("此會員非賣家或賣家資料不存在"));
                }

                // 查詢該賣家的所有優惠券
                var query = _context.Coupons
                    .Where(c => c.SellersId == seller.Id)
                    .AsNoTracking();

                // 如果只要啟用的優惠券
                if (onlyActive)
                {
                    var now = DateTime.Now;
                    query = query.Where(c => 
                        c.IsActive == true &&
                        c.StartAt <= now &&
                        c.ExpiredAt >= now &&
                        (c.UsageLimit == null || c.UsedCount < c.UsageLimit));
                }

                var coupons = await query
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToListAsync();

                var couponDtos = coupons.Select(c => c.ToDto()).ToList();

                _logger.LogInformation("API: 會員 {MemberId} (賣家ID: {SellerId}) 共有 {Count} 張優惠券", memberId, seller.Id, couponDtos.Count);

                return Ok(ApiResponseDto<IEnumerable<CouponDto>>.SuccessResult(
                    couponDtos, 
                    $"成功獲取賣家優惠券"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 獲取會員 {MemberId} 的賣家優惠券失敗", memberId);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("獲取賣家優惠券失敗：" + ex.Message));
            }
        }

        // GET: api/Coupons/Active
        // 取得啟用中的優惠券，支援廠商篩選
        [HttpGet("Active")]
        public async Task<ActionResult<ApiResponseDto<IEnumerable<CouponDto>>>> GetActiveCoupons([FromQuery] int? sellerId = null)
        {
            try
            {
                var now = DateTime.Now;
                var query = _context.Coupons
                    .Where(c => c.IsActive && c.StartAt <= now && c.ExpiredAt >= now);

                if (sellerId.HasValue)
                {
                    query = query.Where(c => c.SellersId == sellerId.Value);
                    _logger.LogInformation("API: 獲取廠商 {SellerId} 的啟用優惠券", sellerId.Value);
                }

                var coupons = await query.AsNoTracking().ToListAsync();
                var couponDtos = coupons.Select(c => c.ToDto()).ToList();

                return Ok(ApiResponseDto<IEnumerable<CouponDto>>.SuccessResult(
                    couponDtos, 
                    "成功獲取啟用中的優惠券"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 獲取啟用優惠券失敗");
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("獲取啟用優惠券失敗：" + ex.Message));
            }
        }

        // GET: api/Coupons/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<CouponDto>>> GetCoupon(int id, [FromQuery] int? sellerId = null)
        {
            try
            {
                var query = _context.Coupons.AsQueryable();

                // 如果指定了廠商ID，確保只能取得該廠商的優惠券
                if (sellerId.HasValue)
                {
                    query = query.Where(c => c.SellersId == sellerId.Value);
                }

                var coupon = await query.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

                if (coupon == null)
                {
                    return NotFound(ApiResponseDto<object>.ErrorResult("找不到指定的優惠券"));
                }

                return Ok(ApiResponseDto<CouponDto>.SuccessResult(
                    coupon.ToDto(), 
                    "成功獲取優惠券詳情"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 獲取優惠券 {Id} 失敗", id);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("獲取優惠券失敗：" + ex.Message));
            }
        }

        // PUT: api/Coupons/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCoupon(int id, UpdateCouponDto dto, [FromQuery] int? sellerId = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                var query = _context.Coupons.AsQueryable();

                // 如果指定了廠商ID，確保只能更新該廠商的優惠券
                if (sellerId.HasValue)
                {
                    query = query.Where(c => c.SellersId == sellerId.Value);
                }

                var coupon = await query.FirstOrDefaultAsync(c => c.Id == id);

                if (coupon == null)
                {
                    return NotFound(ApiResponseDto<object>.ErrorResult("找不到指定的優惠券或無權限修改"));
                }

                // 如果指定了廠商ID，確保SellersId不被修改
                if (sellerId.HasValue)
                {
                    dto.SellersId = sellerId.Value;
                }

                dto.UpdateEntity(coupon);
                await _context.SaveChangesAsync();

                _logger.LogInformation("API: 成功更新優惠券 {Id}", id);

                return Ok(ApiResponseDto<CouponDto>.SuccessResult(
                    coupon.ToDto(), 
                    "優惠券更新成功"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 更新優惠券 {Id} 失敗", id);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("更新優惠券失敗：" + ex.Message));
            }
        }

        // POST: api/Coupons
        [HttpPost]
        public async Task<ActionResult<ApiResponseDto<CouponDto>>> PostCoupon(CreateCouponDto dto, [FromQuery] int? sellerId = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(ApiResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                // 如果指定了廠商ID，自動設定優惠券的廠商ID
                if (sellerId.HasValue)
                {
                    dto.SellersId = sellerId.Value;
                }

                var coupon = dto.ToEntity();
                _context.Coupons.Add(coupon);
                await _context.SaveChangesAsync();

                _logger.LogInformation("API: 成功創建優惠券 {Id}，廠商ID: {SellerId}", coupon.Id, coupon.SellersId);

                return CreatedAtAction(
                    "GetCoupon", 
                    new { id = coupon.Id }, 
                    ApiResponseDto<CouponDto>.SuccessResult(coupon.ToDto(), "優惠券創建成功")
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 創建優惠券失敗");
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("創建優惠券失敗：" + ex.Message));
            }
        }

        // DELETE: api/Coupons/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCoupon(int id, [FromQuery] int? sellerId = null)
        {
            try
            {
                var query = _context.Coupons.AsQueryable();

                // 如果指定了廠商ID，確保只能刪除該廠商的優惠券
                if (sellerId.HasValue)
                {
                    query = query.Where(c => c.SellersId == sellerId.Value);
                }

                var coupon = await query.FirstOrDefaultAsync(c => c.Id == id);

                if (coupon == null)
                {
                    return NotFound(ApiResponseDto<object>.ErrorResult("找不到指定的優惠券或無權限刪除"));
                }

                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();

                _logger.LogInformation("API: 成功刪除優惠券 {Id}", id);

                return Ok(ApiResponseDto<object>.SuccessResult(null, "優惠券刪除成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 刪除優惠券 {Id} 失敗", id);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("刪除優惠券失敗：" + ex.Message));
            }
        }

        // GET: api/Coupons/Statistics
        // 獲取優惠券統計資料，支援廠商篩選
        [HttpGet("Statistics")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetStatistics([FromQuery] int? sellerId = null)
        {
            try
            {
                var query = _context.Coupons.AsQueryable();

                if (sellerId.HasValue)
                {
                    query = query.Where(c => c.SellersId == sellerId.Value);
                    _logger.LogInformation("API: 獲取廠商 {SellerId} 的優惠券統計", sellerId.Value);
                }

                var totalCount = await query.CountAsync();
                var now = DateTime.Now;
                var activeCount = await query.CountAsync(c => c.IsActive && c.StartAt <= now && c.ExpiredAt >= now);
                var expiredCount = await query.CountAsync(c => c.ExpiredAt < now);

                // 取得類型統計
                var typeStats = await query
                    .GroupBy(c => c.DiscountType)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());

                var statusStats = new Dictionary<string, int>
                {
                    ["啟用"] = activeCount,
                    ["已過期"] = expiredCount,
                    ["未開始"] = totalCount - activeCount - expiredCount
                };

                var stats = new
                {
                    TotalCount = totalCount,
                    ActiveCount = activeCount,
                    ExpiredCount = expiredCount,
                    TypeStats = typeStats,
                    StatusStats = statusStats,
                    SellerId = sellerId
                };

                return Ok(ApiResponseDto<object>.SuccessResult(stats, "獲取統計資料成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 獲取統計資料失敗");
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("獲取統計資料失敗：" + ex.Message));
            }
        }

        // GET: api/Coupons/UserAvailable/{userId}
        // 取得用戶可使用的優惠券清單（前端購物車頁面用）
        [HttpGet("UserAvailable/{userId}")]
        public async Task<ActionResult<ApiResponseDto<IEnumerable<CouponDto>>>> GetUserAvailableCoupons(int userId)
        {
            try
            {
                var now = DateTime.Now;
                
                _logger.LogInformation("API: 開始查詢用戶 {UserId} 的可用優惠券", userId);
                
                // 使用原生 SQL 查詢，符合 snake_case 資料庫結構
                var sql = @"
                    SELECT 
                        c.Id,
                        c.Title,
                        c.Discount_Type,
                        c.Discount_Amount,
                        c.Min_Spend,
                        c.Start_At,
                        c.Expired_At,
                        c.Usage_Limit,
                        c.Used_Count,
                        c.Is_Active
                    FROM Member_Coupons mc
                    INNER JOIN Coupons c ON mc.Coupon_Id = c.Id
                    WHERE mc.Member_Id = @userId
                        AND (mc.Status != 'used' AND mc.Used_At IS NULL)  -- 未使用的優惠券
                        AND c.Is_Active = 1                               -- 啟用的優惠券
                        AND c.Start_At <= @now                            -- 已開始
                        AND c.Expired_At >= @now                          -- 未過期
                    ORDER BY c.Expired_At ASC";
                
                var userCoupons = new List<CouponDto>();
                
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@userId", userId));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@now", now));
                    
                    await _context.Database.OpenConnectionAsync();
                    
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            userCoupons.Add(new CouponDto
                            {
                                Id = reader.GetInt32(0),                    // c.Id
                                Title = reader.GetString(1),                // c.Title
                                DiscountType = reader.GetString(2),         // c.Discount_Type
                                DiscountAmount = reader.GetInt32(3),        // c.Discount_Amount
                                MinSpend = reader.IsDBNull(4) ? null : reader.GetInt32(4),  // c.Min_Spend
                                StartAt = reader.GetDateTime(5),            // c.Start_At
                                ExpiredAt = reader.GetDateTime(6),          // c.Expired_At
                                UsageLimit = reader.IsDBNull(7) ? null : reader.GetInt32(7), // c.Usage_Limit
                                UsedCount = reader.GetInt32(8),             // c.Used_Count
                                IsActive = reader.GetBoolean(9)             // c.Is_Active
                            });
                        }
                    }
                }

                _logger.LogInformation("API: 用戶 {UserId} 可用優惠券數量: {Count}", userId, userCoupons.Count);

                // 如果沒有找到優惠券，記錄詳細診斷資訊
                if (!userCoupons.Any())
                {
                    _logger.LogWarning("API: 用戶 {UserId} 沒有可用優惠券，進行診斷查詢", userId);
                    
                    // 診斷查詢：檢查 Member_Coupons 表
                    var diagnosticSql1 = "SELECT COUNT(*) FROM Member_Coupons WHERE Member_Id = @userId";
                    using (var diagCommand1 = _context.Database.GetDbConnection().CreateCommand())
                    {
                        diagCommand1.CommandText = diagnosticSql1;
                        diagCommand1.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@userId", userId));
                        var memberCouponsCount = (int)await diagCommand1.ExecuteScalarAsync();
                        _logger.LogInformation("API: 用戶 {UserId} 在 Member_Coupons 表中共有 {Count} 筆記錄", userId, memberCouponsCount);
                    }
                    
                    // 診斷查詢：檢查 Coupons 表
                    var diagnosticSql2 = "SELECT COUNT(*) FROM Coupons WHERE Is_Active = 1";
                    using (var diagCommand2 = _context.Database.GetDbConnection().CreateCommand())
                    {
                        diagCommand2.CommandText = diagnosticSql2;
                        var activeCouponsCount = (int)await diagCommand2.ExecuteScalarAsync();
                        _logger.LogInformation("API: Coupons 表中共有 {Count} 張啟用的優惠券", activeCouponsCount);
                    }
                }

                return Ok(ApiResponseDto<IEnumerable<CouponDto>>.SuccessResult(
                    userCoupons, 
                    "成功取得可用優惠券清單"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: 取得用戶可用優惠券失敗，用戶ID: {UserId}", userId);
                return StatusCode(500, ApiResponseDto<object>.ErrorResult("取得可用優惠券失敗：" + ex.Message));
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private bool CouponExists(int id)
        {
            return _context.Coupons.Any(e => e.Id == id);
        }
    }
}
