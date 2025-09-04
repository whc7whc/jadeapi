using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.ViewModels.Logistics
{
    /// <summary>
    /// 物流診斷結果 ViewModel
    /// </summary>
    public class LogisticsDiagnosticsViewModel
    {
        public bool CanConnect { get; set; }
        public int CarrierCount { get; set; }
        public bool ServiceWorking { get; set; }
        public List<string> Tests { get; set; } = new();
        public List<CarrierInfo> Carriers { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// 物流商資訊
    /// </summary>
    public class CarrierInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}