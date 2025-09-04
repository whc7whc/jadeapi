using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels.Logistics
{
    /// <summary>
    /// 物流管理首頁 ViewModel
    /// </summary>
    public class LogisticsIndexVm
    {
        public LogisticsQueryVm Query { get; set; } = new();
        public IEnumerable<CarrierListItemVm> Items { get; set; } = Enumerable.Empty<CarrierListItemVm>();
        public int TotalCount { get; set; }
        public bool CanConnect { get; set; } = true;
    }

    /// <summary>
    /// 物流查詢條件 ViewModel
    /// </summary>
    public class LogisticsQueryVm
    {
        public int? CarrierId { get; set; }  // 物流商ID篩選
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// 物流商列表項目 ViewModel
    /// </summary>
    public class CarrierListItemVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string ECPayCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int OrderCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 物流商選項 ViewModel
    /// </summary>
    public class CarrierOptionVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ECPayCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// 物流商詳細資訊 ViewModel
    /// </summary>
    public class CarrierDetailVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string ECPayCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int OrderCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 新增物流商 ViewModel
    /// </summary>
    public class CarrierCreateVm
    {
        [Required(ErrorMessage = "物流商名稱不能為空")]
        [StringLength(100, ErrorMessage = "名稱長度不能超過100字元")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "聯絡資訊不能為空")]
        [StringLength(200, ErrorMessage = "聯絡資訊長度不能超過200字元")]
        public string Contact { get; set; } = string.Empty;
    }

    /// <summary>
    /// 編輯物流商 ViewModel
    /// </summary>
    public class CarrierEditVm
    {
        [Required(ErrorMessage = "物流商名稱不能為空")]
        [StringLength(100, ErrorMessage = "名稱長度不能超過100字元")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "聯絡資訊不能為空")]
        [StringLength(200, ErrorMessage = "聯絡資訊長度不能超過200字元")]
        public string Contact { get; set; } = string.Empty;
    }

    /// <summary>
    /// 運費設定 ViewModel
    /// </summary>
    public class ShippingSettingsVm
    {
        [Required(ErrorMessage = "免運門檻不能為空")]
        [Range(0, 10000, ErrorMessage = "免運門檻必須在0-10000之間")]
        public decimal FreeShippingThreshold { get; set; } = 1000;

        [Required(ErrorMessage = "VIP會員折扣不能為空")]
        [Range(0, 500, ErrorMessage = "VIP會員折扣必須在0-500之間")]
        public decimal VipMemberDiscount { get; set; } = 20;

        [Required(ErrorMessage = "離島加收費用不能為空")]
        [Range(0, 1000, ErrorMessage = "離島加收費用必須在0-1000之間")]
        public decimal RemoteAreaSurcharge { get; set; } = 100;

        [Required(ErrorMessage = "標準運費不能為空")]
        [Range(0, 500, ErrorMessage = "標準運費必須在0-500之間")]
        public decimal StandardShippingFee { get; set; } = 60;

        [Required(ErrorMessage = "快速配送運費不能為空")]
        [Range(0, 500, ErrorMessage = "快速配送運費必須在0-500之間")]
        public decimal ExpressShippingFee { get; set; } = 120;
    }

    /// <summary>
    /// 物流統計 ViewModel
    /// </summary>
    public class LogisticsStatisticsVm
    {
        public List<LogisticsStatDto> CarrierStats { get; set; } = new();
        public List<RegionStatDto> RegionStats { get; set; } = new();
        public int TotalOrders { get; set; }
        public decimal AverageShippingFee { get; set; }
        public decimal TotalRevenue { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 物流商統計資料 DTO
    /// </summary>
    public class LogisticsStatDto
    {
        public int CarrierId { get; set; }
        public string CarrierName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal UsagePercentage { get; set; }
    }

    /// <summary>
    /// 地區統計資料 DTO
    /// </summary>
    public class RegionStatDto
    {
        public string RegionName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal AvgShippingFee { get; set; }
        public string TopCarrier { get; set; } = string.Empty;
    }
}