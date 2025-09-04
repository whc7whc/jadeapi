using Microsoft.AspNetCore.Mvc;
using Team.API.Services;

namespace Team.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MemberLevelController : ControllerBase
    {
        private readonly MemberLevelUpgradeService _upgradeService;

        public MemberLevelController(MemberLevelUpgradeService upgradeService)
        {
            _upgradeService = upgradeService;
        }

        /// <summary>
        /// 檢查並升等會員等級
        /// </summary>
        [HttpPost("{memberId}/check-upgrade")]
        public async Task<IActionResult> CheckUpgrade(int memberId, [FromBody] int addSpentAmount = 0)
        {
            var upgraded = await _upgradeService.CheckAndUpgradeMemberLevel(memberId, addSpentAmount);
            return Ok(new { upgraded, message = upgraded ? "會員等級已升等" : "會員等級無變更" });
        }

        /// <summary>
        /// 取得會員等級資訊
        /// </summary>
        [HttpGet("{memberId}/info")]
        public async Task<IActionResult> GetMemberLevelInfo(int memberId)
        {
            var info = await _upgradeService.GetMemberLevelInfo(memberId);
            if (info == null)
                return NotFound("找不到會員資料");

            return Ok(new 
            { 
                levelName = info.Value.LevelName,
                totalSpent = info.Value.TotalSpent,
                nextLevelAmount = info.Value.NextLevelAmount,
                progress = info.Value.NextLevelAmount > 0 ? 
                    (double)info.Value.TotalSpent / info.Value.NextLevelAmount * 100 : 100
            });
        }

        /// <summary>
        /// 修復現有會員的MemberStat資料（管理員用）
        /// </summary>
        [HttpPost("fix-member-stats")]
        public async Task<IActionResult> FixMemberStats()
        {
            var fixedCount = await _upgradeService.FixExistingMembersStats();
            return Ok(new { message = $"已修復 {fixedCount} 個會員的統計資料" });
        }
    }
}