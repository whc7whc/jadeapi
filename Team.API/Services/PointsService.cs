using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.DTO;

namespace Team.API.Services
{
    /// <summary>
    /// �I�ƪA�Ȥ���
    /// </summary>
    public interface IPointsService
    {
        Task<PointsBalanceDto?> GetBalanceAsync(int memberId);
        Task<PagedResponseDto<PointHistoryItemDto>> GetHistoryAsync(int memberId, PointsHistoryQueryDto query);
        Task<PointsMutationResultDto> EarnPointsAsync(int memberId, PointsEarnRequestDto request);
        Task<PointsMutationResultDto> UsePointsAsync(int memberId, PointsUseRequestDto request);
        Task<PointsMutationResultDto> RefundPointsAsync(int memberId, PointsRefundRequestDto request);
        Task<PointsMutationResultDto> ExpirePointsAsync(int memberId, PointsExpireRequestDto request);

        // ======== �s�W�Gñ�������k ========
        Task<CheckinInfoDto> GetCheckinInfoAsync(int memberId);
        Task<CheckinResultDto> PerformCheckinAsync(int memberId);
    }

    /// <summary>
    /// �I�ƪA�ȹ�@
    /// </summary>
    public class PointsService : IPointsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PointsService> _logger;

        // �I�������զW��
        private static readonly HashSet<string> ValidPointsTypes = new()
        {
            "signin", "used", "refund", "earned", "expired", "adjustment"
        };

        // ?? �״_�Gñ����y�t�m - ����������ܪ� J���ƶq
        private static readonly Dictionary<int, int> CheckinRewards = new()
        {
            { 1, 1 },   // Day 1: 1 J-Coin (database stores 1)
            { 2, 2 },   // Day 2: 2 J-Coins (database stores 2)
            { 3, 3 },   // Day 3: 3 J-Coins (database stores 3)
            { 4, 4 },   // Day 4: 4 J-Coins (database stores 4)
            { 5, 5 },   // Day 5: 5 J-Coins (database stores 5)
            { 6, 6 },   // Day 6: 6 J-Coins (database stores 6)
            { 7, 10 }   // Day 7: 10 J-Coins (database stores 10)
        };

