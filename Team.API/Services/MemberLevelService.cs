using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.DTO;

namespace Team.API.Services
{
    /// <summary>
    /// 會員等級服務介面
    /// </summary>
    public interface IMemberLevelService
    {
        /// <summary>
        /// 取得會員等級 Summary
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>會員等級摘要</returns>
        Task<MemberLevelSummaryDto?> GetMemberLevelSummaryAsync(int memberId);

        /// <summary>
        /// 重新計算會員累積消費並同步等級
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>重新計算結果</returns>
        Task<RecalculateResultDto?> RecalculateMemberLevelAsync(int memberId);
    }

    /// <summary>
    /// 會員等級服務實作
    /// </summary>
    public class MemberLevelService : IMemberLevelService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MemberLevelService> _logger;

        public MemberLevelService(AppDbContext context, ILogger<MemberLevelService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 取得會員等級 Summary
        /// </summary>
        public async Task<MemberLevelSummaryDto?> GetMemberLevelSummaryAsync(int memberId)
        {
            try
            {
                // 取得會員統計資料
                var memberStat = await _context.MemberStats
                    .Include(ms => ms.CurrentLevel)
                        .ThenInclude(cl => cl.MonthlyCoupon)
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    _logger.LogWarning("找不到會員 {MemberId} 的統計資料", memberId);
                    return null;
                }

                var totalSpent = memberStat.TotalSpent;

                // 取得目前等級（根據累積消費重新計算以確保正確性）
                var currentLevel = await GetLevelBySpentAmountAsync(totalSpent);
                
                // 取得下一等級
                var nextLevel = currentLevel != null ? await GetNextLevelAsync(currentLevel.RequiredAmount) : null;

                // 計算進度
                var progress = CalculateProgress(totalSpent, currentLevel, nextLevel);

                return new MemberLevelSummaryDto
                {
                    MemberId = memberId,
                    TotalSpent = totalSpent,
                    CurrentLevel = currentLevel != null ? MapToLevelInfoDto(currentLevel) : null,
                    NextLevel = nextLevel != null ? MapToLevelInfoDto(nextLevel) : null,
                    Progress = progress,
                    UpdatedAt = memberStat.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員 {MemberId} 等級摘要失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 重新計算會員累積消費並同步等級
        /// </summary>
        public async Task<RecalculateResultDto?> RecalculateMemberLevelAsync(int memberId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // 先鎖定 Member_Stats 記錄避免競態條件
                var memberStat = await _context.MemberStats
                    .Include(ms => ms.CurrentLevel)
                        .ThenInclude(cl => cl.MonthlyCoupon)
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    _logger.LogWarning("找不到會員 {MemberId} 的統計資料", memberId);
                    return null;
                }

                var previousTotalSpent = memberStat.TotalSpent;
                var previousLevel = memberStat.CurrentLevel;

                // 從 Orders 重新計算累積消費（只計算已付款/已完成的訂單）
                var recalculatedSpent = await CalculateTotalSpentFromOrdersAsync(memberId);
                
                // 根據新的累積消費判定應屬等級
                var newLevel = await GetLevelBySpentAmountAsync(recalculatedSpent);
                
                // 更新 Member_Stats
                memberStat.TotalSpent = recalculatedSpent;
                memberStat.CurrentLevelId = newLevel?.Id;
                memberStat.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 判斷是否有升級
                bool levelUp = previousLevel?.Id != newLevel?.Id;

                // 取得下一等級
                var nextLevel = newLevel != null ? await GetNextLevelAsync(newLevel.RequiredAmount) : null;

                // 計算進度
                var progress = CalculateProgress(recalculatedSpent, newLevel, nextLevel);

                var result = new RecalculateResultDto
                {
                    MemberId = memberId,
                    TotalSpent = recalculatedSpent,
                    CurrentLevel = newLevel != null ? MapToLevelInfoDto(newLevel) : null,
                    NextLevel = nextLevel != null ? MapToLevelInfoDto(nextLevel) : null,
                    Progress = progress,
                    UpdatedAt = memberStat.UpdatedAt,
                    LevelUp = levelUp,
                    OldLevel = previousLevel != null ? MapToLevelInfoDto(previousLevel) : null,
                    NewLevel = levelUp && newLevel != null ? MapToLevelInfoDto(newLevel) : null,
                    PreviousTotalSpent = previousTotalSpent,
                    RecalculatedTotalSpent = recalculatedSpent
                };

                _logger.LogInformation("會員 {MemberId} 等級重算完成：{PreviousSpent} -> {NewSpent}，等級變化：{LevelUp}", 
                    memberId, previousTotalSpent, recalculatedSpent, levelUp);

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "重新計算會員 {MemberId} 等級失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 根據消費金額取得對應等級
        /// 商業規則：取 Is_Active=1 的最高門檻 ? totalSpent
        /// </summary>
        private async Task<MembershipLevel?> GetLevelBySpentAmountAsync(int totalSpent)
        {
            return await _context.MembershipLevels
                .Include(ml => ml.MonthlyCoupon)
                .Where(ml => ml.IsActive && ml.RequiredAmount <= totalSpent)
                .OrderByDescending(ml => ml.RequiredAmount)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// 取得下一等級
        /// </summary>
        private async Task<MembershipLevel?> GetNextLevelAsync(int currentRequiredAmount)
        {
            return await _context.MembershipLevels
                .Include(ml => ml.MonthlyCoupon)
                .Where(ml => ml.IsActive && ml.RequiredAmount > currentRequiredAmount)
                .OrderBy(ml => ml.RequiredAmount)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// 從訂單計算累積消費
        /// 只計算「有效訂單」金額（paid|completed 狀態）
        /// </summary>
        private async Task<int> CalculateTotalSpentFromOrdersAsync(int memberId)
        {
            var validStatuses = new[] { "paid", "completed" };
            
            var totalSpent = await _context.Orders
                .Where(o => o.MemberId == memberId && validStatuses.Contains(o.OrderStatus.ToLower()))
                .SumAsync(o => (int)o.TotalAmount);

            return totalSpent;
        }

        /// <summary>
        /// 計算升級進度
        /// </summary>
        private LevelProgressDto CalculateProgress(int totalSpent, MembershipLevel? currentLevel, MembershipLevel? nextLevel)
        {
            if (nextLevel == null)
            {
                // 已是最高級
                return new LevelProgressDto
                {
                    CurrentAmount = totalSpent,
                    RequiredForNext = 0,
                    Percentage = 100,
                    IsMaxLevel = true
                };
            }

            var currentLevelAmount = currentLevel?.RequiredAmount ?? 0;
            var nextLevelAmount = nextLevel.RequiredAmount;
            var progressAmount = totalSpent - currentLevelAmount;
            var totalNeeded = nextLevelAmount - currentLevelAmount;
            
            var percentage = totalNeeded > 0 
                ? Math.Min(100, Math.Max(0, (int)Math.Round((double)progressAmount / totalNeeded * 100)))
                : 0;

            return new LevelProgressDto
            {
                CurrentAmount = progressAmount,
                RequiredForNext = Math.Max(0, nextLevelAmount - totalSpent),
                Percentage = percentage,
                IsMaxLevel = false
            };
        }

        /// <summary>
        /// 將 MembershipLevel 實體轉換為 LevelInfoDto
        /// </summary>
        private LevelInfoDto MapToLevelInfoDto(MembershipLevel level)
        {
            return new LevelInfoDto
            {
                Id = level.Id,
                Name = level.LevelName,
                RequiredAmount = level.RequiredAmount,
                IsActive = level.IsActive,
                Description = level.Description,
                MonthlyCouponId = level.MonthlyCouponId,
                MonthlyCouponTitle = level.MonthlyCoupon?.Title
            };
        }
    }
}