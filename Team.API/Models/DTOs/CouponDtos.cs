using System.ComponentModel.DataAnnotations;
using Team.API.Models.EfModel;

namespace Team.API.Models.DTOs
{
    // ²�ƪ��u�f��^�� DTO
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

        // �榡�����
        public string FormattedStartAt => StartAt.ToString("yyyy-MM-dd");
        public string FormattedExpiredAt => ExpiredAt.ToString("yyyy-MM-dd");
        public string FormattedDiscount => GetFormattedDiscount();
        public string ValidPeriod => $"{FormattedStartAt} ~ {FormattedExpiredAt}";
        public string FormattedUsage => UsageLimit.HasValue ? $"{UsedCount}/{UsageLimit}" : $"{UsedCount}/�L��";
        public string Status => GetStatus();
        public bool IsExpired => DateTime.Now > ExpiredAt;
        public bool IsNotStarted => DateTime.Now < StartAt;

        private string GetFormattedDiscount()
        {
            return DiscountType?.ToLower() switch
            {
                "�馩�X" or "percentage" or "%�Ƨ馩" => $"{DiscountAmount}% �馩",
                "�I�ƪ���" or "j���^�X" => $"{DiscountAmount} J���^�X",
                "����" => $"���� ${DiscountAmount}",
                "�K�B�O" => $"���� ${DiscountAmount}",
                _ => $"{DiscountAmount}"
            };
        }

        private string GetStatus()
        {
            var now = DateTime.Now;
            if (now < StartAt) return "���}�l";
            if (now > ExpiredAt) return "�w�L��";
            return "�ҥ�";
        }
    }

    // �u�f��d�߰Ѽ� DTO
    public class CouponQueryDto
    {
        public string Search { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SellerId { get; set; } // �t�ӿz��

        [Range(1, int.MaxValue, ErrorMessage = "���X�����j�� 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "�C�����ƥ����b 1-100 ����")]
        public int ItemsPerPage { get; set; } = 10;

        public string SortBy { get; set; } = "StartAt";

        [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "�ƧǤ�V�u��O asc �� desc")]
        public string SortDirection { get; set; } = "desc";
    }

    // �����^�� DTO
    public class PagedResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "�ާ@���\";
        public IEnumerable<T> Data { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int ItemsPerPage { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    // �Ы��u�f��ШD DTO
    public class CreateCouponDto
    {
        [Required(ErrorMessage = "�u�f��W�٤��ର��")]
        [StringLength(100, ErrorMessage = "�u�f��W�٪��פ���W�L 100 �Ӧr��")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "�馩�������ର��")]
        [StringLength(20, ErrorMessage = "�馩�������פ���W�L 20 �Ӧr��")]
        public string DiscountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "�馩���B���ର��")]
        [Range(1, int.MaxValue, ErrorMessage = "�馩���B�����j�� 0")]
        public int DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "�̧C���O���B���ର�t��")]
        public int? MinSpend { get; set; }

        [Required(ErrorMessage = "�}�l�ɶ����ର��")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "�����ɶ����ର��")]
        public DateTime ExpiredAt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "�ϥΤW�������j�� 0")]
        public int? UsageLimit { get; set; }

        public int? ApplicableLevelId { get; set; }
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
    }

    // ��s�u�f��ШD DTO
    public class UpdateCouponDto
    {
        [Required(ErrorMessage = "�u�f��W�٤��ର��")]
        [StringLength(100, ErrorMessage = "�u�f��W�٪��פ���W�L 100 �Ӧr��")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "�馩�������ର��")]
        [StringLength(20, ErrorMessage = "�馩�������פ���W�L 20 �Ӧr��")]
        public string DiscountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "�馩���B���ର��")]
        [Range(1, int.MaxValue, ErrorMessage = "�馩���B�����j�� 0")]
        public int DiscountAmount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "�̧C���O���B���ର�t��")]
        public int? MinSpend { get; set; }

        [Required(ErrorMessage = "�}�l�ɶ����ର��")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "�����ɶ����ର��")]
        public DateTime ExpiredAt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "�ϥΤW�������j�� 0")]
        public int? UsageLimit { get; set; }

        public int? ApplicableLevelId { get; set; }
        public int? CategoryId { get; set; }
        public int? SellersId { get; set; }
    }

    // API �q�Φ^�� DTO
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "�ާ@���\";
        public T? Data { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static ApiResponseDto<T> SuccessResult(T data, string message = "�ާ@���\")
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

    // �X�i��k���O
    public static class CouponMappingExtensions
    {
        // Entity �� DTO
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

        // DTO �� Entity (�Ы�)
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
                UsedCount = 0, // �s�خɹw�]��0
                ApplicableLevelId = dto.ApplicableLevelId,
                CategoryId = dto.CategoryId,
                SellersId = dto.SellersId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        // DTO ��s�� Entity
        public static void UpdateEntity(this UpdateCouponDto dto, Coupon coupon)
        {
            coupon.Title = dto.Title;
            coupon.DiscountType = dto.DiscountType;
            coupon.DiscountAmount = dto.DiscountAmount;
            coupon.MinSpend = dto.MinSpend ?? 0;
            coupon.StartAt = dto.StartAt;
            coupon.ExpiredAt = dto.ExpiredAt;
            coupon.UsageLimit = dto.UsageLimit;
            // �`�N�G����s UsedCount�A�o���ӥѨt�κ޲z
            coupon.ApplicableLevelId = dto.ApplicableLevelId;
            coupon.CategoryId = dto.CategoryId;
            coupon.SellersId = dto.SellersId;
            coupon.UpdatedAt = DateTime.Now;
        }
    }
}