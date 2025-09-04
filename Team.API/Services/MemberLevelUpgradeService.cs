using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

namespace Team.API.Services
{
    public class MemberLevelUpgradeService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MemberLevelUpgradeService> _logger;

        public MemberLevelUpgradeService(AppDbContext context, ILogger<MemberLevelUpgradeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 檢查並更新會員等級（在消費後調用）
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="addSpentAmount">本次消費金額</param>
        /// <returns>是否升等</returns>
        public async Task<bool> CheckAndUpgradeMemberLevel(int memberId, int addSpentAmount = 0)
        {
            try
            {
                // 取得或創建會員統計資料
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // 如果沒有MemberStat記錄，創建一個
                    memberStat = new MemberStat
                    {
                        MemberId = memberId,
                        CurrentLevelId = 1,
                        TotalSpent = addSpentAmount,
                        TotalPoints = 0,
                        UpdatedAt = DateTime.Now
                    };
                    _context.MemberStats.Add(memberStat);
                }
                else
                {
                    // 更新總消費金額
                    memberStat.TotalSpent += addSpentAmount;
                    memberStat.UpdatedAt = DateTime.Now;
                }

                // 取得所有等級（按需要金額排序）
                var levels = await _context.MembershipLevels
                    .Where(ml => ml.IsActive)
                    .OrderByDescending(ml => ml.RequiredAmount)
                    .ToListAsync();

                // 找到符合條件的最高等級
                var newLevel = levels.FirstOrDefault(l => memberStat.TotalSpent >= l.RequiredAmount);
                
                if (newLevel == null)
                {
                    // 如果沒有符合條件的等級，使用最低等級（ID=1）
                    newLevel = levels.OrderBy(l => l.RequiredAmount).FirstOrDefault();
                    if (newLevel == null)
                    {
                        _logger.LogWarning("找不到任何會員等級！");
                        return false;
                    }
                }

                bool wasUpgraded = false;
                var oldLevelId = memberStat.CurrentLevelId;

                // 檢查是否需要升等
                if (newLevel.Id != memberStat.CurrentLevelId)
                {
                    memberStat.CurrentLevelId = newLevel.Id;
                    
                    // 同時更新Member表的Level欄位（保持一致性）
                    var member = await _context.Members.FindAsync(memberId);
                    if (member != null)
                    {
                        member.Level = newLevel.Id;
                        member.UpdatedAt = DateTime.Now;
                    }
                    
                    wasUpgraded = true;
                    _logger.LogInformation("會員 {MemberId} 從等級 {OldLevel} 升等到 {NewLevel}，總消費：${TotalSpent}", 
                        memberId, oldLevelId, newLevel.Id, memberStat.TotalSpent);
                }

                await _context.SaveChangesAsync();
                return wasUpgraded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查會員 {MemberId} 等級升等時發生錯誤", memberId);
                return false;
            }
        }

        /// <summary>
        /// 取得會員當前等級資訊
        /// </summary>
        public async Task<(string LevelName, int TotalSpent, int NextLevelAmount)?> GetMemberLevelInfo(int memberId)
        {
            try
            {
                var memberStat = await _context.MemberStats
                    .Include(ms => ms.CurrentLevel)
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                    return null;

                // 找下一等級需要的金額
                var nextLevel = await _context.MembershipLevels
                    .Where(ml => ml.IsActive && ml.RequiredAmount > memberStat.TotalSpent)
                    .OrderBy(ml => ml.RequiredAmount)
                    .FirstOrDefaultAsync();

                return (
                    memberStat.CurrentLevel?.LevelName ?? "未知等級",
                    memberStat.TotalSpent,
                    nextLevel?.RequiredAmount ?? 0
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員 {MemberId} 等級資訊時發生錯誤", memberId);
                return null;
            }
        }

        /// <summary>
        /// 修復現有會員的MemberStat資料（一次性修復）
        /// </summary>
        public async Task<int> FixExistingMembersStats()
        {
            try
            {
                var membersWithoutStats = await _context.Members
                    .Where(m => !_context.MemberStats.Any(ms => ms.MemberId == m.Id))
                    .ToListAsync();

                int fixedCount = 0;
                foreach (var member in membersWithoutStats)
                {
                    var memberStat = new MemberStat
                    {
                        MemberId = member.Id,
                        CurrentLevelId = member.Level > 0 ? member.Level : 1,
                        TotalSpent = 0, // 如果有訂單資料可以從這裡計算
                        TotalPoints = 0,
                        UpdatedAt = DateTime.Now
                    };
                    _context.MemberStats.Add(memberStat);
                    fixedCount++;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("修復了 {Count} 個會員的MemberStat資料", fixedCount);
                return fixedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修復會員統計資料時發生錯誤");
                return 0;
            }
        }
    }
}