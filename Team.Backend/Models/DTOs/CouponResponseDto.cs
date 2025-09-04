using System.ComponentModel.DataAnnotations;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.DTOs
{
    // 查詢回應 DTO
    public class CouponResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public string DiscountTypeLabel { get; set; } = string.Empty;
        public int DiscountAmount { get; set; }
        public int? MinSpend { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public int? ApplicableLevelId { get; set; }
        public int? SellersId { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        // 新增：廠商相關資訊
        public bool IsVendorCoupon => SellersId.HasValue && SellersId > 0;
        public string SellerRealName { get; set; } = string.Empty;
        public string SellerEmail { get; set; } = string.Empty;
        public string SellerStatus { get; set; } = string.Empty;
        public string CouponSource => IsVendorCoupon ? "廠商優惠券" : "平台優惠券";
        public string SourceBadgeClass => IsVendorCoupon ? "warning" : "primary";

        // 格式化顯示
        public string FormattedStartAt => StartAt.ToString("yyyy-MM-dd");
        public string FormattedExpiredAt => ExpiredAt.ToString("yyyy-MM-dd");
        public string FormattedDiscount => GetNormalizedDiscountType() switch
        {
            "%數折扣" => $"{DiscountAmount}% 折扣",
            "J幣回饋" => $"{DiscountAmount} J幣回饋",
            "滿減" => $"滿減 ${DiscountAmount}",
            _ => $"{DiscountAmount}"
        };
        public string ValidPeriod => $"{FormattedStartAt} ~ {FormattedExpiredAt}";
        public string FormattedUsage => UsageLimit.HasValue ? $"{UsedCount}/{UsageLimit}" : $"{UsedCount}/無限";

        // 新增：詳細資訊格式化
        public string DetailedDescription => IsVendorCoupon 
            ? $"廠商【{SellerRealName}】發行的{DiscountTypeLabel}優惠券" 
            : $"平台發行的{DiscountTypeLabel}優惠券";
            
        public string RemainingUsage => UsageLimit.HasValue 
            ? $"剩餘 {Math.Max(0, UsageLimit.Value - UsedCount)} 次"
            : "無限制使用";

        // 將舊的折扣類型標準化為三種
        private string GetNormalizedDiscountType()
        {
            return DiscountType?.ToLower() switch
            {
                "折扣碼" or "percentage" or "%數折扣" => "%數折扣",
                "點數返還" or "j幣回饋" => "J幣回饋", 
                "滿減" => "滿減",
                "免運費" => "滿減", // 免運費歸類為滿減
                _ => "滿減" // 其他類型預設為滿減
            };
        }
    }

    // 新增：詳細檢視DTO
    public class CouponDetailDto : CouponResponseDto
    {
        // 詳細統計資訊
        public int TotalViews { get; set; }
        public int TotalUses { get; set; }
        public decimal? TotalSavings { get; set; }
        public DateTime? LastUsedAt { get; set; }
        
        // 廠商詳細資訊
        public string? SellerIdNumber { get; set; }
        public string? SellerPhone { get; set; }
        public DateTime? SellerJoinDate { get; set; }
        
        // 適用等級資訊
        public string? ApplicableLevelName { get; set; }
        
        
        // 使用記錄摘要
        public List<CouponUsageDto> RecentUsages { get; set; } = new();
        
        // 格式化顯示
        public string FormattedTotalSavings => TotalSavings.HasValue ? $"${TotalSavings:N0}" : "無資料";
        public string FormattedLastUsed => LastUsedAt?.ToString("yyyy-MM-dd HH:mm") ?? "從未使用";
        public string FormattedSellerJoinDate => SellerJoinDate?.ToString("yyyy-MM-dd") ?? "";
    }

    // 使用記錄DTO
    public class CouponUsageDto
    {
        public int Id { get; set; }
        public string MemberEmail { get; set; } = string.Empty;
        public DateTime UsedAt { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        
        public string FormattedUsedAt => UsedAt.ToString("yyyy-MM-dd HH:mm");
        public string FormattedOrderAmount => $"${OrderAmount:N0}";
        public string FormattedDiscountAmount => $"${DiscountAmount:N0}";
    }

    // 查詢請求 DTO
    public class CouponQueryDto
    {
        public string Search { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        
        // 新增：廠商篩選
        public string CouponSource { get; set; } = ""; // "vendor", "platform", ""
        public int? SellerId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "每頁筆數必須在 1-100 之間")]
        public int ItemsPerPage { get; set; } = 10;

        public string SortBy { get; set; } = "StartAt";

        [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "排序方向只能是 asc 或 desc")]
        public string SortDirection { get; set; } = "desc";
    }

    // 分頁回應 DTO
    public class PagedCouponResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "操作成功";
        public IEnumerable<T> Data { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int ItemsPerPage { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    // 創建優惠券請求 DTO
    public class CreateCouponDto
    {
        [Required(ErrorMessage = "優惠券名稱不能為空")]
        [StringLength(100, ErrorMessage = "優惠券名稱長度不能超過 100 個字元")]
        public string Title { get; set; }

        [Required(ErrorMessage = "折扣類型不能為空")]
        [StringLength(20, ErrorMessage = "折扣類型長度不能超過 20 個字元")]
        public string DiscountType { get; set; }

        [Required(ErrorMessage = "折扣金額不能為空")]
        [Range(1, int.MaxValue, ErrorMessage = "折扣金額必須大於 0")]
        public int DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "最低消費金額不能為負數")]
        public int? MinSpend { get; set; }

        [Required(ErrorMessage = "開始時間不能為空")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "結束時間不能為空")]
        public DateTime ExpiredAt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "使用上限必須大於 0")]
        public int? UsageLimit { get; set; }

        public int? ApplicableLevelId { get; set; }
        public int? SellersId { get; set; }
    }

    // 更新優惠券請求 DTO
    public class UpdateCouponDto
    {
        [Required(ErrorMessage = "優惠券名稱不能為空")]
        [StringLength(100, ErrorMessage = "優惠券名稱長度不能超過 100 個字元")]
        public string Title { get; set; }

        [Required(ErrorMessage = "折扣類型不能為空")]
        [StringLength(20, ErrorMessage = "折扣類型長度不能超過 20 個字元")]
        public string DiscountType { get; set; }

        [Required(ErrorMessage = "折扣金額不能為空")]
        [Range(1, int.MaxValue, ErrorMessage = "折扣金額必須大於 0")]
        public int DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "最低消費金額不能為負數")]
        public int? MinSpend { get; set; }

        [Required(ErrorMessage = "開始時間不能為空")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "結束時間不能為空")]
        public DateTime ExpiredAt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "使用上限必須大於 0")]
        public int? UsageLimit { get; set; }

        public int? ApplicableLevelId { get; set; }
        public int? SellersId { get; set; }
    }

    // 統計資料 DTO
    public class CouponStatsDto
    {
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int ExpiredCount { get; set; }
        public int UsedCount { get; set; }
        
        // 新增：來源統計
        public int VendorCouponCount { get; set; }
        public int PlatformCouponCount { get; set; }
        
        public Dictionary<string, int> TypeStats { get; set; } = new();
        public Dictionary<string, int> StatusStats { get; set; } = new();
        public Dictionary<string, int> SourceStats { get; set; } = new();
    }

    // 批量操作 DTO
    public class BatchCouponDeleteDto
    {
        [Required(ErrorMessage = "請選擇要刪除的項目")]
        [MinLength(1, ErrorMessage = "至少需要選擇一個項目")]
        public int[] Ids { get; set; }
    }

    // API 通用回應 DTO
    public class ApiCouponResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "操作成功";
        public T Data { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // 新增：警告資訊
        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;
        public Dictionary<string, object> WarningData { get; set; } = new();

        public static ApiCouponResponseDto<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiCouponResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiCouponResponseDto<T> ErrorResult(string message, Dictionary<string, string> errors = null)
        {
            return new ApiCouponResponseDto<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new Dictionary<string, string>()
            };
        }
        
        public static ApiCouponResponseDto<T> WarningResult(T data, string message, string warningMessage, Dictionary<string, object> warningData = null)
        {
            return new ApiCouponResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data,
                HasWarning = true,
                WarningMessage = warningMessage,
                WarningData = warningData ?? new Dictionary<string, object>()
            };
        }
    }

    // 擴展方法類別
    public static class CouponMappingExtensions
    {
        // Entity 轉 DTO
        public static CouponResponseDto ToDto(this Coupon coupon)
        {
            var now = DateTime.Now;
            string status = coupon.StartAt <= now && coupon.ExpiredAt >= now
                ? "啟用"
                : (coupon.ExpiredAt < now ? "已過期" : "未開始");

            return new CouponResponseDto
            {
                Id = coupon.Id,
                Title = coupon.Title,
                DiscountType = coupon.DiscountType,
                DiscountTypeLabel = GetDiscountTypeLabel(coupon.DiscountType),
                DiscountAmount = coupon.DiscountAmount,
                MinSpend = coupon.MinSpend,
                StartAt = coupon.StartAt,
                CreatedAt = coupon.CreatedAt,  // 修正：使用正確的 CreatedAt 而不是 StartAt
                ExpiredAt = coupon.ExpiredAt,
                UsageLimit = coupon.UsageLimit,
                UsedCount = coupon.UsedCount,
                ApplicableLevelId = coupon.ApplicableLevelId,
                SellersId = coupon.SellersId,
                IsActive = coupon.IsActive,
                Status = status,
                StatusLabel = status
            };
        }

        // DTO 轉 Entity (創建)
        public static Coupon ToEntity(this CreateCouponDto dto)
        {
            return new Coupon
            {
                Title = dto.Title,
                DiscountType = dto.DiscountType,
                DiscountAmount = dto.DiscountAmount,
                MinSpend = dto.MinSpend ?? 0,
                StartAt = dto.StartAt,
                ExpiredAt = dto.ExpiredAt,
                UsageLimit = dto.UsageLimit,
                UsedCount = 0, // 新建時預設為0
                IsActive = true, // ✅ 新建優惠券預設為啟用
                ApplicableLevelId = dto.ApplicableLevelId,
                SellersId = dto.SellersId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        // DTO 更新到 Entity
        public static void UpdateEntity(this UpdateCouponDto dto, Coupon coupon)
        {
            coupon.Title = dto.Title;
            coupon.DiscountType = dto.DiscountType;
            coupon.DiscountAmount = dto.DiscountAmount;
            coupon.MinSpend = dto.MinSpend ?? 0;
            coupon.StartAt = dto.StartAt;
            coupon.ExpiredAt = dto.ExpiredAt;
            coupon.UsageLimit = dto.UsageLimit;
            // 注意：不更新 UsedCount，這應該由系統管理
            coupon.ApplicableLevelId = dto.ApplicableLevelId;
            coupon.SellersId = dto.SellersId;
        }

        private static string GetDiscountTypeLabel(string discountType)
        {
            return discountType?.ToLower() switch
            {
                "折扣碼" or "percentage" or "%數折扣" => "%數折扣",
                "點數返還" or "j幣回饋" => "J幣回饋",
                "滿減" => "滿減",
                "免運費" => "滿減", // 免運費歸類為滿減
                _ => "滿減" // 其他類型預設為滿減
            };
        }
    }
}