using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.DTO;

namespace Team.API.Services
{
    /// <summary>
    /// 點數服務介面
    /// </summary>
    public interface IPointsService
    {
        Task<PointsBalanceDto?> GetBalanceAsync(int memberId);
        Task<PagedResponseDto<PointHistoryItemDto>> GetHistoryAsync(int memberId, PointsHistoryQueryDto query);
        Task<PointsMutationResultDto> EarnPointsAsync(int memberId, PointsEarnRequestDto request);
        Task<PointsMutationResultDto> UsePointsAsync(int memberId, PointsUseRequestDto request);
        Task<PointsMutationResultDto> RefundPointsAsync(int memberId, PointsRefundRequestDto request);
        Task<PointsMutationResultDto> ExpirePointsAsync(int memberId, PointsExpireRequestDto request);

        // ======== 新增：簽到相關方法 ========
        Task<CheckinInfoDto> GetCheckinInfoAsync(int memberId);
        Task<CheckinResultDto> PerformCheckinAsync(int memberId);
    }

    /// <summary>
    /// 點數服務實作
    /// </summary>
    public class PointsService : IPointsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PointsService> _logger;

        // 點數類型白名單
        private static readonly HashSet<string> ValidPointsTypes = new()
        {
            "signin", "used", "refund", "earned", "expired", "adjustment"
        };

