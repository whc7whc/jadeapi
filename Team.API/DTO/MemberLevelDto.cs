using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// 會員等級 Summary DTO
    /// </summary>
    public class MemberLevelSummaryDto
    {
        /// <summary>
        /// 會員ID
        /// </summary>
        public int MemberId { get; set; }

        /// <summary>
        /// 累積消費金額
        /// </summary>
        public int TotalSpent { get; set; }

        /// <summary>
        /// 目前等級資訊
        /// </summary>
        public LevelInfoDto? CurrentLevel { get; set; }

        /// <summary>
        /// 下一等級資訊（若已是最高級則為 null）
        /// </summary>
        public LevelInfoDto? NextLevel { get; set; }

        /// <summary>
        /// 升級進度
        /// </summary>
        public LevelProgressDto Progress { get; set; } = new();

        /// <summary>
        /// 最後更新時間
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 等級資訊 DTO
    /// </summary>
    public class LevelInfoDto
    {
        /// <summary>
        /// 等級ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 等級名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 所需消費金額
        /// </summary>
        public int RequiredAmount { get; set; }

        /// <summary>
        /// 是否為啟用狀態
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 等級描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 每月優惠券ID（選配）
        /// </summary>
        public int? MonthlyCouponId { get; set; }

        /// <summary>
        /// 每月優惠券標題（選配）
        /// </summary>
        public string? MonthlyCouponTitle { get; set; }
    }

    /// <summary>
    /// 等級進度 DTO
    /// </summary>
    public class LevelProgressDto
    {
        /// <summary>
        /// 目前消費金額（相對於目前等級門檻）
        /// </summary>
        public int CurrentAmount { get; set; }

        /// <summary>
        /// 距離下一等級還需要的金額
        /// </summary>
        public int RequiredForNext { get; set; }

        /// <summary>
        /// 進度百分比（0-100）
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// 是否已達最高等級
        /// </summary>
        public bool IsMaxLevel { get; set; }
    }

    /// <summary>
    /// 重新計算結果 DTO
    /// </summary>
    public class RecalculateResultDto : MemberLevelSummaryDto
    {
        /// <summary>
        /// 是否有升級
        /// </summary>
        public bool LevelUp { get; set; }

        /// <summary>
        /// 原等級資訊（如有升級）
        /// </summary>
        public LevelInfoDto? OldLevel { get; set; }

        /// <summary>
        /// 新等級資訊（如有升級）
        /// </summary>
        public LevelInfoDto? NewLevel { get; set; }

        /// <summary>
        /// 重算前的累積消費
        /// </summary>
        public int PreviousTotalSpent { get; set; }

        /// <summary>
        /// 從訂單計算的新累積消費
        /// </summary>
        public int RecalculatedTotalSpent { get; set; }
    }
}