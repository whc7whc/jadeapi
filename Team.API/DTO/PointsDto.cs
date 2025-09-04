using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// 點數餘額查詢回傳 DTO
    /// </summary>
    public class PointsBalanceDto
    {
        public int MemberId { get; set; }
        public int Balance { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// 點數歷史紀錄項目 DTO
    /// </summary>
    public class PointHistoryItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string? Note { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// 點數異動結果 DTO
    /// </summary>
    public class PointsMutationResultDto
    {
        public int MemberId { get; set; }
        public int BeforeBalance { get; set; }
        public int ChangeAmount { get; set; }
        public int AfterBalance { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public string? VerificationCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 加點請求 DTO
    /// </summary>
    public class PointsEarnRequestDto
    {
        [Required(ErrorMessage = "金額為必填")]
        [Range(1, int.MaxValue, ErrorMessage = "金額必須大於 0")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "類型為必填")]
        [RegularExpression("^(earned|adjustment)$", ErrorMessage = "類型只能是 earned 或 adjustment")]
        public string Type { get; set; } = string.Empty;

        public string? Note { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public string? TransactionId { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// 扣點請求 DTO
    /// </summary>
    public class PointsUseRequestDto
    {
        [Required(ErrorMessage = "金額為必填")]
        [Range(1, int.MaxValue, ErrorMessage = "金額必須大於 0")]
        public int Amount { get; set; }

        public string? Note { get; set; }

        public string? TransactionId { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// 回補請求 DTO
    /// </summary>
    public class PointsRefundRequestDto
    {
        [Required(ErrorMessage = "金額為必填")]
        [Range(1, int.MaxValue, ErrorMessage = "金額必須大於 0")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "來源交易ID為必填")]
        public string SourceTransactionId { get; set; } = string.Empty;

        public string? Note { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// 到期扣點請求 DTO
    /// </summary>
    public class PointsExpireRequestDto
    {
        [Required(ErrorMessage = "金額為必填")]
        [Range(1, int.MaxValue, ErrorMessage = "金額必須大於 0")]
        public int Amount { get; set; }

        public string? Note { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// 點數歷史查詢參數 DTO
    /// </summary>
    public class PointsHistoryQueryDto
    {
        /// <summary>
        /// 類型篩選: signin|used|refund|earned|expired|adjustment
        /// </summary>
        [RegularExpression("^(signin|used|refund|earned|expired|adjustment)?$", 
            ErrorMessage = "類型只能是 signin, used, refund, earned, expired, adjustment 或空值")]
        public string? Type { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "每頁筆數必須在 1-100 之間")]
        public int PageSize { get; set; } = 20;
    }

    // ======== 簽到相關 DTO ========

    /// <summary>
    /// 簽到資訊回傳 DTO
    /// </summary>
    public class CheckinInfoDto
    {
        /// <summary>
        /// 會員ID
        /// </summary>
        public int MemberId { get; set; }

        /// <summary>
        /// 今天日期 (YYYY-MM-DD)
        /// </summary>
        public string Today { get; set; } = string.Empty;

        /// <summary>
        /// 今天是否已簽到
        /// </summary>
        public bool SignedToday { get; set; }

        /// <summary>
        /// 連續簽到天數
        /// </summary>
        public int CheckinStreak { get; set; }

        /// <summary>
        /// 今日獎勵 (整數 J幣)
        /// </summary>
        public int TodayReward { get; set; }

        /// <summary>
        /// 伺服器時間
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// 顯示單位
        /// </summary>
        public string Unit { get; set; } = "J幣";

        /// <summary>
        /// 換算比例 (現在固定為 1，不需要縮放)
        /// </summary>
        public int Scale { get; set; } = 1;
    }

    /// <summary>
    /// ?? 修復：簽到執行回傳 DTO - 餘額改為整數
    /// </summary>
    public class CheckinResultDto
    {
        /// <summary>
        /// 會員ID
        /// </summary>
        public int MemberId { get; set; }

        /// <summary>
        /// 今天是否已簽到
        /// </summary>
        public bool SignedToday { get; set; }

        /// <summary>
        /// 連續簽到天數
        /// </summary>
        public int CheckinStreak { get; set; }

        /// <summary>
        /// 本次獎勵 (整數 J幣)
        /// </summary>
        public int Reward { get; set; }

        /// <summary>
        /// ?? 修復：簽到前餘額 (整數值，直接顯示)
        /// </summary>
        public int BeforeBalance { get; set; }

        /// <summary>
        /// ?? 修復：簽到後餘額 (整數值，直接顯示)
        /// </summary>
        public int AfterBalance { get; set; }

        /// <summary>
        /// 驗證碼
        /// </summary>
        public string VerificationCode { get; set; } = string.Empty;

        /// <summary>
        /// 建立時間
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 簽到請求 DTO (為了相容性保留)
    /// </summary>
    public class CheckinRequestDto
    {
        public int MemberId { get; set; }
    }
}