using Microsoft.AspNetCore.Mvc;
using Team.Backend.Services;
using Team.Backend.Repositories;
using Team.Backend.Models.ViewModels.Logistics;

namespace Team.Backend.Controllers
{
    /// <summary>
    /// 物流診斷控制器 - 用於快速檢查物流管理問題
    /// </summary>
    [Route("Debug/Logistics")]
    public class LogisticsDiagnosticsController : Controller
    {
        private readonly ILogisticsService _logisticsService;
        private readonly ILogisticsRepository _repository;

        public LogisticsDiagnosticsController(
            ILogisticsService logisticsService, 
            ILogisticsRepository repository)
        {
            _logisticsService = logisticsService;
            _repository = repository;
        }

        /// <summary>
        /// 診斷頁面 - 訪問 /Debug/Logistics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var diagnostics = new LogisticsDiagnosticsViewModel();

            try
            {
                // 1. 檢查資料庫連接
                diagnostics.CanConnect = await _repository.CanConnectAsync();
                diagnostics.Tests.Add($"✅ 資料庫連接: {(diagnostics.CanConnect ? "成功" : "失敗")}");

                if (diagnostics.CanConnect)
                {
                    // 2. 檢查 Carriers 表結構
                    try
                    {
                        var carriers = await _repository.GetAllCarriersAsync();
                        diagnostics.CarrierCount = carriers.Count;
                        diagnostics.Tests.Add($"📊 物流商數量: {diagnostics.CarrierCount}");

                        // 3. 檢查資料庫欄位（透過反射檢查實際取得的資料）
                        if (carriers.Any())
                        {
                            var firstCarrier = carriers.First();
                            var properties = firstCarrier.GetType().GetProperties().Select(p => p.Name).ToList();
                            diagnostics.Tests.Add($"🔍 C# 模型屬性: {string.Join(", ", properties)}");
                            
                            // 檢查 IsActive 和可能的 Status 欄位
                            var hasIsActive = properties.Contains("IsActive");
                            var hasStatus = properties.Contains("Status");
                            diagnostics.Tests.Add($"🟢 IsActive 屬性: {(hasIsActive ? "存在" : "不存在")}");
                            diagnostics.Tests.Add($"📋 Status 屬性: {(hasStatus ? "存在" : "不存在")}");
                        }

                        // 4. 檢查啟用的物流商
                        var activeCarriers = carriers.Where(c => c.IsActive).Count();
                        diagnostics.Tests.Add($"🟢 啟用的物流商: {activeCarriers}");

                        // 5. 顯示物流商列表
                        diagnostics.Carriers = carriers.Select(c => new CarrierInfo
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Contact = c.Contact,
                            IsActive = c.IsActive,
                            CreatedAt = c.CreatedAt ?? DateTime.Now
                        }).ToList();

                        // 6. 測試服務層
                        var searchResult = await _logisticsService.SearchCarriersAsync(new LogisticsQueryVm());
                        diagnostics.ServiceWorking = searchResult != null;
                        diagnostics.Tests.Add($"⚙️ 服務層: {(diagnostics.ServiceWorking ? "正常" : "異常")}");
                        diagnostics.Tests.Add($"📝 搜尋結果項目數: {searchResult?.Items?.Count() ?? 0}");

                        // 7. 檢查綠界代碼對應
                        var ecPayCodes = carriers.Select(c => GetECPayCode(c.Name)).Distinct().ToList();
                        diagnostics.Tests.Add($"🔗 綠界代碼種類: {string.Join(", ", ecPayCodes)}");

                        // 8. 檢查物流商選項服務
                        try
                        {
                            var options = await _logisticsService.GetAllCarrierOptionsAsync();
                            diagnostics.Tests.Add($"📋 物流商選項服務: 正常 ({options.Count} 個選項)");
                        }
                        catch (Exception ex)
                        {
                            diagnostics.Tests.Add($"📋 物流商選項服務: 異常 - {ex.Message}");
                        }

                        // 9. 資料庫欄位建議
                        if (carriers.Any())
                        {
                            var carrier = carriers.First();
                            var props = carrier.GetType().GetProperties();
                            var hasStatusField = props.Any(p => p.Name.ToLower().Contains("status"));
                            
                            if (hasStatusField && !props.Any(p => p.Name == "Status"))
                            {
                                diagnostics.Tests.Add($"💡 發現類似 Status 的欄位，建議檢查資料庫結構");
                            }
                            
                            if (!hasStatusField)
                            {
                                diagnostics.Tests.Add($"ℹ️ 模型使用 IsActive (bool) 欄位來管理狀態");
                            }
                        }
                    }
                    catch (Exception tableEx)
                    {
                        diagnostics.Tests.Add($"❌ Carriers 表操作失敗: {tableEx.Message}");
                        if (tableEx.Message.Contains("Invalid object name"))
                        {
                            diagnostics.Tests.Add("💡 建議: Carriers 表可能不存在，請執行資料庫遷移");
                        }
                        if (tableEx.Message.Contains("Invalid column name"))
                        {
                            diagnostics.Tests.Add("💡 建議: 模型與資料庫結構不一致，考慮重新 Scaffold");
                        }
                    }
                }
                else
                {
                    diagnostics.Tests.Add("❌ 無法連接資料庫，請檢查連接字串");
                    diagnostics.Tests.Add($"💡 連接字串位置: appsettings.json -> ConnectionStrings:DefaultConnection");
                }

                // 10. 檢查控制器路由
                diagnostics.Tests.Add($"🌐 當前請求路徑: {Request.Path}");
                diagnostics.Tests.Add($"🏠 建議訪問: /AdminLogistics (物流管理首頁)");
            }
            catch (Exception ex)
            {
                diagnostics.Tests.Add($"💥 系統錯誤: {ex.Message}");
                diagnostics.Error = ex.ToString();
            }

            return View(diagnostics);
        }

        /// <summary>
        /// 根據物流商名稱取得綠界代碼
        /// </summary>
        private string GetECPayCode(string carrierName)
        {
            return carrierName switch
            {
                var name when name.Contains("黑貓") => "HOME_TCAT",
                var name when name.Contains("7-11") || name.Contains("7-ELEVEN") => "UNIMART", 
                var name when name.Contains("全家") => "FAMI",
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// 快速修復 - 插入測試資料
        /// </summary>
        [HttpPost]
        [Route("QuickFix")]
        public async Task<IActionResult> QuickFix()
        {
            try
            {
                var carriers = await _repository.GetAllCarriersAsync();
                if (!carriers.Any())
                {
                    // 插入基本物流商
                    var testCarriers = new[]
                    {
                        new Models.EfModel.Carrier { Name = "黑貓宅急便", Contact = "客服專線: 0800-200-777", IsActive = true, CreatedAt = DateTime.Now },
                        new Models.EfModel.Carrier { Name = "7-11 超商取貨", Contact = "客服專線: 0800-008-711", IsActive = true, CreatedAt = DateTime.Now },
                        new Models.EfModel.Carrier { Name = "全家便利商店", Contact = "客服專線: 0800-030-588", IsActive = true, CreatedAt = DateTime.Now }
                    };

                    foreach (var carrier in testCarriers)
                    {
                        await _repository.CreateCarrierAsync(carrier);
                    }

                    TempData["SuccessMessage"] = "✅ 成功插入 3 筆測試物流商資料！";
                }
                else
                {
                    TempData["InfoMessage"] = $"ℹ️ 資料庫已有 {carriers.Count} 筆物流商資料，無需插入。";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ 插入失敗: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}