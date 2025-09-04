using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Team.Backend.Models.DTOs;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using Team.Backend.Services;

namespace Team.Backend.Controllers
{
    [Route("Coupons")] // ✅ 新增：為控制器設定基礎路由
    public class CouponsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CouponsController> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IScheduleService _scheduleService;

        public CouponsController(AppDbContext context, ILogger<CouponsController> logger, IMemoryCache memoryCache, IServiceProvider serviceProvider)
        : base(context, logger)
        {
            _context = context;
            _logger = logger;
            _memoryCache = memoryCache;
            _scheduleService = ScheduleServiceFactory.CreateService(context, serviceProvider);
        }
        
        // ✅ 新增：優惠券排程管理頁面
        [HttpGet]
        [Route("ScheduleManagement")]
        public async Task<IActionResult> ScheduleManagement()
        {
            try
            {
                var schedules = await _scheduleService.GetScheduledTasksAsync("coupon");
                ViewBag.SystemType = _scheduleService.GetSystemType();
                ViewBag.PageTitle = "優惠券排程管理";

                return View(schedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入優惠券排程管理頁面失敗");
                TempData["Error"] = "載入排程資料失敗：" + ex.Message;
                return View(new List<ContentPublishingSchedule>());
            }
        }

        // ✅ 新增：設定優惠券發送排程 API（替代原本的動作排程）
        [HttpPost]
        [Route("ScheduleCouponDispatch")]
        public async Task<IActionResult> ScheduleCouponDispatch([FromBody] ScheduleCouponDispatchRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "輸入資料驗證失敗" });
                }

                // 驗證優惠券是否存在
                var coupon = await _context.Coupons.FindAsync(request.CouponId);
                if (coupon == null)
                {
                    return Json(new { success = false, message = "找不到指定的優惠券" });
                }

                // 驗證排程時間
                if (request.ScheduledTime <= DateTime.Now)
                {
                    return Json(new { success = false, message = "排程時間必須是未來時間" });
                }

                // 驗證會員等級（除了"all"）
                if (request.MemberLevel != "all")
                {
                    if (!int.TryParse(request.MemberLevel, out int levelId))
                    {
                        return Json(new { success = false, message = "無效的會員等級設定" });
                    }

                    var level = await _context.MembershipLevels.FindAsync(levelId);
                    if (level == null)
                    {
                        return Json(new { success = false, message = "找不到指定的會員等級" });
                    }

                    if (!level.IsActive)
                    {
                        return Json(new { success = false, message = "該會員等級已停用" });
                    }
                }

                // 建立排程
                var result = await _scheduleService.ScheduleTaskAsync(
                    "coupon",                    // 內容類型
                    request.CouponId,            // 優惠券 ID  
                    request.ScheduledTime,       // 排程時間
                    1,                          // 使用者 ID（可從 Session 或 Claims 取得）
                    request.MemberLevel          // ✅ 使用會員等級作為 ActionType
                );

                if (result.Success)
                {
                    string targetDescription = await GetTargetDescription(request.MemberLevel);
                    
                    _logger.LogInformation("優惠券發送排程設定成功：券ID={CouponId}, 目標={MemberLevel}, 時間={ScheduledTime}",
                        request.CouponId, request.MemberLevel, request.ScheduledTime);

                    return Json(new
                    {
                        success = true,
                        message = $"優惠券「{coupon.Title}」發送排程設定成功！將於 {request.ScheduledTime:yyyy/MM/dd HH:mm} 發送給{targetDescription}",
                        scheduleId = result.ScheduleId
                    });
                }
                else
                {
                    return Json(new { success = false, message = $"排程設定失敗：{result.ErrorMessage}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定優惠券發送排程時發生錯誤");
                return Json(new { success = false, message = "系統發生錯誤，請稍後再試", detail = ex.ToString() });
            }
        }

        // ✅ 新增：取消排程 API
        [HttpPost]
        [Route("CancelSchedule")]
        public async Task<IActionResult> CancelSchedule([FromBody] CancelScheduleRequest request)
        {
            try
            {
                bool success = await _scheduleService.CancelScheduleAsync(request.ScheduleId);

                if (success)
                {
                    return Json(new { success = true, message = "排程已成功取消" });
                }
                else
                {
                    return Json(new { success = false, message = "取消排程失敗，可能該排程不存在或已執行" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消優惠券排程時發生錯誤，排程ID: {ScheduleId}", request.ScheduleId);
                return Json(new { success = false, message = "取消排程時發生錯誤", detail = ex.ToString() });
            }
        }

        // ✅ 輔助方法：取得目標描述
        private async Task<string> GetTargetDescription(string memberLevel)
        {
            if (memberLevel == "all")
            {
                return "全部會員";
            }

            if (int.TryParse(memberLevel, out int levelId))
            {
                var level = await _context.MembershipLevels.FindAsync(levelId);
                if (level != null)
                {
                    return $"{level.LevelName}會員";
                }
            }

            return memberLevel;
        }

        // 輔助方法：取得動作描述（保留以免其他地方還在使用）
        private string GetActionDescription(string actionType)
        {
            return actionType switch
            {
                "activate" => "啟動",
                "deactivate" => "停用",
                "start" => "開始生效",
                "expire" => "過期處理",
                "all" => "發送給全部會員",
                _ => int.TryParse(actionType, out _) ? "發送給指定等級會員" : actionType
            };
        }

        // ✅ 請求模型
        public class ScheduleCouponDispatchRequest
        {
            public int CouponId { get; set; }
            public string MemberLevel { get; set; } = ""; // 可以是 "all" 或會員等級 ID
            public DateTime ScheduledTime { get; set; }
        }

        public class CancelScheduleRequest
        {
            public int ScheduleId { get; set; }
        }
        
        // 主頁面載入
        [HttpGet]
        [Route("CouponsManager")]
        public IActionResult CouponsManager(string type = "", string status = "",
            string keyword = "", DateTime? dateFrom = null, DateTime? dateTo = null,
            int page = 1, int itemsPerPage = 10, string sortBy = "CreatedAt", bool sortDesc = true)
        {
            var viewModel = new CouponManagementViewModel
            {
                Coupons = new List<Coupon>(),
                CurrentPage = page,
                ItemsPerPage = itemsPerPage,
                TotalCount = 0,
                TotalPages = 0,
                CouponTypes = new List<string> { "%數折扣", "J幣回饋", "滿減" },
                CouponStatuses = new List<string> { "啟用", "未開始", "已過期" },
                FilterCount = 0,
                StatisticsByType = new Dictionary<string, int>(),
                ActiveCount = 0,
                ExpiredCount = 0,
                IsLoading = false
            };

            return View(viewModel);
        }
         
        // API: 獲取優惠券列表
        [HttpGet]
        [Route("GetCoupon/{id}")]
        public async Task<IActionResult> GetCoupon(int id)
        {
            try
            {
                var coupon = await _context.Coupons
                    .AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => new 
                    {
                        c.Id,
                        c.Title,
                        c.DiscountType,
                        c.DiscountAmount,
                        c.MinSpend,
                        c.StartAt,
                        c.ExpiredAt,
                        c.UsageLimit,
                        c.UsedCount,
                        c.ApplicableLevelId,
                        c.SellersId,
                        c.IsActive,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (coupon == null)
                    return Json(ApiCouponResponseDto<object>.ErrorResult("找不到指定的優惠券"));

                var couponDto = new CouponResponseDto
                {
                    Id = coupon.Id,
                    Title = coupon.Title,
                    DiscountType = coupon.DiscountType,
                    DiscountTypeLabel = GetDiscountTypeLabel(coupon.DiscountType),
                    DiscountAmount = coupon.DiscountAmount,
                    MinSpend = coupon.MinSpend,
                    StartAt = coupon.StartAt,
                    CreatedAt = coupon.CreatedAt,
                    ExpiredAt = coupon.ExpiredAt,
                    UsageLimit = coupon.UsageLimit,
                    UsedCount = coupon.UsedCount,
                    ApplicableLevelId = coupon.ApplicableLevelId,
                    SellersId = coupon.SellersId,
                    IsActive = coupon.IsActive,
                    Status = GetStatus(coupon.StartAt, coupon.ExpiredAt),
                    StatusLabel = GetStatus(coupon.StartAt, coupon.ExpiredAt)
                };

                return Json(ApiCouponResponseDto<CouponResponseDto>.SuccessResult(couponDto, "獲取單筆成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取優惠券失敗: {Id}", id);
                return Json(ApiCouponResponseDto<object>.ErrorResult("獲取優惠券失敗：" + ex.Message));
            }
        }

        [HttpGet]
        [Route("GetCoupons")]
        public async Task<IActionResult> GetCoupons([FromQuery] CouponQueryDto query)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.First().ErrorMessage
                        );

                    return Json(ApiCouponResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                _logger.LogInformation("開始獲取優惠券列表，參數：{@Query}", query);

                // 使用明確的 Select 來避免 CategoryId1 錯誤，並包含廠商資訊
                var queryable = _context.Coupons
                    .AsNoTracking()
                    .Select(c => new 
                    {
                        c.Id,
                        c.Title,
                        c.DiscountType,
                        c.DiscountAmount,
                        c.MinSpend,
                        c.StartAt,
                        c.ExpiredAt,
                        c.UsageLimit,
                        c.UsedCount,
                        c.ApplicableLevelId,
                        c.SellersId,
                        c.IsActive,
                        c.CreatedAt,
                        c.UpdatedAt,
                        // 廠商資訊
                        SellerRealName = c.Sellers != null ? c.Sellers.RealName : "",
                        SellerEmail = c.Sellers != null && c.Sellers.Members != null ? c.Sellers.Members.Email : "",
                        SellerStatus = c.Sellers != null ? c.Sellers.ApplicationStatus : ""
                    });

                // 搜尋條件 - 擴展搜尋範圍到廠商資訊
                if (!string.IsNullOrEmpty(query.Search))
                {
                    queryable = queryable.Where(c =>
                        EF.Functions.Like(c.Title, $"%{query.Search}%") ||
                        EF.Functions.Like(c.DiscountType, $"%{query.Search}%") ||
                        EF.Functions.Like(c.SellerRealName, $"%{query.Search}%") ||
                        EF.Functions.Like(c.SellerEmail, $"%{query.Search}%"));
                }

                // 篩選條件
                if (!string.IsNullOrEmpty(query.DiscountType))
                {
                    // 將前端三大類映成資料庫可能的實際值
                    var accepted = RawTypesForNormalized(query.DiscountType);
                    queryable = queryable.Where(c => accepted.Contains(c.DiscountType));
                }

                // 新增：來源篩選
                if (!string.IsNullOrEmpty(query.CouponSource))
                {
                    queryable = query.CouponSource.ToLower() switch
                    {
                        "vendor" => queryable.Where(c => c.SellersId.HasValue && c.SellersId > 0),
                        "platform" => queryable.Where(c => !c.SellersId.HasValue || c.SellersId == 0),
                        _ => queryable
                    };
                }

                // 特定廠商篩選
                if (query.SellerId.HasValue && query.SellerId > 0)
                {
                    queryable = queryable.Where(c => c.SellersId == query.SellerId);
                }

                if (!string.IsNullOrEmpty(query.Status))
                {
                    var now = DateTime.Now;
                    queryable = query.Status switch
                    {
                        "啟用" => queryable.Where(c => c.StartAt <= now && c.ExpiredAt >= now),
                        "未開始" => queryable.Where(c => c.StartAt > now),
                        "已過期" => queryable.Where(c => c.ExpiredAt < now),
                        _ => queryable
                    };
                }

                if (query.StartDate.HasValue)
                {
                    var start = query.StartDate.Value;
                    queryable = queryable.Where(c => c.StartAt >= start);
                }

                if (query.EndDate.HasValue)
                {
                    var end = query.EndDate.Value.AddDays(1);
                    queryable = queryable.Where(c => c.StartAt <= end);
                }

                // 排序
                var isDesc = query.SortDirection.ToLower() == "desc";
                queryable = query.SortBy.ToLower() switch
                {
                    "title" => isDesc ? queryable.OrderByDescending(c => c.Title) : queryable.OrderBy(c => c.Title),
                    "discounttype" => isDesc ? queryable.OrderByDescending(c => c.DiscountType) : queryable.OrderBy(c => c.DiscountType),
                    "discountamount" => isDesc ? queryable.OrderByDescending(c => c.DiscountAmount) : queryable.OrderBy(c => c.DiscountAmount),
                    "expiredat" => isDesc ? queryable.OrderByDescending(c => c.ExpiredAt) : queryable.OrderBy(c => c.ExpiredAt),
                    "usedcount" => isDesc ? queryable.OrderByDescending(c => c.UsedCount) : queryable.OrderBy(c => c.UsedCount),
                    "usagelimit" => isDesc ? queryable.OrderByDescending(c => c.UsageLimit) : queryable.OrderBy(c => c.UsageLimit),
                    "sellername" => isDesc ? queryable.OrderByDescending(c => c.SellerRealName) : queryable.OrderBy(c => c.SellerRealName),
                    _ => isDesc ? queryable.OrderByDescending(c => c.StartAt) : queryable.OrderBy(c => c.StartAt)
                };

                var totalCount = await queryable.CountAsync();

                var coupons = await queryable
                    .Skip((query.Page - 1) * query.ItemsPerPage)
                    .Take(query.ItemsPerPage)
                    .ToListAsync();

                // 手動轉換為 CouponResponseDto，包含廠商資訊
                var couponDtos = coupons.Select(c => new CouponResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    DiscountType = c.DiscountType,
                    DiscountTypeLabel = GetDiscountTypeLabel(c.DiscountType),
                    DiscountAmount = c.DiscountAmount,
                    MinSpend = c.MinSpend,
                    StartAt = c.StartAt,
                    CreatedAt = c.CreatedAt,
                    ExpiredAt = c.ExpiredAt,
                    UsageLimit = c.UsageLimit,
                    UsedCount = c.UsedCount,
                    ApplicableLevelId = c.ApplicableLevelId,
                    SellersId = c.SellersId,
                    IsActive = c.IsActive,
                    Status = GetStatus(c.StartAt, c.ExpiredAt),
                    StatusLabel = GetStatus(c.StartAt, c.ExpiredAt),
                    // 廠商資訊
                    SellerRealName = c.SellerRealName,
                    SellerEmail = c.SellerEmail,
                    SellerStatus = c.SellerStatus
                }).ToList();

                var response = new PagedCouponResponseDto<CouponResponseDto>
                {
                    Success = true,
                    Message = "獲取優惠券列表成功",
                    Data = couponDtos,
                    TotalCount = totalCount,
                    CurrentPage = query.Page,
                    TotalPages = (int)Math.Ceiling((double)totalCount / query.ItemsPerPage),
                    ItemsPerPage = query.ItemsPerPage
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取優惠券列表失敗");
                return Json(ApiCouponResponseDto<object>.ErrorResult("獲取優惠券列表失敗：" + ex.Message));
            }
        }

        // 輔助方法：獲取狀態
        private static string GetStatus(DateTime startAt, DateTime expiredAt)
        {
            var now = DateTime.Now;
            return startAt <= now && expiredAt >= now
                ? "啟用"
                : (expiredAt < now ? "已過期" : "未開始");
        }

        // 輔助方法：獲取折扣類型標籤
        private static string GetDiscountTypeLabel(string discountType)
        {
            return discountType?.ToLower() switch
            {
                "折扣碼" or "percentage" or "%數折扣" => "%數折扣",
                "點數返還" or "j幣回饋" => "J幣回饋",
                "滿減" => "滿減",
                "免運費" => "滿減", // 免運費歸類為滿減
                _ => "滿減" // 其他類型預設為滿減
            };
        }

        // API: 創建優惠券
        [HttpPost]
        [Route("CreateCoupon")]
        public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.First().ErrorMessage
                        );

                    return Json(ApiCouponResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                // 類型額外驗證 - 支援多種格式
                var normalizedType = NormalizeDiscountType(dto.DiscountType);
                if (IsPercentageDiscount(dto.DiscountType))
                {
                    if (dto.DiscountAmount <= 0 || dto.DiscountAmount > 100)
                        return Json(ApiCouponResponseDto<object>.ErrorResult("折扣百分比必須介於 1~100"));
                }
                else
                {
                    if (dto.DiscountAmount <= 0)
                        return Json(ApiCouponResponseDto<object>.ErrorResult("折扣金額必須大於 0"));
                }

                // 滿減類型強制要求最低消費
                if (normalizedType == "滿減" && (!dto.MinSpend.HasValue || dto.MinSpend <= 0))
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("滿減優惠必須設定最低消費金額"));
                }

                // 驗證日期邏輯
                if (dto.ExpiredAt <= dto.StartAt)
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("結束時間必須大於開始時間"));
                }

                _logger.LogInformation("開始創建優惠券");

                var coupon = dto.ToEntity();
                coupon.DiscountType = NormalizeDiscountType(dto.DiscountType);
                _context.Coupons.Add(coupon);
                await _context.SaveChangesAsync();
                _memoryCache.Remove("coupon_stats"); // 使統計立即失效

                var responseDto = coupon.ToDto();

                return Json(ApiCouponResponseDto<CouponResponseDto>.SuccessResult(
                    responseDto,
                    "優惠券創建成功"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "創建優惠券失敗");
                return Json(ApiCouponResponseDto<object>.ErrorResult("創建優惠券失敗：" + ex.Message));
            }
        }

        // API: 更新優惠券（含廠商警示）
        [HttpPut]
        [Route("UpdateCoupon/{id}")]
        public async Task<IActionResult> UpdateCoupon(int id, [FromBody] UpdateCouponDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.First().ErrorMessage
                        );

                    return Json(ApiCouponResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                // 獲取優惠券並包含廠商資訊
                var coupon = await _context.Coupons
                    .Include(c => c.Sellers)
                    .FirstOrDefaultAsync(c => c.Id == id);
                    
                if (coupon == null)
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("找不到指定的優惠券"));
                }

                // 檢查是否為廠商優惠券並產生警示
                bool isVendorCoupon = coupon.SellersId.HasValue && coupon.SellersId > 0;
                string warningMessage = "";
                var warningData = new Dictionary<string, object>();

                if (isVendorCoupon)
                {
                    var sellerName = coupon.Sellers?.RealName ?? "未知廠商";
                    var sellerEmail = coupon.Sellers?.Members?.Email ?? "";
                    
                    warningMessage = $"⚠️ 注意：您正在編輯廠商優惠券！\n\n" +
                                   $"廠商：{sellerName}\n" +
                                   $"Email：{sellerEmail}\n\n" +
                                   $"建議在修改前先與廠商聯繫確認，\n" +
                                   $"避免影響廠商的促銷活動。";
                    
                    warningData = new Dictionary<string, object>
                    {
                        ["isVendorCoupon"] = true,
                        ["sellerId"] = coupon.SellersId,
                        ["sellerName"] = sellerName,
                        ["sellerEmail"] = sellerEmail,
                        ["couponTitle"] = coupon.Title
                    };

                    _logger.LogWarning("管理員正在編輯廠商優惠券 - ID: {CouponId}, 廠商: {SellerName} ({SellerId})", 
                        id, sellerName, coupon.SellersId);
                }

                // 類型額外驗證 - 支援多種格式
                var normalizedType = NormalizeDiscountType(dto.DiscountType);
                if (IsPercentageDiscount(dto.DiscountType))
                {
                    if (dto.DiscountAmount <= 0 || dto.DiscountAmount > 100)
                        return Json(ApiCouponResponseDto<object>.ErrorResult("折扣百分比必須介於 1~100"));
                }
                else
                {
                    if (dto.DiscountAmount <= 0)
                        return Json(ApiCouponResponseDto<object>.ErrorResult("折扣金額必須大於 0"));
                }

                // 滿減類型強制要求最低消費
                if (normalizedType == "滿減" && (!dto.MinSpend.HasValue || dto.MinSpend <= 0))
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("滿減優惠必須設定最低消費金額"));
                }

                // 驗證日期邏輯
                if (dto.ExpiredAt <= dto.StartAt)
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("結束時間必須大於開始時間"));
                }

                // 記錄修改前的值（用於審計日誌）
                var originalValues = new
                {
                    coupon.Title,
                    coupon.DiscountType,
                    coupon.DiscountAmount,
                    coupon.MinSpend,
                    coupon.StartAt,
                    coupon.ExpiredAt,
                    coupon.UsageLimit
                };

                // 更新優惠券
                dto.UpdateEntity(coupon);
                coupon.DiscountType = NormalizeDiscountType(dto.DiscountType);
                coupon.UpdatedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();
                _memoryCache.Remove("coupon_stats"); // 使統計立即失效

                // 如果是廠商優惠券，記錄詳細的修改日誌
                if (isVendorCoupon)
                {
                    _logger.LogInformation("廠商優惠券已更新 - ID: {CouponId}, 廠商: {SellerName}, 原值: {@OriginalValues}, 新值: {@NewValues}", 
                        id, coupon.Sellers?.RealName, originalValues, new { dto.Title, dto.DiscountType, dto.DiscountAmount, dto.MinSpend, dto.StartAt, dto.ExpiredAt, dto.UsageLimit });
                }

                var responseDto = coupon.ToDto();

                // 如果有警告，返回警告回應
                if (isVendorCoupon)
                {
                    return Json(ApiCouponResponseDto<CouponResponseDto>.WarningResult(
                        responseDto,
                        "優惠券更新成功",
                        warningMessage,
                        warningData
                    ));
                }

                return Json(ApiCouponResponseDto<CouponResponseDto>.SuccessResult(
                    responseDto,
                    "優惠券更新成功"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新優惠券失敗");
                return Json(ApiCouponResponseDto<object>.ErrorResult("更新優惠券失敗：" + ex.Message));
            }
        }

        // API: 刪除優惠券
        [HttpDelete]
        [Route("DeleteCoupon/{id}")]
        public async Task<IActionResult> DeleteCoupon(int id)
        {
            try
            {
                var coupon = await _context.Coupons.FindAsync(id);
                if (coupon == null)
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("找不到指定的優惠券"));
                }

                // 檢查是否有會員正在使用此優惠券
                var memberCouponExists = await _context.MemberCoupons
                    .AnyAsync(mc => mc.CouponId == id);
                if (memberCouponExists)
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("無法刪除：已有會員領取此優惠券"));
                }

                // 檢查是否有會員等級正在使用此優惠券作為月優惠券
                var membershipLevelExists = await _context.MembershipLevels
                    .AnyAsync(ml => ml.MonthlyCouponId == id);
                if (membershipLevelExists)
                {
                    return Json(ApiCouponResponseDto<object>.ErrorResult("無法刪除：此優惠券正被會員等級使用"));
                }

                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();
                _memoryCache.Remove("coupon_stats"); // 使統計立即失效

                return Json(ApiCouponResponseDto<object>.SuccessResult(null, "優惠券刪除成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除優惠券失敗");
                return Json(ApiCouponResponseDto<object>.ErrorResult("刪除優惠券失敗：" + ex.Message));
            }
        }

        // API: 批量刪除優惠券
        [HttpDelete]
        public async Task<IActionResult> DeleteCoupons([FromBody] BatchCouponDeleteDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.First().ErrorMessage
                        );

                    return Json(ApiCouponResponseDto<object>.ErrorResult("輸入驗證失敗", errors));
                }

                // 檢查哪些優惠券被使用中
                var usedByMembers = await _context.MemberCoupons
                    .Where(mc => dto.Ids.Contains(mc.CouponId.Value))
                    .Select(mc => mc.CouponId.Value)
                    .Distinct()
                    .ToListAsync();

                var usedByLevels = await _context.MembershipLevels
                    .Where(ml => ml.MonthlyCouponId.HasValue && dto.Ids.Contains(ml.MonthlyCouponId.Value))
                    .Select(ml => ml.MonthlyCouponId.Value)
                    .Distinct()
                    .ToListAsync();

                var usedIds = usedByMembers.Union(usedByLevels).ToList();
                
                if (usedIds.Any())
                {
                    var usedTitles = await _context.Coupons
                        .Where(c => usedIds.Contains(c.Id))
                        .Select(c => c.Title)
                        .ToListAsync();
                    
                    return Json(ApiCouponResponseDto<object>.ErrorResult(
                        $"無法刪除：以下優惠券正在使用中：{string.Join("、", usedTitles)}"
                    ));
                }

                var couponsToDelete = await _context.Coupons
                    .Where(c => dto.Ids.Contains(c.Id))
                    .ToListAsync();

                _context.Coupons.RemoveRange(couponsToDelete);
                var deletedCount = await _context.SaveChangesAsync();
                _memoryCache.Remove("coupon_stats"); // 使統計立即失效

                return Json(ApiCouponResponseDto<object>.SuccessResult(
                    null,
                    $"成功刪除 {deletedCount} 筆優惠券"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量刪除優惠券失敗");
                return Json(ApiCouponResponseDto<object>.ErrorResult("批量刪除優惠券失敗：" + ex.Message));
            }
        }

        // API: 獲取統計資料
        [HttpGet]
        [Route("GetStatistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] bool noCache = false)
        {
            try
            {
                const string cacheKey = "coupon_stats";
                const int cacheMinutes = 5;
                // noCache=1 時略過快取讀取（但仍會寫入新快取）
                if (!noCache && _memoryCache.TryGetValue(cacheKey, out CouponStatsDto cachedStats))
                {
                    return Json(ApiCouponResponseDto<CouponStatsDto>.SuccessResult(cachedStats, "獲取統計資料成功(快取)"));
                }

                var totalCount = await _context.Coupons.CountAsync();
                var now = DateTime.Now;
                var activeCount = await _context.Coupons.CountAsync(c => c.StartAt <= now && c.ExpiredAt >= now);
                var expiredCount = await _context.Coupons.CountAsync(c => c.ExpiredAt < now);

                // 新增：來源統計
                var vendorCouponCount = await _context.Coupons.CountAsync(c => c.SellersId.HasValue && c.SellersId > 0);
                var platformCouponCount = totalCount - vendorCouponCount;

                // 取得所有優惠券的類型統計，並標準化為三種類型
                var rawTypeStats = await _context.Coupons
                    .GroupBy(c => c.DiscountType)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());

                // 將所有類型標準化為三種，並合併統計
                var normalizedTypeStats = new Dictionary<string, int>
                {
                    ["%數折扣"] = 0,
                    ["J幣回饋"] = 0,
                    ["滿減"] = 0
                };

                foreach (var kvp in rawTypeStats)
                {
                    var normalizedType = NormalizeDiscountType(kvp.Key);
                    normalizedTypeStats[normalizedType] += kvp.Value;
                }

                var statusStats = new Dictionary<string, int>
                {
                    ["啟用"] = activeCount,
                    ["已過期"] = expiredCount,
                    ["未開始"] = totalCount - activeCount - expiredCount
                };

                var sourceStats = new Dictionary<string, int>
                {
                    ["廠商優惠券"] = vendorCouponCount,
                    ["平台優惠券"] = platformCouponCount
                };

                var stats = new CouponStatsDto
                {
                    TotalCount = totalCount,
                    ActiveCount = activeCount,
                    ExpiredCount = expiredCount,
                    VendorCouponCount = vendorCouponCount,
                    PlatformCouponCount = platformCouponCount,
                    TypeStats = normalizedTypeStats, // 使用標準化後的統計資料
                    StatusStats = statusStats,
                    SourceStats = sourceStats
                };

                _memoryCache.Set(cacheKey, stats, TimeSpan.FromMinutes(cacheMinutes));

                return Json(ApiCouponResponseDto<CouponStatsDto>.SuccessResult(stats!, "獲取統計資料成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取統計資料失敗");
                return Json(ApiCouponResponseDto<object>.ErrorResult("獲取統計資料失敗：" + ex.Message));
            }
        }

        // 工具方法：獲取有效優惠券選項（供下拉選單使用）
        [HttpGet]
        [Route("Coupons/GetOptions")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetOptions(
            bool includeInactive = false,
            bool includeFuture = false,
            bool includeExpired = false)
        {
            try
            {
                _logger.LogInformation("GetOptions called with parameters: includeInactive={includeInactive}, includeFuture={includeFuture}, includeExpired={includeExpired}", 
                    includeInactive, includeFuture, includeExpired);

                var now = DateTime.Now;
                var today = DateTime.Today;              // 00:00:00

                var query = _context.Coupons.AsQueryable();

                // 先檢查總數
                var totalCoupons = await query.CountAsync();
                _logger.LogInformation("資料庫中總共有 {totalCoupons} 張優惠券", totalCoupons);

                // 篩選條件：是否包含未啟用的券
                if (!includeInactive)
                {
                    query = query.Where(c => c.IsActive);
                    var activeCount = await query.CountAsync();
                    _logger.LogInformation("啟用的優惠券數量: {activeCount}", activeCount);
                }

                // 篩選條件：是否包含未來開始的券
                if (!includeFuture)
                {
                    query = query.Where(c => c.StartAt <= now);
                    var startedCount = await query.CountAsync();
                    _logger.LogInformation("已開始的優惠券數量: {startedCount} (當前時間: {now})", startedCount, now);
                }

                // 若 ExpiredAt 存日期（時間為 00:00），用「>= today」才能整天有效
                if (!includeExpired)
                {
                    query = query.Where(c => c.ExpiredAt >= today);
                    var notExpiredCount = await query.CountAsync();
                    _logger.LogInformation("未過期的優惠券數量: {notExpiredCount} (今天: {today})", notExpiredCount, today);
                }

                // 不再依「適用對象/群組」做限制，讓等級預設券可以選所有有效券
                var options = await query
                    .OrderBy(c => c.Title)
                    .Select(c => new { 
                        Id = c.Id, 
                        Title = c.Title,
                        // 添加額外信息用於前端顯示
                        IsActive = c.IsActive,
                        IsStarted = c.StartAt <= now,
                        IsNotExpired = c.ExpiredAt >= today,
                        StartAt = c.StartAt,
                        ExpiredAt = c.ExpiredAt
                    })
                    .ToListAsync();

                _logger.LogInformation("最終篩選出 {count} 張優惠券", options.Count);

                // 如果沒有符合條件的優惠券，記錄詳細信息
                if (!options.Any())
                {
                    var debugInfo = await _context.Coupons
                        .Select(c => new 
                        { 
                            c.Id, 
                            c.Title, 
                            c.IsActive, 
                            c.StartAt, 
                            c.ExpiredAt,
                            IsStarted = c.StartAt <= now,
                            IsNotExpired = c.ExpiredAt >= today
                        })
                        .ToListAsync();

                    _logger.LogWarning("沒有找到符合條件的優惠券。所有優惠券詳情: {@debugInfo}", debugInfo);
                }

                return Json(new { 
                    success = true, 
                    data = options.Select(o => new { Id = o.Id, Title = o.Title }).ToList(),
                    // 添加篩選摘要信息
                    filterSummary = new {
                        total = totalCoupons,
                        filtered = options.Count,
                        includeInactive = includeInactive,
                        includeFuture = includeFuture,
                        includeExpired = includeExpired,
                        currentTime = now,
                        today = today
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取優惠券選項失敗");
                return Json(new { success = false, message = "獲取優惠券選項失敗：" + ex.Message });
            }
        }

        // 測試方法：獲取所有優惠券資料用於診斷
        [HttpGet]
        [Route("Coupons/GetOptionsDebug")]
        public async Task<IActionResult> GetOptionsDebug()
        {
            try
            {
                var now = DateTime.Now;
                var today = DateTime.Today;

                // 獲取所有優惠券的詳細信息
                var allCoupons = await _context.Coupons
                    .Select(c => new
                    {
                        c.Id,
                        c.Title,
                        c.IsActive,
                        c.StartAt,
                        c.ExpiredAt,
                        c.DiscountType,
                        c.DiscountAmount,
                        IsStarted = c.StartAt <= now,
                        IsNotExpired = c.ExpiredAt >= today,
                        DaysUntilExpiry = (c.ExpiredAt - today).Days
                    })
                    .OrderBy(c => c.Title)
                    .ToListAsync();

                var summary = new
                {
                    TotalCoupons = allCoupons.Count,
                    ActiveCoupons = allCoupons.Count(c => c.IsActive),
                    StartedCoupons = allCoupons.Count(c => c.IsStarted),
                    NotExpiredCoupons = allCoupons.Count(c => c.IsNotExpired),
                    ValidCoupons = allCoupons.Count(c => c.IsActive && c.IsStarted && c.IsNotExpired),
                    CurrentTime = now,
                    Today = today
                };

                return Json(new
                {
                    success = true,
                    summary = summary,
                    allCoupons = allCoupons
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取優惠券診斷資料失敗");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 工具方法：判斷是否為百分比折扣
        private bool IsPercentageDiscount(string discountType)
        {
            return discountType?.ToLower() switch
            {
                "折扣碼" or "percentage" or "%數折扣" => true,
                _ => false
            };
        }

        // 工具方法：標準化折扣類型
        private string NormalizeDiscountType(string discountType)
        {
            return discountType?.ToLower() switch
            {
                "折扣碼" or "percentage" or "%數折扣" => "%數折扣",
                "點數返還" or "j幣回饋" => "J幣回饋",
                "滿減" => "滿減",
                "免運費" => "滿減", // 免運費歸類為滿減
                _ => "滿減" // 其他類型預設為滿減
            };
        }

        // 將三大類映回資料庫可能的原始值（供查詢用）
        private static string[] RawTypesForNormalized(string normalized)
        {
            return normalized switch
            {
                "%數折扣" => new[] { "%數折扣", "折扣碼", "percentage" },
                "J幣回饋" => new[] { "J幣回饋", "點數返還" },
                "滿減" => new[] { "滿減", "免運費" },
                _ => new[] { normalized }
            };
        }

        // API: 獲取優惠券詳細資訊
        [HttpGet]
        [Route("GetCouponDetail/{id}")]
        public async Task<IActionResult> GetCouponDetail(int id)
        {
            try
            {
                var couponDetail = await _context.Coupons
                    .AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => new 
                    {
                        c.Id,
                        c.Title,
                        c.DiscountType,
                        c.DiscountAmount,
                        c.MinSpend,
                        c.StartAt,
                        c.ExpiredAt,
                        c.UsageLimit,
                        c.UsedCount,
                        c.ApplicableLevelId,
                        c.SellersId,
                        c.IsActive,
                        c.CreatedAt,
                        c.UpdatedAt,
                        // 廠商資訊
                        SellerRealName = c.Sellers != null ? c.Sellers.RealName : "",
                        SellerEmail = c.Sellers != null && c.Sellers.Members != null ? c.Sellers.Members.Email : "",
                        SellerStatus = c.Sellers != null ? c.Sellers.ApplicationStatus : "",
                        SellerIdNumber = c.Sellers != null ? c.Sellers.IdNumber : "",
                        SellerPhone = c.Sellers != null && c.Sellers.Members != null && c.Sellers.Members.Profile != null ? c.Sellers.Members.Profile.Name : "",
                        SellerJoinDate = c.Sellers != null ? c.Sellers.ApprovedAt : null,
                        // 等級資訊
                        ApplicableLevelName = c.ApplicableLevel != null ? c.ApplicableLevel.LevelName : ""
                    })
                    .FirstOrDefaultAsync();

                if (couponDetail == null)
                    return Json(ApiCouponResponseDto<object>.ErrorResult("找不到指定的優惠券"));

                // 獲取使用記錄
                var recentUsages = await _context.Orders
                    .Where(o => o.CouponId == id)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .Select(o => new CouponUsageDto
                    {
                        Id = o.Id,
                        MemberEmail = o.Member.Email ?? "未知會員",
                        UsedAt = o.CreatedAt,
                        OrderAmount = o.TotalAmount,
                        DiscountAmount = o.DiscountAmount ?? 0
                    })
                    .ToListAsync();

                // 計算統計資訊
                var totalSavings = await _context.Orders
                    .Where(o => o.CouponId == id)
                    .SumAsync(o => o.DiscountAmount ?? 0);

                var lastUsedAt = await _context.Orders
                    .Where(o => o.CouponId == id)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                var detailDto = new CouponDetailDto
                {
                    Id = couponDetail.Id,
                    Title = couponDetail.Title,
                    DiscountType = couponDetail.DiscountType,
                    DiscountTypeLabel = GetDiscountTypeLabel(couponDetail.DiscountType),
                    DiscountAmount = couponDetail.DiscountAmount,
                    MinSpend = couponDetail.MinSpend,
                    StartAt = couponDetail.StartAt,
                    CreatedAt = couponDetail.CreatedAt,
                    ExpiredAt = couponDetail.ExpiredAt,
                    UsageLimit = couponDetail.UsageLimit,
                    UsedCount = couponDetail.UsedCount,
                    ApplicableLevelId = couponDetail.ApplicableLevelId,
                    SellersId = couponDetail.SellersId,
                    IsActive = couponDetail.IsActive,
                    Status = GetStatus(couponDetail.StartAt, couponDetail.ExpiredAt),
                    StatusLabel = GetStatus(couponDetail.StartAt, couponDetail.ExpiredAt),
                    
                    // 廠商資訊
                    SellerRealName = couponDetail.SellerRealName,
                    SellerEmail = couponDetail.SellerEmail,
                    SellerStatus = couponDetail.SellerStatus,
                    SellerIdNumber = couponDetail.SellerIdNumber,
                    SellerPhone = couponDetail.SellerPhone,
                    SellerJoinDate = couponDetail.SellerJoinDate,
                    
                    // 其他資訊
                    ApplicableLevelName = couponDetail.ApplicableLevelName,
                    
                    // 統計資訊
                    TotalSavings = totalSavings,
                    LastUsedAt = lastUsedAt != default ? lastUsedAt : null,
                    TotalUses = couponDetail.UsedCount,
                    RecentUsages = recentUsages
                };

                return Json(ApiCouponResponseDto<CouponDetailDto>.SuccessResult(detailDto, "獲取優惠券詳情成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取優惠券詳情失敗: {Id}", id);
                return Json(ApiCouponResponseDto<object>.ErrorResult("獲取優惠券詳情失敗：" + ex.Message));
            }
        }

        // ✅ 修正：取得優惠券選項 API（供排程設定使用）
        [HttpGet]
        [Route("GetCouponOptions")]
        public async Task<IActionResult> GetCouponOptions()
        {
            try
            {
                _logger.LogInformation("GetCouponOptions called - loading coupons for schedule");

                var today = DateTime.Today; // 今日 00:00:00
                
                // 只篩選未過期的優惠券（移除 IsActive 限制）
                var coupons = await _context.Coupons
                    .Where(c => c.ExpiredAt >= today) // 只要未過期就顯示
                    .Select(c => new { 
                        Id = c.Id, 
                        Title = c.Title, 
                        c.StartAt, 
                        c.ExpiredAt,
                        c.IsActive
                    })
                    .OrderBy(c => c.Title)
                    .ToListAsync();

                _logger.LogInformation("GetCouponOptions loaded {Count} unexpired coupons", coupons.Count);

                return Json(new { 
                    success = true, 
                    data = coupons.Select(c => new { 
                        id = c.Id, 
                        title = c.Title,
                        startAt = c.StartAt,
                        expiredAt = c.ExpiredAt,
                        isActive = c.IsActive,
                        // 添加狀態資訊供前端參考
                        status = GetCouponStatusForDisplay(c.StartAt, c.ExpiredAt, c.IsActive)
                    }).ToList() 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得優惠券選項失敗");
                return Json(new { success = false, message = "載入優惠券資料失敗: " + ex.Message, detail = ex.ToString() });
            }
        }

        // ✅ 新增：取得優惠券顯示狀態的輔助方法
        private string GetCouponStatusForDisplay(DateTime startAt, DateTime expiredAt, bool isActive)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            
            // 已過期
            if (expiredAt < today)
                return "已過期";
            
            // 未開始
            if (startAt > now)
                return isActive ? "未開始(已啟用)" : "未開始(未啟用)";
            
            // 進行中
            return isActive ? "進行中" : "進行中(未啟用)";
        }

        // ✅ 新增：取得會員等級選項 API（供排程設定使用）
        [HttpGet]
        [Route("GetMemberLevelOptions")]
        public async Task<IActionResult> GetMemberLevelOptions()
        {
            try
            {
                _logger.LogInformation("GetMemberLevelOptions called - loading member levels for schedule");

                var levels = await _context.MembershipLevels
                    .Where(l => l.IsActive)
                    .Select(l => new { 
                        id = l.Id.ToString(), 
                        name = l.LevelName 
                    })
                    .OrderBy(l => l.id)
                    .ToListAsync();

                _logger.LogInformation("GetMemberLevelOptions loaded {Count} levels", levels.Count);

                return Json(new { 
                    success = true, 
                    data = levels
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級選項失敗");
                return Json(new { success = false, message = "載入會員等級資料失敗: " + ex.Message });
            }
        }

        // ✅ 新增：診斷優惠券載入問題的 API
        [HttpGet]
        [Route("DiagnoseCouponLoading")]
        public async Task<IActionResult> DiagnoseCouponLoading()
        {
            try
            {
                _logger.LogInformation("開始診斷優惠券載入問題");

                // 1. 檢查總優惠券數量
                var totalCount = await _context.Coupons.CountAsync();
                _logger.LogInformation("資料庫中總共有 {totalCount} 張優惠券", totalCount);

                if (totalCount == 0)
                {
                    return Json(new { 
                        success = false, 
                        message = "資料庫中沒有任何優惠券", 
                        diagnosis = "請先在優惠券管理中新增優惠券" 
                    });
                }

                // 2. 檢查啟用的優惠券
                var activeCount = await _context.Coupons.CountAsync(c => c.IsActive);
                _logger.LogInformation("啟用的優惠券數量: {activeCount}", activeCount);

                // 3. 檢查時間範圍
                var now = DateTime.Now;
                var startedCount = await _context.Coupons.CountAsync(c => c.StartAt <= now);
                var notExpiredCount = await _context.Coupons.CountAsync(c => c.ExpiredAt >= now.Date);

                _logger.LogInformation("已開始的優惠券: {startedCount}, 未過期的優惠券: {notExpiredCount}", startedCount, notExpiredCount);

                // 4. 詳細分析每張優惠券
                var coupons = await _context.Coupons
                    .Select(c => new {
                        c.Id,
                        c.Title,
                        c.IsActive,
                        c.StartAt,
                        c.ExpiredAt,
                        IsStarted = c.StartAt <= now,
                        IsNotExpired = c.ExpiredAt >= now.Date,
                        IsValid = c.IsActive && c.StartAt <= now && c.ExpiredAt >= now.Date
                    })
                    .ToListAsync();

                var validCount = coupons.Count(c => c.IsValid);

                return Json(new {
                    success = true,
                    summary = new {
                        totalCoupons = totalCount,
                        activeCoupons = activeCount,
                        startedCoupons = startedCount,
                        notExpiredCoupons = notExpiredCount,
                        validCoupons = validCount,
                        currentTime = now,
                        today = now.Date
                    },
                    coupons = coupons,
                    recommendation = validCount > 0 
                        ? $"發現 {validCount} 張有效優惠券，應該可以正常載入"
                        : "沒有找到有效的優惠券，請檢查優惠券的啟用狀態和時間設定"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "診斷優惠券載入時發生錯誤");
                return Json(new { 
                    success = false, 
                    message = "診斷時發生錯誤: " + ex.Message,
                    stackTrace = ex.ToString()
                });
            }
        }
    }
}