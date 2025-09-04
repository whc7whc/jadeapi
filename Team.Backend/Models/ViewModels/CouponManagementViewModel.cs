using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.ViewModels
{
    public class CouponManagementViewModel
    {
        public List<Coupon> Coupons { get; set; } = new List<Coupon>();
        public int CurrentPage { get; set; } = 1;
        public int ItemsPerPage { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public string SortBy { get; set; } = "StartAt";
        public bool SortDesc { get; set; } = true;
        public string SelectedType { get; set; } = "";
        public string SelectedStatus { get; set; } = "";
        public string SearchKeyword { get; set; } = "";
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<string> CouponTypes { get; set; } = new List<string>();
        public List<string> CouponStatuses { get; set; } = new List<string>();
        public int FilterCount { get; set; }
        public Dictionary<string, int> StatisticsByType { get; set; } = new Dictionary<string, int>();
        public int ActiveCount { get; set; }
        public int ExpiredCount { get; set; }
        public int TotalUsageCount { get; set; }
        public bool IsLoading { get; set; }
    }
}