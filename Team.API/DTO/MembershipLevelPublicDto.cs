using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// 穦单兜ヘ DTO
    /// </summary>
    public class MembershipLevelItemDto
    {
        /// <summary>
        /// 单ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 单嘿
        /// </summary>
        public string LevelName { get; set; } = string.Empty;

        /// <summary>
        /// ┮惠禣肂
        /// </summary>
        public int RequiredAmount { get; set; }

        /// <summary>
        /// 琌币ノ
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 单磞瓃匡皌逆
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// –る皌ㄩID匡皌逆
        /// </summary>
        public int? MonthlyCouponId { get; set; }
    }

    /// <summary>
    /// 穦单参璸 DTO
    /// </summary>
    public class MembershipLevelsStatsDto
    {
        /// <summary>
        /// 羆单计
        /// </summary>
        public int TotalLevels { get; set; }

        /// <summary>
        /// 币ノ单计
        /// </summary>
        public int ActiveLevels { get; set; }

        /// <summary>
        /// 氨ノ单计
        /// </summary>
        public int InactiveLevels { get; set; }

        /// <summary>
        /// 程耬肂
        /// </summary>
        public int MinRequiredAmount { get; set; }

        /// <summary>
        /// 程蔼耬肂
        /// </summary>
        public int MaxRequiredAmount { get; set; }
    }
}