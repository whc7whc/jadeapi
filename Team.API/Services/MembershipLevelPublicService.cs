using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Team.API.Models.EfModel;
using Team.API.DTO;
using Team.API.Models.DTOs;

namespace Team.API.Services
{
    /// <summary>
    /// 會員等級公開查詢服務介面
    /// </summary>
    public interface IMembershipLevelPublicService
    {
        /// <summary>
        /// 取得會員等級清單（分頁）
        /// </summary>
        /// <param name="activeOnly">是否只回啟用中的等級</param>
        /// <param name="includeDescription">是否包含描述欄位</param>
        /// <param name="includeMonthlyCoupon">是否包含每月配券ID</param>
        /// <param name="page">頁碼</param>
        /// <param name="pageSize">每頁筆數</param>
        /// <returns>分頁的等級清單</returns>
        Task<PagedResponseDto<MembershipLevelItemDto>> GetMembershipLevelsAsync(
            bool activeOnly = true, 
            bool includeDescription = false, 
            bool includeMonthlyCoupon = false, 
            int page = 1, 
            int pageSize = 100);

        /// <summary>
        /// 取得會員等級統計資訊
        /// </summary>
        /// <param name="activeOnly">是否只統計啟用中的等級</param>
        /// <returns>等級統計資訊</returns>
        Task<MembershipLevelsStatsDto> GetMembershipLevelsStatsAsync(bool activeOnly = true);

        /// <summary>
        /// 取得單一會員等級詳細資訊
        /// </summary>
        /// <param name="id">等級ID</param>
        /// <returns>等級詳細資訊</returns>
        Task<MembershipLevelItemDto?> GetMembershipLevelByIdAsync(int id);
    }

    /// <summary>
    /// 會員等級公開查詢服務實作
    /// 
    /// ?? 快取策略：
    /// - 等級列表：60秒記憶體快取
    /// - 統計資料：60秒記憶體快取
    /// - 快取鍵：參數組合 + 時間戳
    /// - 失效條件：資料異動時自動失效，或設定時間到期
    /// </summary>
    public class MembershipLevelPublicService : IMembershipLevelPublicService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MembershipLevelPublicService> _logger;

        // 快取相關常數
        private const int CacheExpirySeconds = 60;
        private const string CacheKeyPrefix = "MembershipLevels";