        // ?? 修復：簽到獎勵配置 - 直接對應顯示的 J幣數量
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
        /// 查詢會員點數餘額
        /// </summary>
        public async Task<PointsBalanceDto?> GetBalanceAsync(int memberId)
        {
            try
            {
                // 從 MemberStats 查詢餘額（整數）
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

                // 若查無資料，回傳餘額為 0
                return new PointsBalanceDto
                {
                    MemberId = memberId,
                    Balance = 0,
                    LastUpdatedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢會員 {MemberId} 點數餘額失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 查詢會員點數歷史記錄（分頁 + 篩選）
        /// </summary>
        public async Task<PagedResponseDto<PointHistoryItemDto>> GetHistoryAsync(int memberId, PointsHistoryQueryDto query)
        {
            try
            {
                var pointsQuery = _context.PointsLogs
                    .Where(pl => pl.MemberId == memberId)
                    .AsNoTracking();

                // 類型篩選
                if (!string.IsNullOrEmpty(query.Type))
                {
                    pointsQuery = pointsQuery.Where(pl => pl.Type == query.Type);
                }

                // 日期篩選（以 CreatedAt 篩選）
                if (query.DateFrom.HasValue)
                {
                    pointsQuery = pointsQuery.Where(pl => pl.CreatedAt >= query.DateFrom.Value);
                }

                if (query.DateTo.HasValue)
                {
                    pointsQuery = pointsQuery.Where(pl => pl.CreatedAt <= query.DateTo.Value);
                }

                // 計算總數
                var total = await pointsQuery.CountAsync();

                // 排序：CreatedAt DESC，並分頁
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
                    Message = "查詢點數歷史成功",
                    Data = items,
                    TotalCount = total,
                    CurrentPage = query.Page,
                    ItemsPerPage = query.PageSize,
                    TotalPages = (int)Math.Ceiling((double)total / query.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢會員 {MemberId} 點數歷史失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 加點（Earn / 調整）
        /// </summary>
        public async Task<PointsMutationResultDto> EarnPointsAsync(int memberId, PointsEarnRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 驗證類型白名單
                if (!ValidPointsTypes.Contains(request.Type))
                {
                    throw new ArgumentException($"無效的點數類型: {request.Type}");
                }

                // 冪等性檢查：若 VerificationCode 已存在，則回傳既有結果
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

                // 獲得目前餘額
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // 新增 PointsLog
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

                // 同步更新增加 MemberStats.TotalPoints
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // 若無資料，建立新記錄
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
                    // 更新現有記錄
                    memberStat.TotalPoints += request.Amount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + request.Amount;

                _logger.LogInformation("會員 {MemberId} 加點成功，類型: {Type}，金額: {Amount}，餘額: {BeforeBalance} -> {AfterBalance}",
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
                _logger.LogError(ex, "會員 {MemberId} 加點失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 扣點（Use）
        /// </summary>
        public async Task<PointsMutationResultDto> UsePointsAsync(int memberId, PointsUseRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 冪等性檢查
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

                // 獲得目前餘額並檢查
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);
                if (beforeBalance < request.Amount)
                {
                    throw new InvalidOperationException($"餘額不足，目前餘額: {beforeBalance}，需求金額: {request.Amount}");
                }

                var now = DateTime.Now;

                // 原子更新：安全更新餘額
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE MemberStats SET TotalPoints = TotalPoints - {0}, UpdatedAt = {1} WHERE MemberId = {2} AND TotalPoints >= {3}",
                    request.Amount, now, memberId, request.Amount);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException("扣點失敗：餘額不足或發生併發");
                }

                // 新增 PointsLog（type=used，amount=正整數保持）
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "used",
                    Amount = request.Amount, // 正整數保持
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

                _logger.LogInformation("會員 {MemberId} 扣點成功，金額: {Amount}，餘額: {BeforeBalance} -> {AfterBalance}",
                    memberId, request.Amount, beforeBalance, afterBalance);

                return new PointsMutationResultDto
                {
                    MemberId = memberId,
                    BeforeBalance = beforeBalance,
                    ChangeAmount = -request.Amount, // 負數表示減少
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
                
                // 只有非餘額不足的錯誤才紀錄到 Points_Log_Error
                if (!ex.Message.Contains("餘額不足"))
                {
                    await LogError(memberId, "UsePoints", ex.Message, request);
                }
                
                _logger.LogError(ex, "會員 {MemberId} 扣點失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 回補（Refund）
        /// </summary>
        public async Task<PointsMutationResultDto> RefundPointsAsync(int memberId, PointsRefundRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 冪等性檢查
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

                // 獲得目前餘額
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // 新增 PointsLog（refund）
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "refund",
                    Amount = request.Amount,
                    Note = request.Note ?? $"退款自交易: {request.SourceTransactionId}",
                    TransactionId = request.SourceTransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();

                // 同步加回 TotalPoints
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

                _logger.LogInformation("會員 {MemberId} 點數退款成功，金額: {Amount}，來源交易: {SourceTransactionId}，餘額: {BeforeBalance} -> {AfterBalance}",
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
                _logger.LogError(ex, "會員 {MemberId} 點數退款失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 到期扣點（Expire）
        /// </summary>
        public async Task<PointsMutationResultDto> ExpirePointsAsync(int memberId, PointsExpireRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 冪等性檢查
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

                // 獲得目前餘額
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // 原子更新：安全更新餘額（同 Use 相同的安全 UPDATE）
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE MemberStats SET TotalPoints = TotalPoints - {0}, UpdatedAt = {1} WHERE MemberId = {2} AND TotalPoints >= {3}",
                    request.Amount, now, memberId, request.Amount);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException("點數到期扣點失敗：餘額不足或發生併發");
                }

                // 新增 PointsLog（expired）
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "expired",
                    Amount = request.Amount,
                    Note = request.Note ?? "點數到期",
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance - request.Amount;

                _logger.LogInformation("會員 {MemberId} 點數到期扣點成功，金額: {Amount}，餘額: {BeforeBalance} -> {AfterBalance}",
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
                _logger.LogError(ex, "會員 {MemberId} 點數到期扣點失敗", memberId);
                throw;
            }
        }

        // ======== 新增：簽到功能實作 ========

        /// <summary>
        /// 取得今日簽到資訊
        /// </summary>
        public async Task<CheckinInfoDto> GetCheckinInfoAsync(int memberId)
        {
            try
            {
                var now = DateTime.Now;
                var today = now.Date;
                var todayStr = today.ToString("yyyy-MM-dd");

                // 檢查今天是否已簽到
                var todayCheckin = await _context.PointsLogs
                    .AsNoTracking()
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.CreatedAt.Date == today)
                    .FirstOrDefaultAsync();

                bool signedToday = todayCheckin != null;

                // 計算連續簽到天數
                int checkinStreak = await CalculateCheckinStreakAsync(memberId, today, signedToday);

                // ?? 修復：計算今日獎勵（直接返回 JCoin 整數值，不再除以10）
                int todayReward = CalculateTodayReward(checkinStreak, signedToday);

                return new CheckinInfoDto
                {
                    MemberId = memberId,
                    Today = todayStr,
                    SignedToday = signedToday,
                    CheckinStreak = checkinStreak,
                    TodayReward = todayReward,  // 直接返回整數 JCoin 值
                    ServerTime = now,
                    Unit = "J幣",  // 更新單位顯示
                    Scale = 1      // 改為 1，不需要縮放
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員 {MemberId} 簽到資訊失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 執行簽到
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

                // 冪等性檢查：檢查今天是否已簽到
                var existingCheckin = await _context.PointsLogs
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.VerificationCode == verificationCode)
                    .FirstOrDefaultAsync();

                if (existingCheckin != null)
                {
                    // 已簽到，回傳既有結果
                    var currentBalance = await GetCurrentBalanceFromStats(memberId);
                    var streak = await CalculateCheckinStreakAsync(memberId, today, true);
                    
                    return new CheckinResultDto
                    {
                        MemberId = memberId,
                        SignedToday = true,
                        CheckinStreak = streak,
                        Reward = existingCheckin.Amount,  // ?? 修復：直接使用 Amount，不再除以10
                        BeforeBalance = currentBalance - existingCheckin.Amount,  // ?? 修復：移除除法
                        AfterBalance = currentBalance,  // ?? 修復：移除除法
                        VerificationCode = existingCheckin.VerificationCode ?? "",
                        CreatedAt = existingCheckin.CreatedAt
                    };
                }

                // 計算連續簽到天數（簽到前）
                int checkinStreak = await CalculateCheckinStreakAsync(memberId, today, false);
                
                // 簽到後天數 = 連續天數 + 1
                int newStreak = checkinStreak + 1;
                
                // ?? 修復：計算獎勵（循環 1-7 天）- 直接使用配置值
                int rewardCycle = ((newStreak - 1) % 7) + 1;
                int rewardAmount = CheckinRewards[rewardCycle];  // 這就是要儲存和顯示的值
                
                // 獲得簽到前餘額
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                // 新增簽到記錄
                var checkinLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "signin",
                    Amount = rewardAmount,  // ?? 修復：直接儲存獎勵值（1,2,3...10）
                    Note = "daily check-in",
                    VerificationCode = verificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(checkinLog);
                await _context.SaveChangesAsync();

                // 原子更新 MemberStats
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // 若無資料，建立新記錄
                    memberStat = new MemberStat
                    {
                        MemberId = memberId,
                        TotalPoints = rewardAmount,
                        UpdatedAt = now
                        // Current_Level_Id 保持預設或使用 1
                    };
                    _context.MemberStats.Add(memberStat);
                }
                else
                {
                    // 更新現有記錄
                    memberStat.TotalPoints += rewardAmount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + rewardAmount;

                _logger.LogInformation("會員 {MemberId} 簽到成功，連續天數: {Streak}，獎勵: {Reward} J幣，餘額: {BeforeBalance} -> {AfterBalance}",
                    memberId, newStreak, rewardAmount, beforeBalance, afterBalance);

                return new CheckinResultDto
                {
                    MemberId = memberId,
                    SignedToday = true,
                    CheckinStreak = newStreak,
                    Reward = rewardAmount,  // ?? 修復：直接回傳獎勵值 (1,2,3...10)
                    BeforeBalance = beforeBalance,  // ?? 修復：移除除法，直接顯示整數餘額
                    AfterBalance = afterBalance,    // ?? 修復：移除除法，直接顯示整數餘額
                    VerificationCode = verificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await LogError(memberId, "checkin", ex.Message, new { memberId });
                _logger.LogError(ex, "會員 {MemberId} 簽到失敗", memberId);
                throw;
            }
        }

        /// <summary>
        /// 計算連續簽到天數
        /// </summary>
        private async Task<int> CalculateCheckinStreakAsync(int memberId, DateTime today, bool includeToday)
        {
            try
            {
                // 取得近 60 天的簽到記錄
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

                // 計算連續天數
                int streak = 0;
                var checkDate = today;

                // 如果包含今天且今天有簽到記錄
                if (includeToday && checkinDates.Contains(today))
                {
                    streak = 1;
                    checkDate = today.AddDays(-1);
                }
                else if (!includeToday)
                {
                    // 簽到前，從昨天開始檢查
                    checkDate = today.AddDays(-1);
                }

                // 向前逐日檢查
                while (checkinDates.Contains(checkDate))
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }

                return streak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "計算會員 {MemberId} 連續簽到天數失敗", memberId);
                return 0;
            }
        }

        /// <summary>
        /// ?? 修復：計算今日獎勵（返回 JCoin 整數值，不再除以10）
        /// </summary>
        private int CalculateTodayReward(int checkinStreak, bool signedToday)
        {
            if (signedToday)
            {
                // 已簽到，獎勵為當前 streak 對應的獎勵
                int rewardCycle = ((checkinStreak - 1) % 7) + 1;
                return CheckinRewards[rewardCycle]; // ?? 修復：直接返回配置值，不再除以10
            }
            else
            {
                // 未簽到，獎勵為簽到後的獎勵
                int newStreak = checkinStreak + 1;
                int rewardCycle = ((newStreak - 1) % 7) + 1;
                return CheckinRewards[rewardCycle]; // ?? 修復：直接返回配置值，不再除以10
            }
        }

        /// <summary>
        /// 從 MemberStats 獲得目前餘額
        /// </summary>
        private async Task<int> GetCurrentBalanceFromStats(int memberId)
        {
            var memberStat = await _context.MemberStats
                .AsNoTracking()
                .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

            return memberStat?.TotalPoints ?? 0;
        }

        /// <summary>
        /// 紀錄錯誤到 PointsLogError
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
                _logger.LogError(ex, "紀錄點數錯誤失敗，會員: {MemberId}, 錯誤類型: {ErrorType}", memberId, errorType);
            }
        }
    }
}