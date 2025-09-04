using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.DTO;
using Team.API.Services;

namespace Team.API.Controllers
{
    /// <summary>
    /// Members API Controller
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

        #region Member Coupons Management (temporarily kept for backward compatibility)

        /// <summary>
        /// Get all coupons for a specific member
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability. Should use JWT claims for authentication.
        /// Recommendation: Use /api/Members/me/MemberCoupons endpoint with user identity from JWT token.
        /// 
        /// Query parameters:
        /// - activeOnly (bool, default false): Only return currently usable coupons
        /// - status (string, options: active|used|expired|cancelled)
        /// - page (int, default 1, minimum 1)
        /// - pageSize (int, default 20, maximum 100)
        /// 
        /// "Currently usable" definition (all conditions must be met):
        /// - Member_Coupons.Status = 'active'
        /// - Coupons.Is_Active = 1
        /// - Current time is between Coupons.Start_At and Coupons.Expired_At
        /// - If Coupons.Usage_Limit has value: Coupons.Used_Count < Coupons.Usage_Limit
        /// 
        /// Sorting: Primary by Coupons.Expired_At ASC, Secondary by Status='active' first
        /// 
        /// Example requests:
        /// - GET /api/Members/123/MemberCoupons?activeOnly=true&page=1&pageSize=10
        /// - GET /api/Members/123/MemberCoupons?status=used&page=2
        /// - GET /api/Members/123/MemberCoupons?status=active&activeOnly=false
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="activeOnly">Whether to return only currently usable coupons</param>
        /// <param name="status">Status filter</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>Paginated list of member coupons</returns>
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
                // Parameter validation
                if (memberId <= 0)
                {
                    return BadRequest(new PagedResponseDto<MyMemberCouponDto>
                    {
                        Success = false,
                        Message = "Member ID must be greater than 0",
                        Data = new List<MyMemberCouponDto>(),
                        TotalCount = 0,
                        CurrentPage = 1,
                        ItemsPerPage = 20,
                        TotalPages = 0
                    });
                }