        public MembershipLevelPublicService(
            AppDbContext context, 
            IMemoryCache cache, 
            ILogger<MembershipLevelPublicService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// 取得會員等級清單（分頁）
        /// </summary>
        public async Task<PagedResponseDto<MembershipLevelItemDto>> GetMembershipLevelsAsync(
            bool activeOnly = true, 
            bool includeDescription = false, 
            bool includeMonthlyCoupon = false, 
            int page = 1, 
            int pageSize = 100)
        {
            try
            {
                // 參數驗證與正規化
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 500); // 嚴格限制上限 500

                _logger.LogInformation("取得會員等級清單：activeOnly={ActiveOnly}, includeDescription={IncludeDescription}, includeMonthlyCoupon={IncludeMonthlyCoupon}, page={Page}, pageSize={PageSize}",
                    activeOnly, includeDescription, includeMonthlyCoupon, page, pageSize);

                // ?? 建立快取鍵
                var cacheKey = $"{CacheKeyPrefix}_List_{activeOnly}_{includeDescription}_{includeMonthlyCoupon}_{page}_{pageSize}";

                // 嘗試從快取取得
                if (_cache.TryGetValue(cacheKey, out PagedResponseDto<MembershipLevelItemDto>? cachedResult) && cachedResult != null)
                {
                    _logger.LogDebug("從快取取得等級清單：{CacheKey}", cacheKey);
                    return cachedResult;
                }

                // 建立查詢
                var query = _context.MembershipLevels.AsNoTracking();

                // activeOnly 篩選
                if (activeOnly)
                {
                    query = query.Where(ml => ml.IsActive);
                }

                // 計算總數
                var totalCount = await query.CountAsync();

                // 排序：Required_Amount ASC，其次 Id ASC
                var levels = await query
                    .OrderBy(ml => ml.RequiredAmount)
                    .ThenBy(ml => ml.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 轉換為 DTO
                var items = levels.Select(level => new MembershipLevelItemDto
                {
                    Id = level.Id,
                    LevelName = level.LevelName,
                    RequiredAmount = level.RequiredAmount,
                    IsActive = level.IsActive,
                    Description = includeDescription ? level.Description : null,
                    MonthlyCouponId = includeMonthlyCoupon ? level.MonthlyCouponId : null
                }).ToList();

                var result = new PagedResponseDto<MembershipLevelItemDto>
                {
                    Success = true,
                    Message = "取得會員等級清單成功",
                    Data = items,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                // ?? 存入快取
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("等級清單已存入快取：{CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級清單失敗");
                return new PagedResponseDto<MembershipLevelItemDto>
                {
                    Success = false,
                    Message = "取得等級清單失敗：" + ex.Message,
                    Data = new List<MembershipLevelItemDto>(),
                    TotalCount = 0,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = 0
                };
            }
        }

        /// <summary>
        /// 取得會員等級統計資訊
        /// </summary>
        public async Task<MembershipLevelsStatsDto> GetMembershipLevelsStatsAsync(bool activeOnly = true)
        {
            try
            {
                _logger.LogInformation("取得會員等級統計：activeOnly={ActiveOnly}", activeOnly);

                // ?? 建立快取鍵
                var cacheKey = $"{CacheKeyPrefix}_Stats_{activeOnly}";

                // 嘗試從快取取得
                if (_cache.TryGetValue(cacheKey, out MembershipLevelsStatsDto? cachedStats) && cachedStats != null)
                {
                    _logger.LogDebug("從快取取得等級統計：{CacheKey}", cacheKey);
                    return cachedStats;
                }

                // 查詢所有等級（用於計算總數）
                var allLevels = await _context.MembershipLevels
                    .AsNoTracking()
                    .ToListAsync();

                if (!allLevels.Any())
                {
                    return new MembershipLevelsStatsDto
                    {
                        TotalLevels = 0,
                        ActiveLevels = 0,
                        InactiveLevels = 0,
                        MinRequiredAmount = 0,
                        MaxRequiredAmount = 0
                    };
                }

                // 篩選目標等級（根據 activeOnly）
                var targetLevels = activeOnly 
                    ? allLevels.Where(ml => ml.IsActive).ToList()
                    : allLevels;

                var stats = new MembershipLevelsStatsDto
                {
                    TotalLevels = allLevels.Count,
                    ActiveLevels = allLevels.Count(ml => ml.IsActive),
                    InactiveLevels = allLevels.Count(ml => !ml.IsActive),
                    MinRequiredAmount = targetLevels.Any() ? targetLevels.Min(ml => ml.RequiredAmount) : 0,
                    MaxRequiredAmount = targetLevels.Any() ? targetLevels.Max(ml => ml.RequiredAmount) : 0
                };

                // ?? 存入快取
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, stats, cacheOptions);
                _logger.LogDebug("等級統計已存入快取：{CacheKey}", cacheKey);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級統計失敗");
                throw;
            }
        }

        /// <summary>
        /// 取得單一會員等級詳細資訊
        /// </summary>
        public async Task<MembershipLevelItemDto?> GetMembershipLevelByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return null;
                }

                _logger.LogInformation("取得會員等級詳細資訊：Id={Id}", id);

                // ?? 建立快取鍵
                var cacheKey = $"{CacheKeyPrefix}_Detail_{id}";

                // 嘗試從快取取得
                if (_cache.TryGetValue(cacheKey, out MembershipLevelItemDto? cachedLevel) && cachedLevel != null)
                {
                    _logger.LogDebug("從快取取得等級詳細資訊：{CacheKey}", cacheKey);
                    return cachedLevel;
                }

                var level = await _context.MembershipLevels
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ml => ml.Id == id);

                if (level == null)
                {
                    return null;
                }

                var result = new MembershipLevelItemDto
                {
                    Id = level.Id,
                    LevelName = level.LevelName,
                    RequiredAmount = level.RequiredAmount,
                    IsActive = level.IsActive,
                    Description = level.Description,
                    MonthlyCouponId = level.MonthlyCouponId
                };

                // ?? 存入快取
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("等級詳細資訊已存入快取：{CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級詳細資訊失敗：Id={Id}", id);
                throw;
            }
        }
    }
}