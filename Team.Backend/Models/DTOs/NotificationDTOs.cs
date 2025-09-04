// NotificationDTOs.cs - 完全修正版本，解決 SpecificAccount 驗證問題
using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.DTOs
{
    // 基礎通知 DTO
    public class BaseNotificationDto
    {
        [Required(ErrorMessage = "分類為必填欄位")]
        [MaxLength(10, ErrorMessage = "分類長度不能超過10個字元")]
        public string Category { get; set; }

        [Required(ErrorMessage = "訊息內容為必填欄位")]
        [MaxLength(2000, ErrorMessage = "訊息內容不能超過2000個字元")]
        public string Message { get; set; }

        [Required(ErrorMessage = "通知管道為必填欄位")]
        [MaxLength(20, ErrorMessage = "通知管道長度不能超過20個字元")]
        public string Channel { get; set; } = "email";

        [Required(ErrorMessage = "郵件狀態為必填欄位")]
        [MaxLength(20, ErrorMessage = "郵件狀態長度不能超過20個字元")]
        public string EmailStatus { get; set; } = "draft";

        public DateTime? SentAt { get; set; }
        public int? MemberId { get; set; }
        public int? SellerId { get; set; }
    }

    // 創建單一通知 DTO (用於指定帳號)
    public class CreateNotificationDto : BaseNotificationDto
    {
        [Required(ErrorMessage = "收件人郵件地址為必填欄位")]
        [EmailAddress(ErrorMessage = "請輸入有效的郵件地址")]
        [MaxLength(256, ErrorMessage = "郵件地址長度不能超過256個字元")]
        public string EmailAddress { get; set; }
    }

    // 🔧 關鍵修正：批量通知 DTO - 移除 SpecificAccount 的必填驗證
    public class CreateBulkNotificationDto : BaseNotificationDto
    {
        [Required(ErrorMessage = "發送目標類型為必填欄位")]
        [Range(1, 3, ErrorMessage = "發送目標類型必須是1(全部會員)、2(全部廠商)或3(指定帳號)")]
        public int TargetType { get; set; }

        // 🔧 關鍵修正：完全移除所有驗證屬性，改為 Controller 中手動驗證
        public string SpecificAccount { get; set; }
    }

    // 更新通知 DTO
    public class UpdateNotificationDto : BaseNotificationDto
    {
        [EmailAddress(ErrorMessage = "請輸入有效的郵件地址")]
        [MaxLength(256, ErrorMessage = "郵件地址長度不能超過256個字元")]
        public string EmailAddress { get; set; }
    }

    // 查詢通知 DTO
    public class NotificationQueryDto
    {
        public int Page { get; set; } = 1;
        public int ItemsPerPage { get; set; } = 10;
        public string Search { get; set; } = "";
        public string Category { get; set; } = "";
        public string EmailStatus { get; set; } = "";
        public string Channel { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SortBy { get; set; } = "SentAt";
        public string SortDirection { get; set; } = "desc";
    }

    // 通知回應 DTO
    public class NotificationResponseDto
    {
        public int Id { get; set; }
        public int? MemberId { get; set; }
        public int? SellerId { get; set; }
        public string EmailAddress { get; set; } = "";
        public string Category { get; set; } = "";
        public string CategoryLabel { get; set; } = "";
        public string EmailStatus { get; set; } = "";
        public string EmailStatusLabel { get; set; } = "";
        public string Channel { get; set; } = "";
        public string ChannelLabel { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime SentAt { get; set; }
        // ISO 8601 UTC timestamp for client-side correct local rendering
        public string SentAtIso { get; set; } = "";
        public string FormattedSentAt { get; set; } = "";
        public DateTime? EmailSentAt { get; set; }
        public int EmailRetry { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FormattedCreatedAt { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
        public string FormattedUpdatedAt { get; set; } = "";
        public bool IsDeleted { get; set; }
    }

    // 批量操作 DTO
    public class BatchOperationDto
    {
        [Required(ErrorMessage = "請提供要操作的通知 ID 列表")]
        public List<int> Ids { get; set; } = new List<int>();
    }

    // 批量刪除 DTO
    public class BatchDeleteDto : BatchOperationDto
    {
        // 繼承 BatchOperationDto 的驗證規則
    }

    // 🔧 新增：JavaScript 刪除請求 DTO (支援前端 JavaScript 的刪除請求格式)
    public class DeleteNotificationRequestDto
    {
        [Required(ErrorMessage = "請提供要刪除的通知 ID 列表")]
        [MinLength(1, ErrorMessage = "至少需要提供一個通知 ID")]
        public List<int> Ids { get; set; } = new List<int>();
    }

    // 統計資料 DTO
    public class NotificationStatsDto
    {
        public int TotalCount { get; set; }
        public int DeliveredCount { get; set; }
        public int FailedCount { get; set; }
        public int TodayCount { get; set; }
        public int ScheduledCount { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, int> CategoryStats { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StatusStats { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ChannelStats { get; set; } = new Dictionary<string, int>();
    }

    // 匯出統計資料 DTO
    public class ExportStatisticsDto
    {
        [Required(ErrorMessage = "請指定匯出格式")]
        public string Format { get; set; } = "excel"; // excel, pdf, csv
    }

    // API 回應基底 DTO
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T Data { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();

        public static ApiResponseDto<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponseDto<T> ErrorResult(string message, Dictionary<string, string> errors = null)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new Dictionary<string, string>()
            };
        }
    }

    // 分頁回應 DTO
    public class PagedResponseDto<T> : ApiResponseDto<IEnumerable<T>>
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int ItemsPerPage { get; set; }
        public int TotalCount { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}