        public PointsService(AppDbContext context, ILogger<PointsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// �d�߷|���I�ƾl�B
        /// </summary>
        public async Task<PointsBalanceDto?> GetBalanceAsync(int memberId)
        {
            try
            {
                // �q MemberStats �d�߾l�B�]��ơ^
                var memberStat = await _context.MemberStats
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat != null)
                {
                    return new PointsBalanceDto
                    {
                        MemberId = memberId,
                        Balance = memberStat.TotalPoints,
                        LastUpdatedAt = memberStat.UpdatedAt
                    };
                }

                // �Y�d�L��ơA�^�Ǿl�B�� 0
                return new PointsBalanceDto
                {
                    MemberId = memberId,
                    Balance = 0,
                    LastUpdatedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�d�߷|�� {MemberId} �I�ƾl�B����", memberId);
                throw;
            }
        }

        /// <summary>
        /// �d�߷|���I�ƾ��v�O���]���� + �z��^
        /// </summary>
        public async Task<PagedResponseDto<PointHistoryItemDto>> GetHistoryAsync(int memberId, PointsHistoryQueryDto query)
        {
            try
            {
                var pointsQuery = _context.PointsLogs
                    .Where(pl => pl.MemberId == memberId)
                    .AsNoTracking();

                // �����z��
                if (!string.IsNullOrEmpty(query.Type))
                {
                    pointsQuery = pointsQuery.Where(pl => pl.Type == query.Type);
                }

                // ����z��]�H CreatedAt �z��^
                if (query.DateFrom.HasValue)
                {
                    pointsQuery = pointsQuery.Where(pl => pl.CreatedAt >= query.DateFrom.Value);
                }

                if (query.DateTo.HasValue)
                {
                    pointsQuery = pointsQuery.Where(pl => pl.CreatedAt <= query.DateTo.Value);
                }

                // �p���`��
                var total = await pointsQuery.CountAsync();

                // �ƧǡGCreatedAt DESC�A�ä���
                var items = await pointsQuery
                    .OrderByDescending(pl => pl.CreatedAt)
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .Select(pl => new PointHistoryItemDto
                    {
                        Id = pl.Id,
                        Type = pl.Type ?? "",
                        Amount = pl.Amount,
                        Note = pl.Note,
                        ExpiredAt = pl.ExpiredAt,
                        TransactionId = pl.TransactionId,
                        CreatedAt = pl.CreatedAt,
                        VerificationCode = pl.VerificationCode
                    })
                    .ToListAsync();

                return new PagedResponseDto<PointHistoryItemDto>
                {
                    Success = true,
                    Message = "�d���I�ƾ��v���\",
                    Data = items,
                    TotalCount = total,
                    CurrentPage = query.Page,
                    ItemsPerPage = query.PageSize,
                    TotalPages = (int)Math.Ceiling((double)total / query.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�d�߷|�� {MemberId} �I�ƾ��v����", memberId);
                throw;
            }
        }

        /// <summary>
        /// �[�I�]Earn / �վ�^
        /// </summary>
        public async Task<PointsMutationResultDto> EarnPointsAsync(int memberId, PointsEarnRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ���������զW��
                if (!ValidPointsTypes.Contains(request.Type))
                {
                    throw new ArgumentException($"�L�Ī��I������: {request.Type}");
                }

                // �������ˬd�G�Y VerificationCode �w�s�b�A�h�^�ǬJ�����G
                if (!string.IsNullOrEmpty(request.VerificationCode))
                {
                    var existingLog = await _context.PointsLogs
                        .FirstOrDefaultAsync(pl => pl.VerificationCode == request.VerificationCode);

                    if (existingLog != null)
                    {
                        var currentBalance = await GetCurrentBalanceFromStats(memberId);
                        return new PointsMutationResultDto
                        {
                            MemberId = memberId,
                            BeforeBalance = currentBalance - existingLog.Amount,
                            ChangeAmount = existingLog.Amount,
                            AfterBalance = currentBalance,
                            Type = existingLog.Type ?? "",
                            TransactionId = existingLog.TransactionId,
                            VerificationCode = existingLog.VerificationCode,
                            CreatedAt = existingLog.CreatedAt
                        };
                    }
                }

                // ��o�ثe�l�B
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // �s�W PointsLog
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = request.Type,
                    Amount = request.Amount,
                    Note = request.Note,
                    ExpiredAt = request.ExpiredAt,
                    TransactionId = request.TransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();

                // �P�B��s�W�[ MemberStats.TotalPoints
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // �Y�L��ơA�إ߷s�O��
                    memberStat = new MemberStat
                    {
                        MemberId = memberId,
                        TotalPoints = request.Amount,
                        UpdatedAt = now
                    };
                    _context.MemberStats.Add(memberStat);
                }
                else
                {
                    // ��s�{���O��
                    memberStat.TotalPoints += request.Amount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + request.Amount;

                _logger.LogInformation("�|�� {MemberId} �[�I���\�A����: {Type}�A���B: {Amount}�A�l�B: {BeforeBalance} -> {AfterBalance}",
                    memberId, request.Type, request.Amount, beforeBalance, afterBalance);

                return new PointsMutationResultDto
                {
                    MemberId = memberId,
                    BeforeBalance = beforeBalance,
                    ChangeAmount = request.Amount,
                    AfterBalance = afterBalance,
                    Type = request.Type,
                    TransactionId = request.TransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await LogError(memberId, "EarnPoints", ex.Message, request);
                _logger.LogError(ex, "�|�� {MemberId} �[�I����", memberId);
                throw;
            }
        }

        /// <summary>
        /// ���I�]Use�^
        /// </summary>
        public async Task<PointsMutationResultDto> UsePointsAsync(int memberId, PointsUseRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // �������ˬd
                if (!string.IsNullOrEmpty(request.VerificationCode))
                {
                    var existingLog = await _context.PointsLogs
                        .FirstOrDefaultAsync(pl => pl.VerificationCode == request.VerificationCode);

                    if (existingLog != null)
                    {
                        var currentBalance = await GetCurrentBalanceFromStats(memberId);
                        return new PointsMutationResultDto
                        {
                            MemberId = memberId,
                            BeforeBalance = currentBalance + existingLog.Amount,
                            ChangeAmount = -existingLog.Amount,
                            AfterBalance = currentBalance,
                            Type = "used",
                            TransactionId = existingLog.TransactionId,
                            VerificationCode = existingLog.VerificationCode,
                            CreatedAt = existingLog.CreatedAt
                        };
                    }
                }

                // ��o�ثe�l�B���ˬd
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);
                if (beforeBalance < request.Amount)
                {
                    throw new InvalidOperationException($"�l�B�����A�ثe�l�B: {beforeBalance}�A�ݨD���B: {request.Amount}");
                }

                var now = DateTime.Now;

                // ��l��s�G�w����s�l�B
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE MemberStats SET TotalPoints = TotalPoints - {0}, UpdatedAt = {1} WHERE MemberId = {2} AND TotalPoints >= {3}",
                    request.Amount, now, memberId, request.Amount);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException("���I���ѡG�l�B�����εo�ֵͨo");
                }

                // �s�W PointsLog�]type=used�Aamount=����ƫO���^
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "used",
                    Amount = request.Amount, // ����ƫO��
                    Note = request.Note,
                    TransactionId = request.TransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance - request.Amount;

                _logger.LogInformation("�|�� {MemberId} ���I���\�A���B: {Amount}�A�l�B: {BeforeBalance} -> {AfterBalance}",
                    memberId, request.Amount, beforeBalance, afterBalance);

                return new PointsMutationResultDto
                {
                    MemberId = memberId,
                    BeforeBalance = beforeBalance,
                    ChangeAmount = -request.Amount, // �t�ƪ�ܴ��
                    AfterBalance = afterBalance,
                    Type = "used",
                    TransactionId = request.TransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                // �u���D�l�B���������~�~������ Points_Log_Error
                if (!ex.Message.Contains("�l�B����"))
                {
                    await LogError(memberId, "UsePoints", ex.Message, request);
                }
                
                _logger.LogError(ex, "�|�� {MemberId} ���I����", memberId);
                throw;
            }
        }

