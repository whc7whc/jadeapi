using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Team.API.Models.EfModel;
using Team.API.DTO;
using Team.API.Models.DTOs;

namespace Team.API.Services
{
    /// <summary>
    /// Membership level public query service interface
    /// </summary>
    public interface IMembershipLevelPublicService
    {
        /// <summary>
        /// Get membership levels list (paged)
        /// </summary>
        /// <param name="activeOnly">Whether to return only active levels</param>
        /// <param name="includeDescription">Whether to include description</param>
        /// <param name="includeMonthlyCoupon">Whether to include monthly coupon ID</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>Paginated membership levels list</returns>
        Task<PagedResponseDto<MembershipLevelItemDto>> GetMembershipLevelsAsync(
            bool activeOnly = true, 
            bool includeDescription = false, 
            bool includeMonthlyCoupon = false, 
            int page = 1, 
            int pageSize = 100);

        /// <summary>
        /// Get membership levels statistics
        /// </summary>
        /// <param name="activeOnly">Whether to calculate only active levels</param>
        /// <returns>Membership levels statistics</returns>
        Task<MembershipLevelsStatsDto> GetMembershipLevelsStatsAsync(bool activeOnly = true);

        /// <summary>
        /// Get specific membership level details
        /// </summary>
        /// <param name="id">Level ID</param>
        /// <returns>Membership level details</returns>
        Task<MembershipLevelItemDto?> GetMembershipLevelByIdAsync(int id);
    }

    /// <summary>
    /// Membership level public query service implementation
    /// 
    /// Cache strategy:
    /// - Levels list: 60 seconds cache
    /// - Statistics: 60 seconds cache
    /// - Cache key: parameter combination + timestamp
    /// - Cache invalidation: automatically cleared on data changes, or set expiration time
    /// </summary>
    public class MembershipLevelPublicService : IMembershipLevelPublicService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MembershipLevelPublicService> _logger;

        // Cache configuration constants
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
        /// Get membership levels list (paged)
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
                // Parameter validation and normalization
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 500); // Maximum 500 for performance

                _logger.LogInformation("Getting membership levels: activeOnly={ActiveOnly}, includeDescription={IncludeDescription}, includeMonthlyCoupon={IncludeMonthlyCoupon}, page={Page}, pageSize={PageSize}",
                    activeOnly, includeDescription, includeMonthlyCoupon, page, pageSize);

                // Build cache key
                var cacheKey = $"{CacheKeyPrefix}_List_{activeOnly}_{includeDescription}_{includeMonthlyCoupon}_{page}_{pageSize}";

                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out PagedResponseDto<MembershipLevelItemDto>? cachedResult) && cachedResult != null)
                {
                    _logger.LogDebug("Retrieved membership levels from cache: {CacheKey}", cacheKey);
                    return cachedResult;
                }

                // Build query
                var query = _context.MembershipLevels.AsNoTracking();

                // activeOnly filter
                if (activeOnly)
                {
                    query = query.Where(ml => ml.IsActive);
                }

                // Calculate total count
                var totalCount = await query.CountAsync();

                // Sorting: Required_Amount ASC, then Id ASC
                var levels = await query
                    .OrderBy(ml => ml.RequiredAmount)
                    .ThenBy(ml => ml.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Convert to DTO
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
                    Message = "Membership levels retrieved successfully",
                    Data = items,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                // Store in cache
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("Membership levels stored in cache: {CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get membership levels");
                return new PagedResponseDto<MembershipLevelItemDto>
                {
                    Success = false,
                    Message = "Failed to get membership levels: " + ex.Message,
                    Data = new List<MembershipLevelItemDto>(),
                    TotalCount = 0,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = 0
                };
            }
        }

        /// <summary>
        /// Get membership levels statistics
        /// </summary>
        public async Task<MembershipLevelsStatsDto> GetMembershipLevelsStatsAsync(bool activeOnly = true)
        {
            try
            {
                _logger.LogInformation("Getting membership levels statistics: activeOnly={ActiveOnly}", activeOnly);

                // Build cache key
                var cacheKey = $"{CacheKeyPrefix}_Stats_{activeOnly}";

                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out MembershipLevelsStatsDto? cachedStats) && cachedStats != null)
                {
                    _logger.LogDebug("Retrieved membership levels statistics from cache: {CacheKey}", cacheKey);
                    return cachedStats;
                }

                // Query all levels (for calculating totals)
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

                // Filter target levels (based on activeOnly)
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

                // Store in cache
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, stats, cacheOptions);
                _logger.LogDebug("Membership levels statistics stored in cache: {CacheKey}", cacheKey);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get membership levels statistics");
                throw;
            }
        }

        /// <summary>
        /// Get specific membership level details
        /// </summary>
        public async Task<MembershipLevelItemDto?> GetMembershipLevelByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return null;
                }

                _logger.LogInformation("Getting membership level details: Id={Id}", id);

                // Build cache key
                var cacheKey = $"{CacheKeyPrefix}_Detail_{id}";

                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out MembershipLevelItemDto? cachedLevel) && cachedLevel != null)
                {
                    _logger.LogDebug("Retrieved membership level details from cache: {CacheKey}", cacheKey);
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

                // Store in cache
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("Membership level details stored in cache: {CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get membership level details: Id={Id}", id);
                throw;
            }
        }
    }
}