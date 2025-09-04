using System.ComponentModel.DataAnnotations;
using Team.API.Models.EfModel;

namespace Team.API.Models.DTOs
{
    // 簡化的優惠券回應 DTO
    public class CouponDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public int DiscountAmount { get; set; }
        public int? MinSpend { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public int? ApplicableLevelId { get; set; }
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public bool IsActive { get; set; }

        // 格式化顯示
        public string FormattedStartAt => StartAt.ToString("yyyy-MM-dd");
        public string FormattedExpiredAt => ExpiredAt.ToString("yyyy-MM-dd");
        public string FormattedDiscount => GetFormattedDiscount();
        public string ValidPeriod => $"{FormattedStartAt} ~ {FormattedExpiredAt}";
        public string FormattedUsage => UsageLimit.HasValue ? $"{UsedCount}/{UsageLimit}" : $"{UsedCount}/無限";
        public string Status => GetStatus();
        public bool IsExpired => DateTime.Now > ExpiredAt;
        public bool IsNotStarted => DateTime.Now < StartAt;

        private string GetFormattedDiscount()
        {
            return DiscountType?.ToLower() switch
            {
                "折扣碼" or "percentage" or "%數折扣" => $"{DiscountAmount}% 折扣",
                "點數返還" or "j幣回饋" => $"{DiscountAmount} J幣回饋",
                "滿減" => $"滿減 ${DiscountAmount}",
                "免運費" => $"滿減 ${DiscountAmount}",
                _ => $"{DiscountAmount}"
            };
        }

        private string GetStatus()
        {
            var now = DateTime.Now;
            if (now < StartAt) return "未開始";
            if (now > ExpiredAt) return "已過期";
            return "啟用";
        }
    }

    // 優惠券查詢參數 DTO
    public class CouponQueryDto
    {
        public string Search { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SellerId { get; set; } // 廠商篩選

        [Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "每頁筆數必須在 1-100 之間")]
        public int ItemsPerPage { get; set; } = 10;

        public string SortBy { get; set; } = "StartAt";

        [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "排序方向只能是 asc 或 desc")]
        public string SortDirection { get; set; } = "desc";
    }

    // 分頁回應 DTO
    public class PagedResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "操作成功";
        public IEnumerable<T> Data { get; set; } = new List<T>();
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
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "折扣類型不能為空")]
        [StringLength(20, ErrorMessage = "折扣類型長度不能超過 20 個字元")]
        public string DiscountType { get; set; } = string.Empty;

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
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
    }

    // 更新優惠券請求 DTO
    public class UpdateCouponDto
    {
        [Required(ErrorMessage = "優惠券名稱不能為空")]
        [StringLength(100, ErrorMessage = "優惠券名稱長度不能超過 100 個字元")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "折扣類型不能為空")]
        [StringLength(20, ErrorMessage = "折扣類型長度不能超過 20 個字元")]
        public string DiscountType { get; set; } = string.Empty;

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
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
    }

    // API 通用回應 DTO
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "操作成功";
        public T? Data { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static ApiResponseDto<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponseDto<T> ErrorResult(string message, Dictionary<string, string>? errors = null)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new Dictionary<string, string>()
            };
        }
    }

    // 擴展方法類別
    public static class CouponMappingExtensions
    {
        // Entity 轉 DTO
        public static CouponDto ToDto(this Coupon coupon)
        {
            return new CouponDto
            {
                Id = coupon.Id,
                Title = coupon.Title ?? string.Empty,
                DiscountType = coupon.DiscountType ?? string.Empty,
                DiscountAmount = coupon.DiscountAmount,
                MinSpend = coupon.MinSpend,
                StartAt = coupon.StartAt,
                ExpiredAt = coupon.ExpiredAt,
                UsageLimit = coupon.UsageLimit,
                UsedCount = coupon.UsedCount,
                ApplicableLevelId = coupon.ApplicableLevelId,
                CategoryId = coupon.CategoryId,
                SellersId = coupon.SellersId,
                IsActive = coupon.IsActive
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
                ApplicableLevelId = dto.ApplicableLevelId,
                CategoryId = dto.CategoryId,
                SellersId = dto.SellersId,
                IsActive = true,
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
            coupon.CategoryId = dto.CategoryId;
            coupon.SellersId = dto.SellersId;
            coupon.UpdatedAt = DateTime.Now;
        }
    }
}