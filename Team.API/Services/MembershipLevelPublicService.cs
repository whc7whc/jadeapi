using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Team.API.Models.EfModel;
using Team.API.DTO;
using Team.API.Models.DTOs;

namespace Team.API.Services
{
    /// <summary>
    /// �|�����Ť��}�d�ߪA�Ȥ���
    /// </summary>
    public interface IMembershipLevelPublicService
    {
        /// <summary>
        /// ���o�|�����ŲM��]�����^
        /// </summary>
        /// <param name="activeOnly">�O�_�u�^�ҥΤ�������</param>
        /// <param name="includeDescription">�O�_�]�t�y�z���</param>
        /// <param name="includeMonthlyCoupon">�O�_�]�t�C��t��ID</param>
        /// <param name="page">���X</param>
        /// <param name="pageSize">�C������</param>
        /// <returns>���������ŲM��</returns>
        Task<PagedResponseDto<MembershipLevelItemDto>> GetMembershipLevelsAsync(
            bool activeOnly = true, 
            bool includeDescription = false, 
            bool includeMonthlyCoupon = false, 
            int page = 1, 
            int pageSize = 100);

        /// <summary>
        /// ���o�|�����Ųέp��T
        /// </summary>
        /// <param name="activeOnly">�O�_�u�έp�ҥΤ�������</param>
        /// <returns>���Ųέp��T</returns>
        Task<MembershipLevelsStatsDto> GetMembershipLevelsStatsAsync(bool activeOnly = true);

        /// <summary>
        /// ���o��@�|�����ŸԲӸ�T
        /// </summary>
        /// <param name="id">����ID</param>
        /// <returns>���ŸԲӸ�T</returns>
        Task<MembershipLevelItemDto?> GetMembershipLevelByIdAsync(int id);
    }

    /// <summary>
    /// �|�����Ť��}�d�ߪA�ȹ�@
    /// 
    /// ?? �֨������G
    /// - ���ŦC��G60��O����֨�
    /// - �έp��ơG60��O����֨�
    /// - �֨���G�ѼƲզX + �ɶ��W
    /// - ���ı���G��Ʋ��ʮɦ۰ʥ��ġA�γ]�w�ɶ����
    /// </summary>
    public class MembershipLevelPublicService : IMembershipLevelPublicService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MembershipLevelPublicService> _logger;

        // �֨������`��
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
        /// ���o�|�����ŲM��]�����^
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
                // �Ѽ����һP���W��
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 500); // �Y�歭��W�� 500

                _logger.LogInformation("���o�|�����ŲM��GactiveOnly={ActiveOnly}, includeDescription={IncludeDescription}, includeMonthlyCoupon={IncludeMonthlyCoupon}, page={Page}, pageSize={PageSize}",
                    activeOnly, includeDescription, includeMonthlyCoupon, page, pageSize);

                // ?? �إߧ֨���
                var cacheKey = $"{CacheKeyPrefix}_List_{activeOnly}_{includeDescription}_{includeMonthlyCoupon}_{page}_{pageSize}";

                // ���ձq�֨����o
                if (_cache.TryGetValue(cacheKey, out PagedResponseDto<MembershipLevelItemDto>? cachedResult) && cachedResult != null)
                {
                    _logger.LogDebug("�q�֨����o���ŲM��G{CacheKey}", cacheKey);
                    return cachedResult;
                }

                // �إ߬d��
                var query = _context.MembershipLevels.AsNoTracking();

                // activeOnly �z��
                if (activeOnly)
                {
                    query = query.Where(ml => ml.IsActive);
                }

                // �p���`��
                var totalCount = await query.CountAsync();

                // �ƧǡGRequired_Amount ASC�A�䦸 Id ASC
                var levels = await query
                    .OrderBy(ml => ml.RequiredAmount)
                    .ThenBy(ml => ml.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // �ഫ�� DTO
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
                    Message = "���o�|�����ŲM�榨�\",
                    Data = items,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                // ?? �s�J�֨�
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("���ŲM��w�s�J�֨��G{CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���o�|�����ŲM�楢��");
                return new PagedResponseDto<MembershipLevelItemDto>
                {
                    Success = false,
                    Message = "���o���ŲM�楢�ѡG" + ex.Message,
                    Data = new List<MembershipLevelItemDto>(),
                    TotalCount = 0,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = 0
                };
            }
        }

        /// <summary>
        /// ���o�|�����Ųέp��T
        /// </summary>
        public async Task<MembershipLevelsStatsDto> GetMembershipLevelsStatsAsync(bool activeOnly = true)
        {
            try
            {
                _logger.LogInformation("���o�|�����Ųέp�GactiveOnly={ActiveOnly}", activeOnly);

                // ?? �إߧ֨���
                var cacheKey = $"{CacheKeyPrefix}_Stats_{activeOnly}";

                // ���ձq�֨����o
                if (_cache.TryGetValue(cacheKey, out MembershipLevelsStatsDto? cachedStats) && cachedStats != null)
                {
                    _logger.LogDebug("�q�֨����o���Ųέp�G{CacheKey}", cacheKey);
                    return cachedStats;
                }

                // �d�ߩҦ����š]�Ω�p���`�ơ^
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

                // �z��ؼе��š]�ھ� activeOnly�^
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

                // ?? �s�J�֨�
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, stats, cacheOptions);
                _logger.LogDebug("���Ųέp�w�s�J�֨��G{CacheKey}", cacheKey);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���o�|�����Ųέp����");
                throw;
            }
        }

        /// <summary>
        /// ���o��@�|�����ŸԲӸ�T
        /// </summary>
        public async Task<MembershipLevelItemDto?> GetMembershipLevelByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return null;
                }

                _logger.LogInformation("���o�|�����ŸԲӸ�T�GId={Id}", id);

                // ?? �إߧ֨���
                var cacheKey = $"{CacheKeyPrefix}_Detail_{id}";

                // ���ձq�֨����o
                if (_cache.TryGetValue(cacheKey, out MembershipLevelItemDto? cachedLevel) && cachedLevel != null)
                {
                    _logger.LogDebug("�q�֨����o���ŸԲӸ�T�G{CacheKey}", cacheKey);
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

                // ?? �s�J�֨�
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirySeconds),
                    SlidingExpiration = TimeSpan.FromSeconds(CacheExpirySeconds / 2),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("���ŸԲӸ�T�w�s�J�֨��G{CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���o�|�����ŸԲӸ�T���ѡGId={Id}", id);
                throw;
            }
        }
    }
}