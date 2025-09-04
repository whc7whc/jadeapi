using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.DTO;

namespace Team.API.Services
{
    /// <summary>
    /// �|�����ŪA�Ȥ���
    /// </summary>
    public interface IMemberLevelService
    {
        /// <summary>
        /// ���o�|������ Summary
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <returns>�|�����źK�n</returns>
        Task<MemberLevelSummaryDto?> GetMemberLevelSummaryAsync(int memberId);

        /// <summary>
        /// ���s�p��|���ֿn���O�æP�B����
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <returns>���s�p�⵲�G</returns>
        Task<RecalculateResultDto?> RecalculateMemberLevelAsync(int memberId);
    }

    /// <summary>
    /// �|�����ŪA�ȹ�@
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
        /// ���o�|������ Summary
        /// </summary>
        public async Task<MemberLevelSummaryDto?> GetMemberLevelSummaryAsync(int memberId)
        {
            try
            {
                // ���o�|���έp���
                var memberStat = await _context.MemberStats
                    .Include(ms => ms.CurrentLevel)
                        .ThenInclude(cl => cl.MonthlyCoupon)
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    _logger.LogWarning("�䤣��|�� {MemberId} ���έp���", memberId);
                    return null;
                }

                var totalSpent = memberStat.TotalSpent;

                // ���o�ثe���š]�ھڲֿn���O���s�p��H�T�O���T�ʡ^
                var currentLevel = await GetLevelBySpentAmountAsync(totalSpent);
                
                // ���o�U�@����
                var nextLevel = currentLevel != null ? await GetNextLevelAsync(currentLevel.RequiredAmount) : null;

                // �p��i��
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
                _logger.LogError(ex, "���o�|�� {MemberId} ���źK�n����", memberId);
                throw;
            }
        }

        /// <summary>
        /// ���s�p��|���ֿn���O�æP�B����
        /// </summary>
        public async Task<RecalculateResultDto?> RecalculateMemberLevelAsync(int memberId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // ����w Member_Stats �O���קK�v�A����
                var memberStat = await _context.MemberStats
                    .Include(ms => ms.CurrentLevel)
                        .ThenInclude(cl => cl.MonthlyCoupon)
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    _logger.LogWarning("�䤣��|�� {MemberId} ���έp���", memberId);
                    return null;
                }

                var previousTotalSpent = memberStat.TotalSpent;
                var previousLevel = memberStat.CurrentLevel;

                // �q Orders ���s�p��ֿn���O�]�u�p��w�I��/�w�������q��^
                var recalculatedSpent = await CalculateTotalSpentFromOrdersAsync(memberId);
                
                // �ھڷs���ֿn���O�P�w���ݵ���
                var newLevel = await GetLevelBySpentAmountAsync(recalculatedSpent);
                
                // ��s Member_Stats
                memberStat.TotalSpent = recalculatedSpent;
                memberStat.CurrentLevelId = newLevel?.Id;
                memberStat.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // �P�_�O�_���ɯ�
                bool levelUp = previousLevel?.Id != newLevel?.Id;

                // ���o�U�@����
                var nextLevel = newLevel != null ? await GetNextLevelAsync(newLevel.RequiredAmount) : null;

                // �p��i��
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

                _logger.LogInformation("�|�� {MemberId} ���ŭ��⧹���G{PreviousSpent} -> {NewSpent}�A�����ܤơG{LevelUp}", 
                    memberId, previousTotalSpent, recalculatedSpent, levelUp);

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "���s�p��|�� {MemberId} ���ť���", memberId);
                throw;
            }
        }

        /// <summary>
        /// �ھڮ��O���B���o��������
        /// �ӷ~�W�h�G�� Is_Active=1 ���̰����e ? totalSpent
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
        /// ���o�U�@����
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
        /// �q�q��p��ֿn���O
        /// �u�p��u���ĭq��v���B�]paid|completed ���A�^
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
        /// �p��ɯŶi��
        /// </summary>
        private LevelProgressDto CalculateProgress(int totalSpent, MembershipLevel? currentLevel, MembershipLevel? nextLevel)
        {
            if (nextLevel == null)
            {
                // �w�O�̰���
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
        /// �N MembershipLevel �����ഫ�� LevelInfoDto
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