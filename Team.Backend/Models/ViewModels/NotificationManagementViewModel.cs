using Team.Backend.Models.EfModel; // 使用 EfModel 中的 Notification
using Team.Backend.Models.DTOs; // 添加 DTOs 命名空間

namespace Team.Backend.Models.ViewModels
{
	public class NotificationManagementViewModel
	{
		public List<Notification> Notifications { get; set; } = new List<Notification>();
		public int CurrentPage { get; set; } = 1;
		public int ItemsPerPage { get; set; } = 10;
		public int TotalCount { get; set; }
		public int TotalPages { get; set; }
		
		// 修正排序欄位名稱，對應實際的資料庫欄位
		public string SortBy { get; set; } = "Created_At"; // 改為對應資料庫欄位
		public bool SortDesc { get; set; } = true;
		
		public string SelectedCategory { get; set; } = "";
		public string SelectedStatus { get; set; } = "";
		public string SelectedChannel { get; set; } = "";
		public DateTime? DateFrom { get; set; }
		
		public List<string> Categories { get; set; } = new List<string>();
		public List<string> EmailStatuses { get; set; } = new List<string>();
		public List<string> Channels { get; set; } = new List<string>();
		public int FilterCount { get; set; }
		public Dictionary<string, int> StatisticsByCategory { get; set; } = new Dictionary<string, int>();
		public int TodayCount { get; set; }
		public bool IsLoading { get; set; }

		// 新增：方便的屬性，可從 NotificationStatsDto 轉換
		public void UpdateFromStats(NotificationStatsDto stats)
		{
			if (stats != null)
			{
				TotalCount = stats.TotalCount;
				TodayCount = stats.TodayCount;
				StatisticsByCategory = stats.CategoryStats ?? new Dictionary<string, int>();
			}
		}

		// 新增：分頁計算方法
		public void CalculatePaging()
		{
			TotalPages = TotalCount > 0 ? (int)Math.Ceiling((double)TotalCount / ItemsPerPage) : 1;
			
			// 確保當前頁數不超過總頁數
			if (CurrentPage > TotalPages && TotalPages > 0)
				CurrentPage = TotalPages;
			if (CurrentPage < 1)
				CurrentPage = 1;
		}

		// 新增：分頁資訊屬性
		public bool HasNextPage => CurrentPage < TotalPages;
		public bool HasPreviousPage => CurrentPage > 1;
		public string PageInfoText => TotalCount > 0 
			? $"顯示第 {Math.Max(1, (CurrentPage - 1) * ItemsPerPage + 1)}-{Math.Min(CurrentPage * ItemsPerPage, TotalCount)} 筆，共 {TotalCount} 筆"
			: "目前沒有資料";

		// 新增：獲取排序欄位對應的資料庫欄位名稱
		public string GetDatabaseColumnName(string sortBy)
		{
			return sortBy?.ToLower() switch
			{
				"sentat" => "Sent_At",
				"emailaddress" => "Email_Address", 
				"emailstatus" => "Email_Status",
				"emailsentat" => "Email_Sent_At",
				"emailretry" => "Email_Retry",
				"memberid" => "Member_Id",
				"sellerid" => "Seller_Id",
				
				"createdat" => "Created_At",
				"updatedat" => "Updated_At",
				"isdeleted" => "Is_Deleted",
				"category" => "Category",
				"message" => "Message",
				"channel" => "Channel",
				_ => "Created_At" // 預設排序欄位
			};
		}
	}

	// 新增：資料庫欄位對應的常數類別
	public static class NotificationColumns
	{
		public const string Id = "Id";
		public const string MemberId = "Member_Id";
		public const string SellerId = "Seller_Id";
		public const string Category = "Category";
		public const string Message = "Message";
		public const string SentAt = "Sent_At";
		public const string EmailAddress = "Email_Address";
		public const string EmailStatus = "Email_Status";
		public const string EmailSentAt = "Email_Sent_At";
		public const string EmailRetry = "Email_Retry";
		public const string Channel = "Channel";

		public const string CreatedAt = "Created_At";
		public const string UpdatedAt = "Updated_At";
		public const string IsDeleted = "Is_Deleted";

		// 映射屬性名稱到資料庫欄位名稱
		public static readonly Dictionary<string, string> PropertyToColumn = new()
		{
			{ nameof(Notification.MemberId), MemberId },
			{ nameof(Notification.SellerId), SellerId },
			{ nameof(Notification.SentAt), SentAt },
			{ nameof(Notification.EmailAddress), EmailAddress },
			{ nameof(Notification.EmailStatus), EmailStatus },
			{ nameof(Notification.EmailSentAt), EmailSentAt },
			{ nameof(Notification.EmailRetry), EmailRetry },
			{ nameof(Notification.CreatedAt), CreatedAt },
			{ nameof(Notification.UpdatedAt), UpdatedAt },
			{ nameof(Notification.IsDeleted), IsDeleted }
		};
	}
}