        /// <summary>
        /// �^�ɡ]Refund�^
        /// </summary>
        public async Task<PointsMutationResultDto> RefundPointsAsync(int memberId, PointsRefundRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // �������ˬd
                if (!string.IsNullOrEmpty(request.VerificationCode))
                {
                    var existingLog = await _context.PointsLogs
                        .FirstOrDefaultAsync(pl => pl.VerificationCode == request.VerificationCode);

                    if (existingLog != null)
                    {
                        var currentBalance = await GetCurrentBalanceFromStats(memberId);
                        return new PointsMutationResultDto
                        {
                            MemberId = memberId,
                            BeforeBalance = currentBalance - existingLog.Amount,
                            ChangeAmount = existingLog.Amount,
                            AfterBalance = currentBalance,
                            Type = "refund",
                            TransactionId = existingLog.TransactionId,
                            VerificationCode = existingLog.VerificationCode,
                            CreatedAt = existingLog.CreatedAt
                        };
                    }
                }

                // ��o�ثe�l�B
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // �s�W PointsLog�]refund�^
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "refund",
                    Amount = request.Amount,
                    Note = request.Note ?? $"�h�ڦۥ��: {request.SourceTransactionId}",
                    TransactionId = request.SourceTransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();

                // �P�B�[�^ TotalPoints
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    memberStat = new MemberStat
                    {
                        MemberId = memberId,
                        TotalPoints = request.Amount,
                        UpdatedAt = now
                    };
                    _context.MemberStats.Add(memberStat);
                }
                else
                {
                    memberStat.TotalPoints += request.Amount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + request.Amount;

                _logger.LogInformation("�|�� {MemberId} �I�ưh�ڦ��\�A���B: {Amount}�A�ӷ����: {SourceTransactionId}�A�l�B: {BeforeBalance} -> {AfterBalance}",
                    memberId, request.Amount, request.SourceTransactionId, beforeBalance, afterBalance);

                return new PointsMutationResultDto
                {
                    MemberId = memberId,
                    BeforeBalance = beforeBalance,
                    ChangeAmount = request.Amount,
                    AfterBalance = afterBalance,
                    Type = "refund",
                    TransactionId = request.SourceTransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await LogError(memberId, "RefundPoints", ex.Message, request);
                _logger.LogError(ex, "�|�� {MemberId} �I�ưh�ڥ���", memberId);
                throw;
            }
        }

