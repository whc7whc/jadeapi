using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// 會員持有優惠券 DTO - 扁平化設計，包含會員持有和優惠券定義資訊
    /// </summary>
    public class MyMemberCouponDto
    {
        // 會員持有層（Member_Coupons）
        public int MemberCouponId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public int? OrderId { get; set; }
        public string VerificationCode { get; set; } = string.Empty;

        // 券定義層（Coupons）
        public int CouponId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public int DiscountAmount { get; set; }
        public int? MinSpend { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public bool IsActive { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public int? SellersId { get; set; }
        public int? CategoryId { get; set; }
        public int? ApplicableLevelId { get; set; }

        // 衍生欄位
        public string Source => SellersId.HasValue && SellersId > 0 ? "seller" : "platform";
        public string? SellerName { get; set; }

        // 格式化顯示欄位
        public string FormattedDiscount => DiscountType?.ToLower() switch
        {
            "%數折扣" or "percentage" => $"{DiscountAmount}% 折扣",
            "j幣回饋" or "點數返還" => $"{DiscountAmount} J幣回饋",
            "滿減" => $"滿 ${MinSpend} 減 ${DiscountAmount}",
            _ => $"{DiscountAmount}"
        };

        public string ValidityPeriod => $"{StartAt:yyyy-MM-dd} ~ {ExpiredAt:yyyy-MM-dd}";
        
        public string UsageInfo => UsageLimit.HasValue 
            ? $"{UsedCount}/{UsageLimit}" 
            : $"{UsedCount}/無限";

        public bool IsCurrentlyActive => Status == "active";
    }

    /// <summary>
    /// 會員優惠券查詢參數 DTO
    /// </summary>
    public class MemberCouponQueryDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "會員ID必須大於 0")]
        public int MemberId { get; set; }

        /// <summary>
        /// 是否只回「目前可用」的持有券
        /// </summary>
        public bool ActiveOnly { get; set; } = false;

        /// <summary>
        /// 狀態篩選: active|used|expired|cancelled
        /// </summary>
        public string Status { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "每頁筆數必須在 1-100 之間")]
        public int PageSize { get; set; } = 20;
    }
}