using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.DTO;
using Team.API.Services;

namespace Team.API.Controllers
{
    /// <summary>
    /// �|������ API ���
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

        #region �u�f��������I�]�ȮɫO�d�\��^

        /// <summary>
        /// ��o���w�|�����Ҧ��u�f��M��
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C���N�d�ߤ�k�ɯŬ��ʺA�A
        /// �ɯŤ�׬O��� claims �� id �אּ�� /api/Members/me/MemberCoupons�A
        /// ���֤߷~���޿�P DTO �O�����ܡC
        /// 
        /// �d�߰ѼơG
        /// - activeOnly�]bool�A�w�] false�^�G�u�^�ǡu�ثe�i�Ρv���u�f��
        /// - status�]string�A�i�� active|used|expired|cancelled�^
        /// - page�]int�A�w�] 1�F�Y�p��1�h��1�^
        /// - pageSize�]int�A�w�] 20�F�̤j 100�^
        /// 
        /// �u�ثe�i�Ρv�w�q�]�P�ɺ����^�G
        /// - Member_Coupons.Status = 'active'
        /// - Coupons.Is_Active = 1
        /// - �{�b�ɶ����� Coupons.Start_At �P Coupons.Expired_At�]�t��ɡ^
        /// - �Y Coupons.Usage_Limit ���ȡGCoupons.Used_Count < Coupons.Usage_Limit
        /// 
        /// �ƧǡG�D�n�� Coupons.Expired_At �ɶ��@�ǡA�P���n�� Status='active' �u��
        /// 
        /// �d�߽ШD�G
        /// - GET /api/Members/123/MemberCoupons?activeOnly=true&page=1&pageSize=10
        /// - GET /api/Members/123/MemberCoupons?status=used&page=2
        /// - GET /api/Members/123/MemberCoupons?status=active&activeOnly=false
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="activeOnly">�O�_�u�^�ǥثe�i�Ϊ��u�f��</param>
        /// <param name="status">���A�z��</param>
        /// <param name="page">���X</param>
        /// <param name="pageSize">�C������</param>
        /// <returns>������|���u�f��M��</returns>
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
                // �Ѽ�����
                if (memberId <= 0)
                {
                    return BadRequest(new PagedResponseDto<MyMemberCouponDto>
                    {
                        Success = false,
                        Message = "�|��ID�����j�� 0",
                        Data = new List<MyMemberCouponDto>(),
                        TotalCount = 0,
                        CurrentPage = 1,
                        ItemsPerPage = 20,
                        TotalPages = 0
                    });
                }

                // ����Ѽ�
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                _logger.LogInformation("�}�l�d�߷|�� {MemberId} �u�f��AactiveOnly: {ActiveOnly}, status: {Status}, page: {Page}, pageSize: {PageSize}",
                    memberId, activeOnly, status, page, pageSize);