        /// <summary>
        /// ������I�]Expire�^
        /// </summary>
        public async Task<PointsMutationResultDto> ExpirePointsAsync(int memberId, PointsExpireRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // �������ˬd
                if (!string.IsNullOrEmpty(request.VerificationCode))
                {
                    var existingLog = await _context.PointsLogs
                        .FirstOrDefaultAsync(pl => pl.VerificationCode == request.VerificationCode);

                    if (existingLog != null)
                    {
                        var currentBalance = await GetCurrentBalanceFromStats(memberId);
                        return new PointsMutationResultDto
                        {
                            MemberId = memberId,
                            BeforeBalance = currentBalance + existingLog.Amount,
                            ChangeAmount = -existingLog.Amount,
                            AfterBalance = currentBalance,
                            Type = "expired",
                            TransactionId = existingLog.TransactionId,
                            VerificationCode = existingLog.VerificationCode,
                            CreatedAt = existingLog.CreatedAt
                        };
                    }
                }

                // ��o�ثe�l�B
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // ��l��s�G�w����s�l�B�]�P Use �ۦP���w�� UPDATE�^
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE MemberStats SET TotalPoints = TotalPoints - {0}, UpdatedAt = {1} WHERE MemberId = {2} AND TotalPoints >= {3}",
                    request.Amount, now, memberId, request.Amount);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException("�I�ƨ�����I���ѡG�l�B�����εo�ֵͨo");
                }

                // �s�W PointsLog�]expired�^
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "expired",
                    Amount = request.Amount,
                    Note = request.Note ?? "�I�ƨ��",
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance - request.Amount;

                _logger.LogInformation("�|�� {MemberId} �I�ƨ�����I���\�A���B: {Amount}�A�l�B: {BeforeBalance} -> {AfterBalance}",
                    memberId, request.Amount, beforeBalance, afterBalance);

                return new PointsMutationResultDto
                {
                    MemberId = memberId,
                    BeforeBalance = beforeBalance,
                    ChangeAmount = -request.Amount,
                    AfterBalance = afterBalance,
                    Type = "expired",
                    TransactionId = null,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await LogError(memberId, "ExpirePoints", ex.Message, request);
                _logger.LogError(ex, "�|�� {MemberId} �I�ƨ�����I����", memberId);
                throw;
            }
        }

        // ======== �s�W�Gñ��\���@ ========

        /// <summary>
        /// ���o����ñ���T
        /// </summary>
        public async Task<CheckinInfoDto> GetCheckinInfoAsync(int memberId)
        {
            try
            {
                var now = DateTime.Now;
                var today = now.Date;
                var todayStr = today.ToString("yyyy-MM-dd");

                // �ˬd���ѬO�_�wñ��
                var todayCheckin = await _context.PointsLogs
                    .AsNoTracking()
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.CreatedAt.Date == today)
                    .FirstOrDefaultAsync();

                bool signedToday = todayCheckin != null;

                // �p��s��ñ��Ѽ�
                int checkinStreak = await CalculateCheckinStreakAsync(memberId, today, signedToday);

                // ?? �״_�G�p�⤵����y�]������^ JCoin ��ƭȡA���A���H10�^
                int todayReward = CalculateTodayReward(checkinStreak, signedToday);

                return new CheckinInfoDto
                {
                    MemberId = memberId,
                    Today = todayStr,
                    SignedToday = signedToday,
                    CheckinStreak = checkinStreak,
                    TodayReward = todayReward,  // ������^��� JCoin ��
                    ServerTime = now,
                    Unit = "J��",  // ��s������
                    Scale = 1      // �אּ 1�A���ݭn�Y��
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���o�|�� {MemberId} ñ���T����", memberId);
                throw;
            }
        }

        /// <summary>
        /// ����ñ��
        /// </summary>
        public async Task<CheckinResultDto> PerformCheckinAsync(int memberId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.Now;
                var today = now.Date;
                var todayStr = today.ToString("yyyyMMdd");
                var verificationCode = $"CHK-{todayStr}-{memberId}";

                // �������ˬd�G�ˬd���ѬO�_�wñ��
                var existingCheckin = await _context.PointsLogs
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.VerificationCode == verificationCode)
                    .FirstOrDefaultAsync();

                if (existingCheckin != null)
                {
                    // �wñ��A�^�ǬJ�����G
                    var currentBalance = await GetCurrentBalanceFromStats(memberId);
                    var streak = await CalculateCheckinStreakAsync(memberId, today, true);
                    
                    return new CheckinResultDto
                    {
                        MemberId = memberId,
                        SignedToday = true,
                        CheckinStreak = streak,
                        Reward = existingCheckin.Amount,  // ?? �״_�G�����ϥ� Amount�A���A���H10
                        BeforeBalance = currentBalance - existingCheckin.Amount,  // ?? �״_�G�������k
                        AfterBalance = currentBalance,  // ?? �״_�G�������k
                        VerificationCode = existingCheckin.VerificationCode ?? "",
                        CreatedAt = existingCheckin.CreatedAt
                    };
                }

                // �p��s��ñ��Ѽơ]ñ��e�^
                int checkinStreak = await CalculateCheckinStreakAsync(memberId, today, false);
                
                // ñ���Ѽ� = �s��Ѽ� + 1
                int newStreak = checkinStreak + 1;
                
                // ?? �״_�G�p����y�]�`�� 1-7 �ѡ^- �����ϥΰt�m��
                int rewardCycle = ((newStreak - 1) % 7) + 1;
                int rewardAmount = CheckinRewards[rewardCycle];  // �o�N�O�n�x�s�M��ܪ���
                
                // ��oñ��e�l�B
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                // �s�Wñ��O��
                var checkinLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "signin",
                    Amount = rewardAmount,  // ?? �״_�G�����x�s���y�ȡ]1,2,3...10�^
                    Note = "daily check-in",
                    VerificationCode = verificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(checkinLog);
                await _context.SaveChangesAsync();

                // ��l��s MemberStats
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // �Y�L��ơA�إ߷s�O��
                    memberStat = new MemberStat
                    {
                        MemberId = memberId,
                        TotalPoints = rewardAmount,
                        UpdatedAt = now
                        // Current_Level_Id �O���w�]�Ψϥ� 1
                    };
                    _context.MemberStats.Add(memberStat);
                }
                else
                {
                    // ��s�{���O��
                    memberStat.TotalPoints += rewardAmount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + rewardAmount;

                _logger.LogInformation("�|�� {MemberId} ñ�즨�\�A�s��Ѽ�: {Streak}�A���y: {Reward} J���A�l�B: {BeforeBalance} -> {AfterBalance}",
                    memberId, newStreak, rewardAmount, beforeBalance, afterBalance);

                return new CheckinResultDto
                {
                    MemberId = memberId,
                    SignedToday = true,
                    CheckinStreak = newStreak,
                    Reward = rewardAmount,  // ?? �״_�G�����^�Ǽ��y�� (1,2,3...10)
                    BeforeBalance = beforeBalance,  // ?? �״_�G�������k�A������ܾ�ƾl�B
                    AfterBalance = afterBalance,    // ?? �״_�G�������k�A������ܾ�ƾl�B
                    VerificationCode = verificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await LogError(memberId, "checkin", ex.Message, new { memberId });
                _logger.LogError(ex, "�|�� {MemberId} ñ�쥢��", memberId);
                throw;
            }
        }

        /// <summary>
        /// �p��s��ñ��Ѽ�
        /// </summary>
        private async Task<int> CalculateCheckinStreakAsync(int memberId, DateTime today, bool includeToday)
        {
            try
            {
                // ���o�� 60 �Ѫ�ñ��O��
                var startDate = today.AddDays(-60);
                var checkinDates = await _context.PointsLogs
                    .AsNoTracking()
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.CreatedAt.Date >= startDate && 
                                pl.CreatedAt.Date <= today)
                    .Select(pl => pl.CreatedAt.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToListAsync();

                if (checkinDates.Count == 0)
                {
                    return 0;
                }

                // �p��s��Ѽ�
                int streak = 0;
                var checkDate = today;

                // �p�G�]�t���ѥB���Ѧ�ñ��O��
                if (includeToday && checkinDates.Contains(today))
                {
                    streak = 1;
                    checkDate = today.AddDays(-1);
                }
                else if (!includeToday)
                {
                    // ñ��e�A�q�Q�Ѷ}�l�ˬd
                    checkDate = today.AddDays(-1);
                }

                // �V�e�v���ˬd
                while (checkinDates.Contains(checkDate))
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }

                return streak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�p��|�� {MemberId} �s��ñ��Ѽƥ���", memberId);
                return 0;
            }
        }

        /// <summary>
        /// ?? �״_�G�p�⤵����y�]��^ JCoin ��ƭȡA���A���H10�^
        /// </summary>
        private int CalculateTodayReward(int checkinStreak, bool signedToday)
        {
            if (signedToday)
            {
                // �wñ��A���y����e streak ���������y
                int rewardCycle = ((checkinStreak - 1) % 7) + 1;
                return CheckinRewards[rewardCycle]; // ?? �״_�G������^�t�m�ȡA���A���H10
            }
            else
            {
                // ��ñ��A���y��ñ��᪺���y
                int newStreak = checkinStreak + 1;
                int rewardCycle = ((newStreak - 1) % 7) + 1;
                return CheckinRewards[rewardCycle]; // ?? �״_�G������^�t�m�ȡA���A���H10
            }
        }

        /// <summary>
        /// �q MemberStats ��o�ثe�l�B
        /// </summary>
        private async Task<int> GetCurrentBalanceFromStats(int memberId)
        {
            var memberStat = await _context.MemberStats
                .AsNoTracking()
                .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

            return memberStat?.TotalPoints ?? 0;
        }

        /// <summary>
        /// �������~�� PointsLogError
        /// </summary>
        private async Task LogError(int memberId, string errorType, string errorDetails, object payload)
        {
            try
            {
                var error = new PointsLogError
                {
                    MemberId = memberId,
                    ErrorType = errorType,
                    ErrorDetails = $"{errorDetails} | Payload: {System.Text.Json.JsonSerializer.Serialize(payload)}",
                    CreatedAt = DateTime.Now
                };

                _context.PointsLogErrors.Add(error);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "�����I�ƿ��~���ѡA�|��: {MemberId}, ���~����: {ErrorType}", memberId, errorType);
            }
        }
    }
}