namespace Team.API.Models.DTOs
{
    /// <summary>
    /// API 統一回應格式
    /// </summary>
    /// <typeparam name="T">回應資料類型</typeparam>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static ApiResponse<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }

    /// <summary>
    /// 分頁回應 DTO
    /// </summary>
    /// <typeparam name="T">資料類型</typeparam>
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }

    /// <summary>
    /// 購物車操作結果 DTO
    /// </summary>
    public class CartOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public CartResponseDto? Cart { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 商品庫存檢查 DTO
    /// </summary>
    public class StockCheckDto
    {
        public int ProductId { get; set; }
        public int AttributeValueId { get; set; }
        public int RequestedQuantity { get; set; }
        public int AvailableStock { get; set; }
        public bool IsAvailable { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string AttributeInfo { get; set; } = string.Empty;
    }

    /// <summary>
    /// 購物車統計 DTO
    /// </summary>
    public class CartStatisticsDto
    {
        public int TotalCarts { get; set; }
        public int ActiveCarts { get; set; }
        public int AbandonedCarts { get; set; }
        public decimal AverageCartValue { get; set; }
        public int TotalItems { get; set; }
        public Dictionary<string, int> TopProducts { get; set; } = new Dictionary<string, int>();
    }
}