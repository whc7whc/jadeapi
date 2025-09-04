using System.ComponentModel.DataAnnotations;
using Team.API.Models.EfModel;

namespace Team.API.Models.DTOs
{
    // Coupon response DTO
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

        // Formatted properties
        public string FormattedStartAt => StartAt.ToString("yyyy-MM-dd");
        public string FormattedExpiredAt => ExpiredAt.ToString("yyyy-MM-dd");
        public string FormattedDiscount => GetFormattedDiscount();
        public string ValidPeriod => $"{FormattedStartAt} ~ {FormattedExpiredAt}";
        public string FormattedUsage => UsageLimit.HasValue ? $"{UsedCount}/{UsageLimit}" : $"{UsedCount}/unlimited";
        public string Status => GetStatus();
        public bool IsExpired => DateTime.Now > ExpiredAt;
        public bool IsNotStarted => DateTime.Now < StartAt;

        private string GetFormattedDiscount()
        {
            return DiscountType?.ToLower() switch
            {
                "percentage" or "percent_discount" => $"{DiscountAmount}% off",
                "points_return" or "j_coin_return" => $"{DiscountAmount} J-Coin return",
                "amount_off" => $"${DiscountAmount} off",
                "free_shipping" => $"Free shipping (${DiscountAmount})",
                _ => $"{DiscountAmount}"
            };
        }

        private string GetStatus()
        {
            var now = DateTime.Now;
            if (now < StartAt) return "not_started";
            if (now > ExpiredAt) return "expired";
            return "active";
        }
    }

    // Coupon query parameters DTO
    public class CouponQueryDto
    {
        public string Search { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SellerId { get; set; } // Seller filter

        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Items per page must be between 1-100")]
        public int ItemsPerPage { get; set; } = 10;

        public string SortBy { get; set; } = "StartAt";

        [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "Sort direction must be asc or desc")]
        public string SortDirection { get; set; } = "desc";
    }

    // Paged response DTO
    public class PagedResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Operation successful";
        public IEnumerable<T> Data { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int ItemsPerPage { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    // Create coupon request DTO
    public class CreateCouponDto
    {
        [Required(ErrorMessage = "Coupon title is required")]
        [StringLength(100, ErrorMessage = "Coupon title cannot exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount type is required")]
        [StringLength(20, ErrorMessage = "Discount type cannot exceed 20 characters")]
        public string DiscountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount amount is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Discount amount must be greater than 0")]
        public int DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Minimum spend amount cannot be negative")]
        public int? MinSpend { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "End time is required")]
        public DateTime ExpiredAt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be greater than 0")]
        public int? UsageLimit { get; set; }

        public int? ApplicableLevelId { get; set; }
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
    }

    // Update coupon request DTO
    public class UpdateCouponDto
    {
        [Required(ErrorMessage = "Coupon title is required")]
        [StringLength(100, ErrorMessage = "Coupon title cannot exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount type is required")]
        [StringLength(20, ErrorMessage = "Discount type cannot exceed 20 characters")]
        public string DiscountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount amount is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Discount amount must be greater than 0")]
        public int DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Minimum spend amount cannot be negative")]
        public int? MinSpend { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "End time is required")]
        public DateTime ExpiredAt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be greater than 0")]
        public int? UsageLimit { get; set; }

        public int? ApplicableLevelId { get; set; }
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
    }

    // API generic response DTO
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Operation successful";
        public T? Data { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static ApiResponseDto<T> SuccessResult(T data, string message = "Operation successful")
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

    // Mapping extension methods
    public static class CouponMappingExtensions
    {
        // Entity to DTO
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

        // DTO to Entity (Create)
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
                UsedCount = 0, // Default to 0 for new coupons
                ApplicableLevelId = dto.ApplicableLevelId,
                CategoryId = dto.CategoryId,
                SellersId = dto.SellersId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        // DTO updates Entity
        public static void UpdateEntity(this UpdateCouponDto dto, Coupon coupon)
        {
            coupon.Title = dto.Title;
            coupon.DiscountType = dto.DiscountType;
            coupon.DiscountAmount = dto.DiscountAmount;
            coupon.MinSpend = dto.MinSpend ?? 0;
            coupon.StartAt = dto.StartAt;
            coupon.ExpiredAt = dto.ExpiredAt;
            coupon.UsageLimit = dto.UsageLimit;
            // Note: Do not update UsedCount, this is managed by the system
            coupon.ApplicableLevelId = dto.ApplicableLevelId;
            coupon.CategoryId = dto.CategoryId;
            coupon.SellersId = dto.SellersId;
            coupon.UpdatedAt = DateTime.Now;
        }
    }
}