                // �ʸ˰ʺA�d�ߤ�k�A�K�󤧫��� claims �ɯ�
                var result = await GetMemberCouponsInternal(memberId, activeOnly, status, page, pageSize);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�d�߷|�� {MemberId} �u�f�饢��", memberId);
                return StatusCode(500, new PagedResponseDto<MyMemberCouponDto>
                {
                    Success = false,
                    Message = "�d�ߥ��ѡG" + ex.Message,
                    Data = new List<MyMemberCouponDto>(),
                    TotalCount = 0,
                    CurrentPage = 1,
                    ItemsPerPage = 20,
                    TotalPages = 0
                });
            }
        }

        /// <summary>
        /// �����d�ߤ�k - �ʸ˰ʺA�A�K�󤧫��� JWT claims �ɯ�
        /// </summary>
        private async Task<PagedResponseDto<MyMemberCouponDto>> GetMemberCouponsInternal(
            int memberId, bool activeOnly, string status, int page, int pageSize)
        {
            var now = DateTime.Now;

            // �إ߰򥻬d�ߡGMemberCoupons JOIN Coupons�A�u�^�Ǹӷ|�����u�f��
            var query = _context.MemberCoupons
                .Where(mc => mc.MemberId == memberId)
                .Include(mc => mc.Coupon)
                    .ThenInclude(c => c.Sellers)
                        .ThenInclude(s => s.Members)
                .AsNoTracking();

            // activeOnly �z��G�u�^�ǡu�ثe�i�Ρv���u�f��
            if (activeOnly)
            {
                query = query.Where(mc =>
                    mc.Status == "active" &&
                    mc.Coupon.IsActive &&
                    now >= mc.Coupon.StartAt &&
                    now <= mc.Coupon.ExpiredAt &&
                    (mc.Coupon.UsageLimit == null || mc.Coupon.UsedCount < mc.Coupon.UsageLimit));
            }

            // status �z��
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(mc => mc.Status.ToLower() == status.ToLower());
            }

            // �p���`��
            var total = await query.CountAsync();

            // �ƧǡG�D�n�� Coupons.ExpiredAt �ɶ��@�ǡA�P���n�� Status='active' �u��
            var memberCoupons = await query
                .OrderBy(mc => mc.Coupon.ExpiredAt)
                .ThenByDescending(mc => mc.Status == "active" ? 1 : 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // �ഫ�� DTO
            var dtos = memberCoupons.Select(mc => 
            {
                var now = DateTime.Now;
                var currentStatus = mc.Status ?? "";
                
                // �ʺA�p�⥿�T�����A
                string actualStatus;
                if (mc.UsedAt.HasValue || currentStatus.ToLower() == "used")
                {
                    actualStatus = "used";
                }
                else if (mc.Coupon.ExpiredAt < now)
                {
                    actualStatus = "expired";  // ?? ����ק�G�L���ˬd
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
                    // �|���u�f���T�]Member_Coupons�^
                    MemberCouponId = mc.Id,
                    Status = actualStatus,  // �ϥέp��᪺���A
                    AssignedAt = mc.AssignedAt,
                    UsedAt = mc.UsedAt,
                    OrderId = mc.OrderId,
                    VerificationCode = mc.VerificationCode ?? "",

                    // �u�f��w�q��T�]Coupons�^
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

                    // �ɥR��T�GSellerName�]�p�G���t�ӥi�H��ܡA�_�h�^ null�^
                    SellerName = mc.Coupon.Sellers?.RealName
                };
            }).ToList();

            _logger.LogInformation("�|�� {MemberId} �d�ߧ����A�`��: {Total}�A�Ǧ^: {PageCount}",
                memberId, total, dtos.Count);

            return new PagedResponseDto<MyMemberCouponDto>
            {
                Success = true,
                Message = "�d�߷|���u�f�馨�\",
                Data = dtos,
                TotalCount = total,
                CurrentPage = page,
                ItemsPerPage = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            };
        }

        #endregion

        #region �I�Ƭ������I

        /// <summary>
        /// �d�߷|���I�ƾl�B
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C
        /// ���N�אּ /api/Members/me/Points/Balance�]�q JWT claims ��o memberId�^�C
        /// 
        /// �d�ߡGMember_Stats.Total_Points�]��ơ^�A�Y�d�L��Ʀ^ balance=0�C
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <returns>�|���I�ƾl�B��T</returns>
        [HttpGet("{memberId}/Points/Balance")]
        public async Task<ActionResult<PointsBalanceDto>> GetPointsBalance(int memberId)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                var balance = await _pointsService.GetBalanceAsync(memberId);
                if (balance == null)
                {
                    return NotFound("�䤣����w�|��");
                }

                return Ok(balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�d�߷|�� {MemberId} �I�ƾl�B����", memberId);
                return StatusCode(500, "�d���I�ƾl�B����");
            }
        }

        /// <summary>
        /// �d�߷|���I�ƾ��v�O���]���� + �z��^
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C
        /// 
        /// �z��G
        /// - type�]�i��F�i��ȡGsignin|used|refund|earned|expired|adjustment�^
        /// - dateFrom/dateTo�]�H Created_At �z��^
        /// 
        /// �ƧǡGCreated_At DESC
        /// 
        /// �^�ǡG������A�C���]�t�GId, Type, Amount, Note, Expired_At, Transaction_Id, Created_At, Verification_Code
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="type">�����z��</param>
        /// <param name="dateFrom">�}�l���</param>
        /// <param name="dateTo">�������</param>
        /// <param name="page">���X</param>
        /// <param name="pageSize">�C������</param>
        /// <returns>�������I�ƾ��v�O��</returns>
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
                    return BadRequest("�|��ID�����j�� 0");
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
                _logger.LogError(ex, "�d�߷|�� {MemberId} �I�ƾ��v����", memberId);
                return StatusCode(500, "�d���I�ƾ��v����");
            }
        }

        /// <summary>
        /// �[�I�]Earn / �վ�^
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C
        /// 
        /// �޿�G
        /// - amount > 0�Ftype �����b�զW��]earned �� adjustment�^
        /// - �����G�Y verificationCode �w�s�b�� Points_Log �N���Ʀ^�Ǧ��\���G�]�����ʡ^
        /// - �y�{�G�s�W Points_Log�]+amount�^�A�P�B�w���W�[ Member_Stats.Total_Points
        /// - ���Ѭ��� Points_Log_Error
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="request">�[�I�ШD</param>
        /// <returns>�I�Ʋ��ʵ��G</returns>
        [HttpPost("{memberId}/Points/Earn")]
        public async Task<ActionResult<PointsMutationResultDto>> EarnPoints(int memberId, [FromBody] PointsEarnRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "��J�ѼƦ��~", Errors = errors });
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
                _logger.LogError(ex, "�|�� {MemberId} �[�I����", memberId);
                return StatusCode(500, "�[�I�@�~����");
            }
        }

        /// <summary>
        /// ���I�]Use�^
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C
        /// 
        /// �ˬd�G
        /// - Ū Member_Stats.Total_Points�A�ˬd�i���� amount
        /// - verificationCode �����ʳB�z�]�Y�s�b�A���Ʀ^�ǬJ�����G�^
        /// 
        /// �y�{�G
        /// - �s�W Points_Log�]type=used�Aamount=����ƫO���A���O�P�ɱa�W direction:"debit"�^
        /// - ��l��s�GUPDATE Member_Stats SET Total_Points = Total_Points - @amount WHERE Member_Id=@memberId AND Total_Points >= @amount
        /// - �Y UPDATE ���v�T �� 1 �^ 409/400 �ì��� Points_Log_Error
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="request">���I�ШD</param>
        /// <returns>�I�Ʋ��ʵ��G</returns>
        [HttpPost("{memberId}/Points/Use")]
        public async Task<ActionResult<PointsMutationResultDto>> UsePoints(int memberId, [FromBody] PointsUseRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "��J�ѼƦ��~", Errors = errors });
                }

                var result = await _pointsService.UsePointsAsync(memberId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("�l�B����"))
            {
                return Conflict(new { Message = ex.Message, Code = "INSUFFICIENT_BALANCE" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�|�� {MemberId} ���I����", memberId);
                return StatusCode(500, "���I�@�~����");
            }
        }

        /// <summary>
        /// �^�ɡ]Refund�^
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C
        /// 
        /// �����GverificationCode ����
        /// �y�{�G�g Points_Log�]refund�^�A�P�B�[�^ Total_Points
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="request">�^�ɽШD</param>
        /// <returns>�I�Ʋ��ʵ��G</returns>
        [HttpPost("{memberId}/Points/Refund")]
        public async Task<ActionResult<PointsMutationResultDto>> RefundPoints(int memberId, [FromBody] PointsRefundRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "��J�ѼƦ��~", Errors = errors });
                }

                var result = await _pointsService.RefundPointsAsync(memberId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�|�� {MemberId} �I�ưh�ڥ���", memberId);
                return StatusCode(500, "�I�ưh�ڧ@�~����");
            }
        }

        /// <summary>
        /// ������I�]�Ƶ{�ΡA�Ω�{�ǡ^
        /// 
        /// ?? �Ȯɤ�סG�ثe�ȴ��ɴ��A�s�b IDOR ���I�C
        /// 
        /// �M���̻ݡu�L�����I�v�ݨD�ɨϥΡG�g expired �����A�æP�B��� Total_Points�]�P Use �ۦP���w�� UPDATE�^
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="request">������I�ШD</param>
        /// <returns>�I�Ʋ��ʵ��G</returns>
        [HttpPost("{memberId}/Points/Expire")]
        public async Task<ActionResult<PointsMutationResultDto>> ExpirePoints(int memberId, [FromBody] PointsExpireRequestDto request)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.First().ErrorMessage);
                    
                    return BadRequest(new { Message = "��J�ѼƦ��~", Errors = errors });
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
                _logger.LogError(ex, "�|�� {MemberId} �I�ƨ�����I����", memberId);
                return StatusCode(500, "�I�ƨ�����I�@�~����");
            }
        }

        #endregion

        #region ñ��������I�]�s�W�^

        /// <summary>
        /// ���o����ñ���T
        /// 
        /// ?? �Ȯɤ�סG�ϥ� memberId ���|�ѼơA�s�b IDOR ���I
        /// 
        /// �\��G
        /// - �ˬd���ѬO�_�wñ��]�H���A�����鬰�ǡ^
        /// - �p��s��ñ��Ѽ�
        /// - �p�⤵����y�]�p����ܭȡ^
        /// - �^�Ǧ��A���ɶ��M������
        /// 
        /// ñ����y�W�h�G
        /// - �s�� 1-7 �ѹ��� 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 1.0 JCoin
        /// - �� 8 �ѭ��s�`���� 0.1 JCoin
        /// - �����x�s����� (Amount = ��ܭ� �� 10)
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <returns>����ñ���T</returns>
        [HttpGet("{memberId}/Checkin/Info")]
        public async Task<ActionResult<CheckinInfoDto>> GetCheckinInfo(int memberId)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                var info = await _pointsService.GetCheckinInfoAsync(memberId);
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���o�|�� {MemberId} ñ���T����", memberId);
                return StatusCode(500, "���oñ���T����");
            }
        }

        /// <summary>
        /// ����ñ��
        /// 
        /// ?? �Ȯɤ�סG�ϥ� memberId ���|�ѼơA�s�b IDOR ���I
        /// 
        /// �\��G
        /// - �����ʡG���ѭ���ñ��|�^�ǬۦP���G�A�����Ƶ��I
        /// - ñ��ߤ@�X�GCHK-YYYYMMDD-{memberId}
        /// - ���y�p��G�ھڳs��Ѽƴ`�� 1-7 ��
        /// - ��l��s�G�w����s Member_Stats �l�B
        /// - ����O�@�G���Ѧ۰ʦ^�u
        /// 
        /// Request Body�G���F�ۮe�ʱ����A���H���| memberId ����
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <param name="request">ñ��ШD�]�i��^</param>
        /// <returns>ñ�쵲�G</returns>
        [HttpPost("{memberId}/Checkin")]
        public async Task<ActionResult<CheckinResultDto>> PerformCheckin(int memberId, [FromBody] CheckinRequestDto? request = null)
        {
            try
            {
                if (memberId <= 0)
                {
                    return BadRequest("�|��ID�����j�� 0");
                }

                // ���F�ۮe�ʱ��� request body�A����ڥH���|�Ѽƪ� memberId ����
                var result = await _pointsService.PerformCheckinAsync(memberId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�|�� {MemberId} ñ�쥢��", memberId);

                // �ˬd�O�_������ñ�쪺�Ĭ�
                if (ex.Message.Contains("CHK-") || ex.Message.Contains("�wñ��"))
                {
                    return Conflict(new { Message = "����w����ñ��", Code = "ALREADY_CHECKED_IN" });
                }

                return StatusCode(500, "ñ��@�~����");
            }
        }

        #endregion

        #region �|������ Summary �P�ɯ� API

        /// <summary>
        /// ��o�|������ Summary
        /// 
        /// ?? �Ȯɰ��kĵ�i�G�ثe�ȴ��ɴ��A�s�b IDOR ���I
        /// �U�@�B�n�אּ /api/Members/me/Level/Summary �óz�L JWT claims ��o memberId
        /// 
        /// �^�Ǥ��e�G
        /// - �ثe���šB�U�@�Ū��e�B�w��O���B�B�Z���i��
        /// - �ɯŧP�w�̾ڡGMembership_Levels.Required_Amount �P�|���֭p��O���B
        /// - �w��O���B�ӷ��GMember_Stats.Total_Spent
        /// 
        /// �d�߽ШD�GGET /api/Members/123/Level/Summary
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <returns>�|�����źK�n��T</returns>
        [HttpGet("{memberId}/Level/Summary")]
        public async Task<ActionResult<MemberLevelSummaryDto>> GetMemberLevelSummary(int memberId)
        {
            try
            {
                // ���� memberId
                if (!int.TryParse(memberId.ToString(), out var validMemberId) || validMemberId <= 0)
                {
                    return BadRequest(new { message = "�L�Ī��|��ID" });
                }

                _logger.LogInformation("��o�|�� {MemberId} ���źK�n", memberId);

                var summary = await _memberLevelService.GetMemberLevelSummaryAsync(memberId);
                
                if (summary == null)
                {
                    return NotFound(new { message = "�䤣����w�|���ε��Ÿ��" });
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "��o�|�� {MemberId} ���źK�n����", memberId);
                return StatusCode(500, new { message = "��o���źK�n����", error = ex.Message });
            }
        }

        /// <summary>
        /// ���s�p��w��O���B�æP�B�ɯ�
        /// 
        /// ?? �Ȯɰ��kĵ�i�G�ثe�ȴ��ɴ��A�s�b IDOR ���I
        /// �U�@�B�n�אּ /api/Members/me/Level/Recalculate �óz�L JWT claims ��o memberId
        /// 
        /// �޿�G
        /// 1. �q Orders �ֿn�ӷ|�����u��ڧ����q��v���B�]paid|completed ���A�^
        /// 2. �Y�d�ߨ� Orders�A��s Member_Stats.Total_Spent �í���
        /// 3. �̷s�� totalSpent �P�w�w�q���š]�� Is_Active=1 ���̰��A�ά��ǡA�� totalSpent�^
        /// 4. �Y�P Member_Stats.Current_Level_Id ���P�G��s�æ^�� levelUp: true
        /// 5. ��l��s Member_Stats�]Total_Spent�BCurrent_Level_Id�BUpdated_At�^
        /// 
        /// �������G�]�b������g Member_Stats�A�קK�v�α��p
        /// 
        /// �d�߽ШD�GPOST /api/Members/123/Level/Recalculate
        /// </summary>
        /// <param name="memberId">�|��ID</param>
        /// <returns>���s�p�⵲�G�A�]�t levelUp �аO�M�s�����Ÿ�T</returns>
        [HttpPost("{memberId}/Level/Recalculate")]
        public async Task<ActionResult<RecalculateResultDto>> RecalculateMemberLevel(int memberId)
        {
            try
            {
                // ���� memberId
                if (!int.TryParse(memberId.ToString(), out var validMemberId) || validMemberId <= 0)
                {
                    return BadRequest(new { message = "�L�Ī��|��ID" });
                }

                _logger.LogInformation("�}�l���s�p��|�� {MemberId} ����", memberId);

                var result = await _memberLevelService.RecalculateMemberLevelAsync(memberId);
                
                if (result == null)
                {
                    return NotFound(new { message = "�䤣����w�|���θ��" });
                }

                var message = result.LevelUp 
                    ? $"���ŭ��p�����A���ߤɯšI�q {result.OldLevel?.Name} �ɯŦ� {result.NewLevel?.Name}"
                    : "���ŭ��p�����A���ŵL����";

                _logger.LogInformation("�|�� {MemberId} ���ŭ��p�����A�ɯŪ��p�G{LevelUp}", memberId, result.LevelUp);

                return Ok(new 
                { 
                    message = message,
                    data = result 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���s�p��|�� {MemberId} ���ť���", memberId);
                return StatusCode(500, new { message = "���s�p�ⵥ�ť���", error = ex.Message });
            }
        }

        #endregion
    }
}