                // Normalize parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                _logger.LogInformation("Starting query for member {MemberId} coupons: activeOnly: {ActiveOnly}, status: {Status}, page: {Page}, pageSize: {PageSize}",
                    memberId, activeOnly, status, page, pageSize);

                // Use internal method to avoid token validation complexity
                var result = await GetMemberCouponsInternal(memberId, activeOnly, status, page, pageSize);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query coupons for member {MemberId}", memberId);
                return StatusCode(500, new PagedResponseDto<MyMemberCouponDto>
                {
                    Success = false,
                    Message = "Query failed: " + ex.Message,
                    Data = new List<MyMemberCouponDto>(),
                    TotalCount = 0,
                    CurrentPage = 1,
                    ItemsPerPage = 20,
                    TotalPages = 0
                });
            }
        }

        /// <summary>
        /// Internal query method - handles actual query logic without token validation
        /// </summary>
        private async Task<PagedResponseDto<MyMemberCouponDto>> GetMemberCouponsInternal(
            int memberId, bool activeOnly, string status, int page, int pageSize)
        {
            var now = DateTime.Now;

            // Build base query: MemberCoupons JOIN Coupons, only return this member's coupons
            var query = _context.MemberCoupons
                .Where(mc => mc.MemberId == memberId)
                .Include(mc => mc.Coupon)
                    .ThenInclude(c => c.Sellers)
                        .ThenInclude(s => s.Members)
                .AsNoTracking();

            // activeOnly filter: only return currently usable coupons
            if (activeOnly)
            {
                query = query.Where(mc =>
                    mc.Status == "active" &&
                    mc.Coupon.IsActive &&
                    now >= mc.Coupon.StartAt &&
                    now <= mc.Coupon.ExpiredAt &&
                    (mc.Coupon.UsageLimit == null || mc.Coupon.UsedCount < mc.Coupon.UsageLimit));
            }

            // status filter
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(mc => mc.Status.ToLower() == status.ToLower());
            }

            // Calculate total count
            var total = await query.CountAsync();

            // Sorting: Primary by Coupons.ExpiredAt ASC, Secondary by Status='active' first
            var memberCoupons = await query
                .OrderBy(mc => mc.Coupon.ExpiredAt)
                .ThenByDescending(mc => mc.Status == "active" ? 1 : 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Convert to DTO
            var dtos = memberCoupons.Select(mc => 
            {
                var now = DateTime.Now;
                var currentStatus = mc.Status ?? "";
                
                // Dynamically calculate correct status
                string actualStatus;
                if (mc.UsedAt.HasValue || currentStatus.ToLower() == "used")
                {
                    actualStatus = "used";
                }
                else if (mc.Coupon.ExpiredAt < now)
                {
                    actualStatus = "expired";  // Expired check
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
                    // Member coupon information (Member_Coupons)
                    MemberCouponId = mc.Id,
                    Status = actualStatus,  // Use calculated status
                    AssignedAt = mc.AssignedAt,
                    UsedAt = mc.UsedAt,
                    OrderId = mc.OrderId,
                    VerificationCode = mc.VerificationCode ?? "",

                    // Coupon definition information (Coupons)
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

                    // Additional information: SellerName (if vendor coupon available, otherwise return null)
                    SellerName = mc.Coupon.Sellers?.RealName
                };
            }).ToList();

            _logger.LogInformation("Member {MemberId} query completed, total: {Total}, returned: {PageCount}",
                memberId, total, dtos.Count);

            return new PagedResponseDto<MyMemberCouponDto>
            {
                Success = true,
                Message = "Member coupons query successful",
                Data = dtos,
                TotalCount = total,
                CurrentPage = page,
                ItemsPerPage = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            };
        }

        #endregion

        #region Points Management API

        /// <summary>
        /// Query member points balance
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability.
        /// Recommendation: Use /api/Members/me/Points/Balance (get memberId from JWT claims).
        /// 
        /// Query: Member_Stats.Total_Points (integer), return balance=0 if no record found.
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <returns>Member points balance information</returns>
        [HttpGet("{memberId}/Points/Balance")]
        public async Task<ActionResult<PointsBalanceDto>> GetPointsBalance(int memberId)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                var balance = await _pointsService.GetBalanceAsync(memberId);
                if (balance == null)
                {
                    return NotFound("Member not found");
                }

                return Ok(balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query points balance for member {MemberId}", memberId);
                return StatusCode(500, "Failed to query points balance");
            }
        }

        /// <summary>
        /// Query member points transaction history (paged + filtered)
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability.
        /// 
        /// Filters:
        /// - type (optional values: signin|used|refund|earned|expired|adjustment)
        /// - dateFrom/dateTo (filter by Created_At)
        /// 
        /// Sorting: Created_At DESC
        /// 
        /// Returns: Paginated list, each item includes: Id, Type, Amount, Note, Expired_At, Transaction_Id, Created_At, Verification_Code
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="type">Type filter</param>
        /// <param name="dateFrom">Start date</param>
        /// <param name="dateTo">End date</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>Paginated points transaction history</returns>
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
                    return BadRequest("Member ID must be greater than 0");
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
                _logger.LogError(ex, "Failed to query points history for member {MemberId}", memberId);
                return StatusCode(500, "Failed to query points history");
            }
        }

        /// <summary>
        /// Add points (Earn / Adjustment)
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability.
        /// 
        /// Validation:
        /// - amount > 0; type must be in whitelist (earned or adjustment)
        /// - Idempotency: If verificationCode already exists in Points_Log, return existing result
        /// - Process: Add Points_Log (+amount), and synchronously increase Member_Stats.Total_Points
        /// - Error handling: Log errors to Points_Log_Error
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="request">Add points request</param>
        /// <returns>Points mutation result</returns>
        [HttpPost("{memberId}/Points/Earn")]
        public async Task<ActionResult<PointsMutationResultDto>> EarnPoints(int memberId, [FromBody] PointsEarnRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "Input parameter error", Errors = errors });
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
                _logger.LogError(ex, "Failed to earn points for member {MemberId}", memberId);
                return StatusCode(500, "Points earning operation failed");
            }
        }

        /// <summary>
        /// Deduct points (Use)
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability.
        /// 
        /// Validation:
        /// - Read Member_Stats.Total_Points, check sufficient balance for amount
        /// - verificationCode idempotency handling (if exists, return previous result)
        /// 
        /// Process:
        /// - Add Points_Log (type=used, amount=positive integer kept, may include direction:"debit")
        /// - Atomic update: UPDATE Member_Stats SET Total_Points = Total_Points - @amount WHERE Member_Id=@memberId AND Total_Points >= @amount
        /// - If UPDATE affected rows != 1, return 409/400 and log to Points_Log_Error
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="request">Use points request</param>
        /// <returns>Points mutation result</returns>
        [HttpPost("{memberId}/Points/Use")]
        public async Task<ActionResult<PointsMutationResultDto>> UsePoints(int memberId, [FromBody] PointsUseRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "Input parameter error", Errors = errors });
                }

                var result = await _pointsService.UsePointsAsync(memberId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient balance"))
            {
                return Conflict(new { Message = ex.Message, Code = "INSUFFICIENT_BALANCE" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to use points for member {MemberId}", memberId);
                return StatusCode(500, "Points usage operation failed");
            }
        }

        /// <summary>
        /// Refund points
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability.
        /// 
        /// Validation: verificationCode idempotency
        /// Process: Write Points_Log (refund), and add back Total_Points
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="request">Refund points request</param>
        /// <returns>Points mutation result</returns>
        [HttpPost("{memberId}/Points/Refund")]
        public async Task<ActionResult<PointsMutationResultDto>> RefundPoints(int memberId, [FromBody] PointsRefundRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "Input parameter error", Errors = errors });
                }

                var result = await _pointsService.RefundPointsAsync(memberId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refund points for member {MemberId}", memberId);
                return StatusCode(500, "Points refund operation failed");
            }
        }

        /// <summary>
        /// Expire points (scheduled process, admin operation)
        /// 
        /// SECURITY NOTE: Currently has IDOR vulnerability.
        /// 
        /// Used when implementing "point expiration" requirements: Write expired transaction, and synchronously deduct Total_Points (same safe UPDATE as Use)
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="request">Expire points request</param>
        /// <returns>Points mutation result</returns>
        [HttpPost("{memberId}/Points/Expire")]
        public async Task<ActionResult<PointsMutationResultDto>> ExpirePoints(int memberId, [FromBody] PointsExpireRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "Input parameter error", Errors = errors });
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
                _logger.LogError(ex, "Failed to expire points for member {MemberId}", memberId);
                return StatusCode(500, "Points expiration operation failed");
            }
        }

        #endregion

        #region Check-in Feature API (New)

        /// <summary>
        /// Get today's check-in information
        /// 
        /// SECURITY NOTE: Using memberId parameter, has IDOR vulnerability
        /// 
        /// Features:
        /// - Check if already checked in today (by date, unique per day)
        /// - Calculate consecutive check-in days
        /// - Calculate today's reward (calculated reward value)
        /// - Return server time and unit information
        /// 
        /// Check-in reward rules:
        /// - Consecutive 1-7 days: 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 1.0 JCoin
        /// - Day 8+ restart cycle to 0.1 JCoin
        /// - Database storage (Amount = display value * 10)
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <returns>Today's check-in information</returns>
        [HttpGet("{memberId}/Checkin/Info")]
        public async Task<ActionResult<CheckinInfoDto>> GetCheckinInfo(int memberId)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                var info = await _pointsService.GetCheckinInfoAsync(memberId);
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get check-in info for member {MemberId}", memberId);
                return StatusCode(500, "Failed to get check-in information");
            }
        }

        /// <summary>
        /// Perform check-in
        /// 
        /// SECURITY NOTE: Using memberId parameter, has IDOR vulnerability
        /// 
        /// Features:
        /// - Idempotency: If already checked in today, return same result without duplicating points
        /// - Check-in verification code: CHK-YYYYMMDD-{memberId}
        /// - Reward calculation: Based on consecutive days cycle 1-7
        /// - Atomic update: Safely update Member_Stats balance
        /// - Automatic rollback: Errors automatically rollback
        /// 
        /// Request Body: For future auto-detection compatibility, currently only uses memberId parameter
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <param name="request">Check-in request (optional)</param>
        /// <returns>Check-in result</returns>
        [HttpPost("{memberId}/Checkin")]
        public async Task<ActionResult<CheckinResultDto>> PerformCheckin(int memberId, [FromBody] CheckinRequestDto? request = null)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("Member ID must be greater than 0");
                }

                // For future auto-detection compatibility request body, currently only use memberId parameter
                var result = await _pointsService.PerformCheckinAsync(memberId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check-in failed for member {MemberId}", memberId);

                // Check if it's already checked in today error
                if (ex.Message.Contains("CHK-") || ex.Message.Contains("already checked in"))
                {
                    return Conflict(new { Message = "Already checked in today", Code = "ALREADY_CHECKED_IN" });
                }

                return StatusCode(500, "Check-in operation failed");
            }
        }

        #endregion

        #region Member Level Summary and Status API

        /// <summary>
        /// Get member level summary
        /// 
        /// Security Warning: Currently has IDOR vulnerability
        /// Next step: Should use /api/Members/me/Level/Summary and get memberId through JWT claims
        /// 
        /// Return content:
        /// - Current level, next level progress, required amount, progress percentage
        /// - Status judgment basis: Membership_Levels.Required_Amount vs member accumulated spending
        /// - Accumulated spending source: Member_Stats.Total_Spent
        /// 
        /// Example request: GET /api/Members/123/Level/Summary
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <returns>Member level summary information</returns>
        [HttpGet("{memberId}/Level/Summary")]
        public async Task<ActionResult<MemberLevelSummaryDto>> GetMemberLevelSummary(int memberId)
        {
            try
            {
                // Validate memberId
                if (!int.TryParse(memberId.ToString(), out var validMemberId) || validMemberId <= 0)
                {
                    return BadRequest(new { message = "Invalid member ID" });
                }

                _logger.LogInformation("Getting level summary for member {MemberId}", memberId);

                var summary = await _memberLevelService.GetMemberLevelSummaryAsync(memberId);
                
                if (summary == null)
                {
                    return NotFound(new { message = "Member not found or level information unavailable" });
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get level summary for member {MemberId}", memberId);
                return StatusCode(500, new { message = "Failed to get level summary", error = ex.Message });
            }
        }

        /// <summary>
        /// Recalculate accumulated spending and update level
        /// 
        /// Security Warning: Currently has IDOR vulnerability
        /// Next step: Should use /api/Members/me/Level/Recalculate and get memberId through JWT claims
        /// 
        /// Process:
        /// 1. Query Orders to sum member's "actual paid order amounts" (paid|completed status only)
        /// 2. If orders found, update Member_Stats.Total_Spent to sum amount
        /// 3. Use latest totalSpent and predefined levels (Is_Active=1, sorted by minimum level first, by totalSpent)
        /// 4. If different from Member_Stats.Current_Level_Id: update and return levelUp: true
        /// 5. Atomic update Member_Stats (Total_Spent, Current_Level_Id, Updated_At)
        /// 
        /// Error handling: Wrapped in transaction, prevents concurrent calculations
        /// 
        /// Example request: POST /api/Members/123/Level/Recalculate
        /// </summary>
        /// <param name="memberId">Member ID</param>
        /// <returns>Recalculation result, including levelUp status and new level information</returns>
        [HttpPost("{memberId}/Level/Recalculate")]
        public async Task<ActionResult<RecalculateResultDto>> RecalculateMemberLevel(int memberId)
        {
            try
            {
                // Validate memberId
                if (!int.TryParse(memberId.ToString(), out var validMemberId) || validMemberId <= 0)
                {
                    return BadRequest(new { message = "Invalid member ID" });
                }

                _logger.LogInformation("Starting level recalculation for member {MemberId}", memberId);

                var result = await _memberLevelService.RecalculateMemberLevelAsync(memberId);
                
                if (result == null)
                {
                    return NotFound(new { message = "Member not found or level data unavailable" });
                }

                var message = result.LevelUp 
                    ? $"Level recalculation completed. Congratulations! Upgraded from {result.OldLevel?.Name} to {result.NewLevel?.Name}"
                    : "Level recalculation completed, no level change";

                _logger.LogInformation("Member {MemberId} level recalculation completed, level up: {LevelUp}", memberId, result.LevelUp);

                return Ok(new 
                { 
                    message = message,
                    data = result 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate level for member {MemberId}", memberId);
                return StatusCode(500, new { message = "Level recalculation failed", error = ex.Message });
            }
        }

        #endregion
    }
}