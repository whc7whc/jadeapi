using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.DTO;
using Team.API.Services;

namespace Team.API.Controllers
{
    /// <summary>
    /// 會員相關 API 控制器
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MembersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MembersController> _logger;
        private readonly IPointsService _pointsService;
        private readonly IMemberLevelService _memberLevelService;

        public MembersController(
            AppDbContext context, 
            ILogger<MembersController> logger, 
            IPointsService pointsService,
            IMemberLevelService memberLevelService)
        {
            _context = context;
            _logger = logger;
            _pointsService = pointsService;
            _memberLevelService = memberLevelService;
        }

        #region 優惠券相關端點（暫時保留功能）

        /// <summary>
        /// 獲得指定會員的所有優惠券清單
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。日後將查詢方法升級為動態，
        /// 升級方案是改用 claims 的 id 改為對 /api/Members/me/MemberCoupons，
        /// 但核心業務邏輯與 DTO 保持不變。
        /// 
        /// 查詢參數：
        /// - activeOnly（bool，預設 false）：只回傳「目前可用」的優惠券
        /// - status（string，可能 active|used|expired|cancelled）
        /// - page（int，預設 1；若小於1則為1）
        /// - pageSize（int，預設 20；最大 100）
        /// 
        /// 「目前可用」定義（同時滿足）：
        /// - Member_Coupons.Status = 'active'
        /// - Coupons.Is_Active = 1
        /// - 現在時間介於 Coupons.Start_At 與 Coupons.Expired_At（含邊界）
        /// - 若 Coupons.Usage_Limit 有值：Coupons.Used_Count < Coupons.Usage_Limit
        /// 
        /// 排序：主要按 Coupons.Expired_At 時間昇序，與次要按 Status='active' 優先
        /// 
        /// 查詢請求：
        /// - GET /api/Members/123/MemberCoupons?activeOnly=true&page=1&pageSize=10
        /// - GET /api/Members/123/MemberCoupons?status=used&page=2
        /// - GET /api/Members/123/MemberCoupons?status=active&activeOnly=false
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="activeOnly">是否只回傳目前可用的優惠券</param>
        /// <param name="status">狀態篩選</param>
        /// <param name="page">頁碼</param>
        /// <param name="pageSize">每頁筆數</param>
        /// <returns>分頁後會員優惠券清單</returns>
        [HttpGet("{memberId}/MemberCoupons")]
        public async Task<ActionResult<PagedResponseDto<MyMemberCouponDto>>> GetMemberCoupons(
            int memberId,
            [FromQuery] bool activeOnly = false,
            [FromQuery] string status = "",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // 參數驗證
                if (memberId <= 0)
                {
                    return BadRequest(new PagedResponseDto<MyMemberCouponDto>
                    {
                        Success = false,
                        Message = "會員ID必須大於 0",
                        Data = new List<MyMemberCouponDto>(),
                        TotalCount = 0,
                        CurrentPage = 1,
                        ItemsPerPage = 20,
                        TotalPages = 0
                    });
                }

                // 限制參數
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                _logger.LogInformation("開始查詢會員 {MemberId} 優惠券，activeOnly: {ActiveOnly}, status: {Status}, page: {Page}, pageSize: {PageSize}",
                    memberId, activeOnly, status, page, pageSize);

                // 封裝動態查詢方法，便於之後改用 claims 升級
                var result = await GetMemberCouponsInternal(memberId, activeOnly, status, page, pageSize);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢會員 {MemberId} 優惠券失敗", memberId);
                return StatusCode(500, new PagedResponseDto<MyMemberCouponDto>
                {
                    Success = false,
                    Message = "查詢失敗：" + ex.Message,
                    Data = new List<MyMemberCouponDto>(),
                    TotalCount = 0,
                    CurrentPage = 1,
                    ItemsPerPage = 20,
                    TotalPages = 0
                });
            }
        }

        /// <summary>
        /// 內部查詢方法 - 封裝動態，便於之後改用 JWT claims 升級
        /// </summary>
        private async Task<PagedResponseDto<MyMemberCouponDto>> GetMemberCouponsInternal(
            int memberId, bool activeOnly, string status, int page, int pageSize)
        {
            var now = DateTime.Now;

            // 建立基本查詢：MemberCoupons JOIN Coupons，只回傳該會員的優惠券
            var query = _context.MemberCoupons
                .Where(mc => mc.MemberId == memberId)
                .Include(mc => mc.Coupon)
                    .ThenInclude(c => c.Sellers)
                        .ThenInclude(s => s.Members)
                .AsNoTracking();

            // activeOnly 篩選：只回傳「目前可用」的優惠券
            if (activeOnly)
            {
                query = query.Where(mc =>
                    mc.Status == "active" &&
                    mc.Coupon.IsActive &&
                    now >= mc.Coupon.StartAt &&
                    now <= mc.Coupon.ExpiredAt &&
                    (mc.Coupon.UsageLimit == null || mc.Coupon.UsedCount < mc.Coupon.UsageLimit));
            }

            // status 篩選
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(mc => mc.Status.ToLower() == status.ToLower());
            }

            // 計算總數
            var total = await query.CountAsync();

            // 排序：主要按 Coupons.ExpiredAt 時間昇序，與次要按 Status='active' 優先
            var memberCoupons = await query
                .OrderBy(mc => mc.Coupon.ExpiredAt)
                .ThenByDescending(mc => mc.Status == "active" ? 1 : 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 轉換為 DTO
            var dtos = memberCoupons.Select(mc => 
            {
                var now = DateTime.Now;
                var currentStatus = mc.Status ?? "";
                
                // 動態計算正確的狀態
                string actualStatus;
                if (mc.UsedAt.HasValue || currentStatus.ToLower() == "used")
                {
                    actualStatus = "used";
                }
                else if (mc.Coupon.ExpiredAt < now)
                {
                    actualStatus = "expired";  // ?? 關鍵修改：過期檢查
                }
                else if (!mc.Coupon.IsActive)
                {
                    actualStatus = "inactive";
                }
                else if (mc.Coupon.StartAt > now)
                {
                    actualStatus = "pending";
                }
                else if (mc.Coupon.UsageLimit.HasValue && mc.Coupon.UsedCount >= mc.Coupon.UsageLimit.Value)
                {
                    actualStatus = "exhausted";
                }
                else
                {
                    actualStatus = "active";
                }

                return new MyMemberCouponDto
                {
                    // 會員優惠券資訊（Member_Coupons）
                    MemberCouponId = mc.Id,
                    Status = actualStatus,  // 使用計算後的狀態
                    AssignedAt = mc.AssignedAt,
                    UsedAt = mc.UsedAt,
                    OrderId = mc.OrderId,
                    VerificationCode = mc.VerificationCode ?? "",

                    // 優惠券定義資訊（Coupons）
                    CouponId = mc.Coupon.Id,
                    Title = mc.Coupon.Title ?? "",
                    DiscountType = mc.Coupon.DiscountType ?? "",
                    DiscountAmount = mc.Coupon.DiscountAmount,
                    MinSpend = mc.Coupon.MinSpend,
                    StartAt = mc.Coupon.StartAt,
                    ExpiredAt = mc.Coupon.ExpiredAt,
                    IsActive = mc.Coupon.IsActive,
                    UsageLimit = mc.Coupon.UsageLimit,
                    UsedCount = mc.Coupon.UsedCount,
                    SellersId = mc.Coupon.SellersId,
                    CategoryId = mc.Coupon.CategoryId,
                    ApplicableLevelId = mc.Coupon.ApplicableLevelId,

                    // 補充資訊：SellerName（如果有廠商可以顯示，否則回 null）
                    SellerName = mc.Coupon.Sellers?.RealName
                };
            }).ToList();

            _logger.LogInformation("會員 {MemberId} 查詢完成，總數: {Total}，傳回: {PageCount}",
                memberId, total, dtos.Count);

            return new PagedResponseDto<MyMemberCouponDto>
            {
                Success = true,
                Message = "查詢會員優惠券成功",
                Data = dtos,
                TotalCount = total,
                CurrentPage = page,
                ItemsPerPage = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            };
        }

        #endregion

        #region 點數相關端點

        /// <summary>
        /// 查詢會員點數餘額
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。
        /// 日後將改為 /api/Members/me/Points/Balance（從 JWT claims 獲得 memberId）。
        /// 
        /// 查詢：Member_Stats.Total_Points（整數），若查無資料回 balance=0。
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>會員點數餘額資訊</returns>
        [HttpGet("{memberId}/Points/Balance")]
        public async Task<ActionResult<PointsBalanceDto>> GetPointsBalance(int memberId)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                var balance = await _pointsService.GetBalanceAsync(memberId);
                if (balance == null)
                {
                    return NotFound("找不到指定會員");
                }

                return Ok(balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢會員 {MemberId} 點數餘額失敗", memberId);
                return StatusCode(500, "查詢點數餘額失敗");
            }
        }

        /// <summary>
        /// 查詢會員點數歷史記錄（分頁 + 篩選）
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。
        /// 
        /// 篩選：
        /// - type（可選；可能值：signin|used|refund|earned|expired|adjustment）
        /// - dateFrom/dateTo（以 Created_At 篩選）
        /// 
        /// 排序：Created_At DESC
        /// 
        /// 回傳：分頁後，每項包含：Id, Type, Amount, Note, Expired_At, Transaction_Id, Created_At, Verification_Code
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="type">類型篩選</param>
        /// <param name="dateFrom">開始日期</param>
        /// <param name="dateTo">結束日期</param>
        /// <param name="page">頁碼</param>
        /// <param name="pageSize">每頁筆數</param>
        /// <returns>分頁後點數歷史記錄</returns>
        [HttpGet("{memberId}/Points/History")]
        public async Task<ActionResult<PagedResponseDto<PointHistoryItemDto>>> GetPointsHistory(
            int memberId,
            [FromQuery] string? type = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                var query = new PointsHistoryQueryDto
                {
                    Type = type,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    Page = Math.Max(1, page),
                    PageSize = Math.Clamp(pageSize, 1, 100)
                };

                var result = await _pointsService.GetHistoryAsync(memberId, query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢會員 {MemberId} 點數歷史失敗", memberId);
                return StatusCode(500, "查詢點數歷史失敗");
            }
        }

        /// <summary>
        /// 加點（Earn / 調整）
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。
        /// 
        /// 邏輯：
        /// - amount > 0；type 必須在白名單（earned 或 adjustment）
        /// - 冪等：若 verificationCode 已存在於 Points_Log 就重複回傳成功結果（冪等性）
        /// - 流程：新增 Points_Log（+amount），同步安全增加 Member_Stats.Total_Points
        /// - 失敗紀錄 Points_Log_Error
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="request">加點請求</param>
        /// <returns>點數異動結果</returns>
        [HttpPost("{memberId}/Points/Earn")]
        public async Task<ActionResult<PointsMutationResultDto>> EarnPoints(int memberId, [FromBody] PointsEarnRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "輸入參數有誤", Errors = errors });
                }

                var result = await _pointsService.EarnPointsAsync(memberId, request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "會員 {MemberId} 加點失敗", memberId);
                return StatusCode(500, "加點作業失敗");
            }
        }

        /// <summary>
        /// 扣點（Use）
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。
        /// 
        /// 檢查：
        /// - 讀 Member_Stats.Total_Points，檢查可扣除 amount
        /// - verificationCode 冪等性處理（若存在，重複回傳既有結果）
        /// 
        /// 流程：
        /// - 新增 Points_Log（type=used，amount=正整數保持，註記同時帶上 direction:"debit"）
        /// - 原子更新：UPDATE Member_Stats SET Total_Points = Total_Points - @amount WHERE Member_Id=@memberId AND Total_Points >= @amount
        /// - 若 UPDATE 受影響 ≠ 1 回 409/400 並紀錄 Points_Log_Error
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="request">扣點請求</param>
        /// <returns>點數異動結果</returns>
        [HttpPost("{memberId}/Points/Use")]
        public async Task<ActionResult<PointsMutationResultDto>> UsePoints(int memberId, [FromBody] PointsUseRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "輸入參數有誤", Errors = errors });
                }

                var result = await _pointsService.UsePointsAsync(memberId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("餘額不足"))
            {
                return Conflict(new { Message = ex.Message, Code = "INSUFFICIENT_BALANCE" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "會員 {MemberId} 扣點失敗", memberId);
                return StatusCode(500, "扣點作業失敗");
            }
        }

        /// <summary>
        /// 回補（Refund）
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。
        /// 
        /// 冪等：verificationCode 冪等
        /// 流程：寫 Points_Log（refund），同步加回 Total_Points
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="request">回補請求</param>
        /// <returns>點數異動結果</returns>
        [HttpPost("{memberId}/Points/Refund")]
        public async Task<ActionResult<PointsMutationResultDto>> RefundPoints(int memberId, [FromBody] PointsRefundRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "輸入參數有誤", Errors = errors });
                }

                var result = await _pointsService.RefundPointsAsync(memberId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "會員 {MemberId} 點數退款失敗", memberId);
                return StatusCode(500, "點數退款作業失敗");
            }
        }

        /// <summary>
        /// 到期妞點（排程用，用於程序）
        /// 
        /// ?? 暫時方案：目前僅測時期，存在 IDOR 風險。
        /// 
        /// 專為最需「過期妞點」需求時使用：寫 expired 紀錄，並同步減少 Total_Points（同 Use 相同的安全 UPDATE）
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="request">到期扣點請求</param>
        /// <returns>點數異動結果</returns>
        [HttpPost("{memberId}/Points/Expire")]
        public async Task<ActionResult<PointsMutationResultDto>> ExpirePoints(int memberId, [FromBody] PointsExpireRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "輸入參數有誤", Errors = errors });
                }

                var result = await _pointsService.ExpirePointsAsync(memberId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "會員 {MemberId} 點數到期扣點失敗", memberId);
                return StatusCode(500, "點數到期扣點作業失敗");
            }
        }

        #endregion

        #region 簽到相關端點（新增）

        /// <summary>
        /// 取得今日簽到資訊
        /// 
        /// ?? 暫時方案：使用 memberId 路徑參數，存在 IDOR 風險
        /// 
        /// 功能：
        /// - 檢查今天是否已簽到（以伺服器日曆日為準）
        /// - 計算連續簽到天數
        /// - 計算今日獎勵（小數顯示值）
        /// - 回傳伺服器時間和換算比例
        /// 
        /// 簽到獎勵規則：
        /// - 連續 1-7 天對應 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 1.0 JCoin
        /// - 第 8 天重新循環到 0.1 JCoin
        /// - 內部儲存為整數 (Amount = 顯示值 × 10)
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>今日簽到資訊</returns>
        [HttpGet("{memberId}/Checkin/Info")]
        public async Task<ActionResult<CheckinInfoDto>> GetCheckinInfo(int memberId)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                var info = await _pointsService.GetCheckinInfoAsync(memberId);
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員 {MemberId} 簽到資訊失敗", memberId);
                return StatusCode(500, "取得簽到資訊失敗");
            }
        }

        /// <summary>
        /// 執行簽到
        /// 
        /// ?? 暫時方案：使用 memberId 路徑參數，存在 IDOR 風險
        /// 
        /// 功能：
        /// - 冪等性：今天重複簽到會回傳相同結果，不重複給點
        /// - 簽到唯一碼：CHK-YYYYMMDD-{memberId}
        /// - 獎勵計算：根據連續天數循環 1-7 天
        /// - 原子更新：安全更新 Member_Stats 餘額
        /// - 交易保護：失敗自動回滾
        /// 
        /// Request Body：為了相容性接受，但以路徑 memberId 為準
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="request">簽到請求（可選）</param>
        /// <returns>簽到結果</returns>
        [HttpPost("{memberId}/Checkin")]
        public async Task<ActionResult<CheckinResultDto>> PerformCheckin(int memberId, [FromBody] CheckinRequestDto? request = null)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("會員ID必須大於 0");
                }

                // 為了相容性接受 request body，但實際以路徑參數的 memberId 為準
                var result = await _pointsService.PerformCheckinAsync(memberId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "會員 {MemberId} 簽到失敗", memberId);

                // 檢查是否為重複簽到的衝突
                if (ex.Message.Contains("CHK-") || ex.Message.Contains("已簽到"))
                {
                    return Conflict(new { Message = "今日已完成簽到", Code = "ALREADY_CHECKED_IN" });
                }

                return StatusCode(500, "簽到作業失敗");
            }
        }

        #endregion

        #region 會員等級 Summary 與升級 API

        /// <summary>
        /// 獲得會員等級 Summary
        /// 
        /// ?? 暫時做法警告：目前僅測時期，存在 IDOR 風險
        /// 下一步要改為 /api/Members/me/Level/Summary 並透過 JWT claims 獲得 memberId
        /// 
        /// 回傳內容：
        /// - 目前等級、下一級門檻、已花費金額、距離進度
        /// - 升級判定依據：Membership_Levels.Required_Amount 與會員累計花費金額
        /// - 已花費金額來源：Member_Stats.Total_Spent
        /// 
        /// 查詢請求：GET /api/Members/123/Level/Summary
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>會員等級摘要資訊</returns>
        [HttpGet("{memberId}/Level/Summary")]
        public async Task<ActionResult<MemberLevelSummaryDto>> GetMemberLevelSummary(int memberId)
        {
            try
            {
                // 驗證 memberId
                if (!int.TryParse(memberId.ToString(), out var validMemberId) || validMemberId <= 0)
                {
                    return BadRequest(new { message = "無效的會員ID" });
                }

                _logger.LogInformation("獲得會員 {MemberId} 等級摘要", memberId);

                var summary = await _memberLevelService.GetMemberLevelSummaryAsync(memberId);
                
                if (summary == null)
                {
                    return NotFound(new { message = "找不到指定會員或等級資料" });
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲得會員 {MemberId} 等級摘要失敗", memberId);
                return StatusCode(500, new { message = "獲得等級摘要失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 重新計算已花費金額並同步升級
        /// 
        /// ?? 暫時做法警告：目前僅測時期，存在 IDOR 風險
        /// 下一步要改為 /api/Members/me/Level/Recalculate 並透過 JWT claims 獲得 memberId
        /// 
        /// 邏輯：
        /// 1. 從 Orders 累積該會員的「實際完成訂單」金額（paid|completed 狀態）
        /// 2. 若查詢到 Orders，更新 Member_Stats.Total_Spent 並重算
        /// 3. 依新的 totalSpent 與已定義等級（按 Is_Active=1 的最高適用為準，按 totalSpent）
        /// 4. 若與 Member_Stats.Current_Level_Id 不同：更新並回傳 levelUp: true
        /// 5. 原子更新 Member_Stats（Total_Spent、Current_Level_Id、Updated_At）
        /// 
        /// 交易控制：包在交易中寫 Member_Stats，避免競用情況
        /// 
        /// 查詢請求：POST /api/Members/123/Level/Recalculate
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>重新計算結果，包含 levelUp 標記和新的等級資訊</returns>
        [HttpPost("{memberId}/Level/Recalculate")]
        public async Task<ActionResult<RecalculateResultDto>> RecalculateMemberLevel(int memberId)
        {
            try
            {
                // 驗證 memberId
                if (!int.TryParse(memberId.ToString(), out var validMemberId) || validMemberId <= 0)
                {
                    return BadRequest(new { message = "無效的會員ID" });
                }

                _logger.LogInformation("開始重新計算會員 {MemberId} 等級", memberId);

                var result = await _memberLevelService.RecalculateMemberLevelAsync(memberId);
                
                if (result == null)
                {
                    return NotFound(new { message = "找不到指定會員或資料" });
                }

                var message = result.LevelUp 
                    ? $"等級重計完成，恭喜升級！從 {result.OldLevel?.Name} 升級至 {result.NewLevel?.Name}"
                    : "等級重計完成，等級無異動";

                _logger.LogInformation("會員 {MemberId} 等級重計完成，升級狀況：{LevelUp}", memberId, result.LevelUp);

                return Ok(new 
                { 
                    message = message,
                    data = result 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新計算會員 {MemberId} 等級失敗", memberId);
                return StatusCode(500, new { message = "重新計算等級失敗", error = ex.Message });
            }
        }

        #endregion
    }
}