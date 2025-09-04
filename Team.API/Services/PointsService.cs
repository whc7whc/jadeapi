using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.DTO;

namespace Team.API.Services
{
    /// <summary>
    /// Points service interface
    /// </summary>
    public interface IPointsService
    {
        Task<PointsBalanceDto?> GetBalanceAsync(int memberId);
        Task<PagedResponseDto<PointHistoryItemDto>> GetHistoryAsync(int memberId, PointsHistoryQueryDto query);
        Task<PointsMutationResultDto> EarnPointsAsync(int memberId, PointsEarnRequestDto request);
        Task<PointsMutationResultDto> UsePointsAsync(int memberId, PointsUseRequestDto request);
        Task<PointsMutationResultDto> RefundPointsAsync(int memberId, PointsRefundRequestDto request);
        Task<PointsMutationResultDto> ExpirePointsAsync(int memberId, PointsExpireRequestDto request);

        // Check-in related methods
        Task<CheckinInfoDto> GetCheckinInfoAsync(int memberId);
        Task<CheckinResultDto> PerformCheckinAsync(int memberId);
    }

    /// <summary>
    /// Points service implementation
    /// </summary>
    public class PointsService : IPointsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PointsService> _logger;

        // Valid points types whitelist
        private static readonly HashSet<string> ValidPointsTypes = new()
        {
            "signin", "used", "refund", "earned", "expired", "adjustment"
        };

        // Check-in reward configuration - directly corresponds to displayed J-Coin amounts
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
        /// Query member points balance
        /// </summary>
        public async Task<PointsBalanceDto?> GetBalanceAsync(int memberId)
        {
            try
            {
                // Query balance from MemberStats (integer)
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

                // If no data found, return balance as 0
                return new PointsBalanceDto
                {
                    MemberId = memberId,
                    Balance = 0,
                    LastUpdatedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query points balance for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Query member points history (paged + filtered)
        /// </summary>
        public async Task<PagedResponseDto<PointHistoryItemDto>> GetHistoryAsync(int memberId, PointsHistoryQueryDto query)
        {
            try
            {
                var pointsQuery = _context.PointsLogs
                    .Where(pl => pl.MemberId == memberId)
                    .AsNoTracking();

                // Type filter
                if (!string.IsNullOrEmpty(query.Type))
                {
                    pointsQuery = pointsQuery.Where(pl => pl.Type == query.Type);
                }

                // Date filter (based on CreatedAt)
                if (query.DateFrom.HasValue)
                {
                    pointsQuery = pointsQuery.Where(pl => pl.CreatedAt >= query.DateFrom.Value);
                }

                if (query.DateTo.HasValue)
                {
                    pointsQuery = pointsQuery.Where(pl => pl.CreatedAt <= query.DateTo.Value);
                }

                // Calculate total count
                var total = await pointsQuery.CountAsync();

                // Sort by CreatedAt DESC and paginate
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
                    Message = "Points history query successful",
                    Data = items,
                    TotalCount = total,
                    CurrentPage = query.Page,
                    ItemsPerPage = query.PageSize,
                    TotalPages = (int)Math.Ceiling((double)total / query.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query points history for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Add points (Earn / Adjustment)
        /// </summary>
        public async Task<PointsMutationResultDto> EarnPointsAsync(int memberId, PointsEarnRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate type whitelist
                if (!ValidPointsTypes.Contains(request.Type))
                {
                    throw new ArgumentException($"Invalid points type: {request.Type}");
                }

                // Idempotency check: if VerificationCode already exists, return existing result
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

                // Get current balance
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // Add PointsLog
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

                // Sync update MemberStats.TotalPoints
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // If no data, create new record
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
                    // Update existing record
                    memberStat.TotalPoints += request.Amount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + request.Amount;

                _logger.LogInformation("Member {MemberId} points earned successfully, type: {Type}, amount: {Amount}, balance: {BeforeBalance} -> {AfterBalance}",
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
                _logger.LogError(ex, "Failed to earn points for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Deduct points (Use)
        /// </summary>
        public async Task<PointsMutationResultDto> UsePointsAsync(int memberId, PointsUseRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Idempotency check
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

                // Get current balance and check
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);
                if (beforeBalance < request.Amount)
                {
                    throw new InvalidOperationException($"Insufficient balance, current: {beforeBalance}, required: {request.Amount}");
                }

                var now = DateTime.Now;

                // Atomic update: safely update balance
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE MemberStats SET TotalPoints = TotalPoints - {0}, UpdatedAt = {1} WHERE MemberId = {2} AND TotalPoints >= {3}",
                    request.Amount, now, memberId, request.Amount);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException("Points deduction failed: insufficient balance or concurrency issue");
                }

                // Add PointsLog (type=used, amount=positive integer kept)
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "used",
                    Amount = request.Amount, // Keep positive integer
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

                _logger.LogInformation("Member {MemberId} points used successfully, amount: {Amount}, balance: {BeforeBalance} -> {AfterBalance}",
                    memberId, request.Amount, beforeBalance, afterBalance);

                return new PointsMutationResultDto
                {
                    MemberId = memberId,
                    BeforeBalance = beforeBalance,
                    ChangeAmount = -request.Amount, // Negative number indicates decrease
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
                
                // Only log errors that are not insufficient balance errors to Points_Log_Error
                if (!ex.Message.Contains("Insufficient balance"))
                {
                    await LogError(memberId, "UsePoints", ex.Message, request);
                }
                
                _logger.LogError(ex, "Failed to use points for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Refund points
        /// </summary>
        public async Task<PointsMutationResultDto> RefundPointsAsync(int memberId, PointsRefundRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Idempotency check
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

                // Get current balance
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // Add PointsLog (refund)
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "refund",
                    Amount = request.Amount,
                    Note = request.Note ?? $"Refund from transaction: {request.SourceTransactionId}",
                    TransactionId = request.SourceTransactionId,
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();

                // Sync add back TotalPoints
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

                _logger.LogInformation("Member {MemberId} points refund successful, amount: {Amount}, source transaction: {SourceTransactionId}, balance: {BeforeBalance} -> {AfterBalance}",
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
                _logger.LogError(ex, "Failed to refund points for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Expire points
        /// </summary>
        public async Task<PointsMutationResultDto> ExpirePointsAsync(int memberId, PointsExpireRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Idempotency check
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

                // Get current balance
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                var now = DateTime.Now;

                // Atomic update: safely update balance (same safe UPDATE as Use)
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE MemberStats SET TotalPoints = TotalPoints - {0}, UpdatedAt = {1} WHERE MemberId = {2} AND TotalPoints >= {3}",
                    request.Amount, now, memberId, request.Amount);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException("Points expiration failed: insufficient balance or concurrency issue");
                }

                // Add PointsLog (expired)
                var pointsLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "expired",
                    Amount = request.Amount,
                    Note = request.Note ?? "Points expired",
                    VerificationCode = request.VerificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(pointsLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance - request.Amount;

                _logger.LogInformation("Member {MemberId} points expiration successful, amount: {Amount}, balance: {BeforeBalance} -> {AfterBalance}",
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
                _logger.LogError(ex, "Failed to expire points for member {MemberId}", memberId);
                throw;
            }
        }

        // Check-in functionality implementation

        /// <summary>
        /// Get today's check-in information
        /// </summary>
        public async Task<CheckinInfoDto> GetCheckinInfoAsync(int memberId)
        {
            try
            {
                var now = DateTime.Now;
                var today = now.Date;
                var todayStr = today.ToString("yyyy-MM-dd");

                // Check if already checked in today
                var todayCheckin = await _context.PointsLogs
                    .AsNoTracking()
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.CreatedAt.Date == today)
                    .FirstOrDefaultAsync();

                bool signedToday = todayCheckin != null;

                // Calculate consecutive check-in days
                int checkinStreak = await CalculateCheckinStreakAsync(memberId, today, signedToday);

                // Calculate today's reward (directly return JCoin integer value, no division by 10)
                int todayReward = CalculateTodayReward(checkinStreak, signedToday);

                return new CheckinInfoDto
                {
                    MemberId = memberId,
                    Today = todayStr,
                    SignedToday = signedToday,
                    CheckinStreak = checkinStreak,
                    TodayReward = todayReward,  // Directly return integer JCoin value
                    ServerTime = now,
                    Unit = "J-Coin",  // Update unit display
                    Scale = 1      // Change to 1, no scaling needed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get check-in info for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Perform check-in
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

                // Idempotency check: check if already checked in today
                var existingCheckin = await _context.PointsLogs
                    .Where(pl => pl.MemberId == memberId && 
                                pl.Type == "signin" && 
                                pl.VerificationCode == verificationCode)
                    .FirstOrDefaultAsync();

                if (existingCheckin != null)
                {
                    // Already checked in, return existing result
                    var currentBalance = await GetCurrentBalanceFromStats(memberId);
                    var streak = await CalculateCheckinStreakAsync(memberId, today, true);
                    
                    return new CheckinResultDto
                    {
                        MemberId = memberId,
                        SignedToday = true,
                        CheckinStreak = streak,
                        Reward = existingCheckin.Amount,  // Directly use Amount, no division by 10
                        BeforeBalance = currentBalance - existingCheckin.Amount,  // Remove division
                        AfterBalance = currentBalance,  // Remove division
                        VerificationCode = existingCheckin.VerificationCode ?? "",
                        CreatedAt = existingCheckin.CreatedAt
                    };
                }

                // Calculate consecutive check-in days (before check-in)
                int checkinStreak = await CalculateCheckinStreakAsync(memberId, today, false);
                
                // Days after check-in = consecutive days + 1
                int newStreak = checkinStreak + 1;
                
                // Calculate reward (cycle 1-7 days) - directly use configuration value
                int rewardCycle = ((newStreak - 1) % 7) + 1;
                int rewardAmount = CheckinRewards[rewardCycle];  // This is the value to store and display
                
                // Get balance before check-in
                var beforeBalance = await GetCurrentBalanceFromStats(memberId);

                // Add check-in record
                var checkinLog = new PointsLog
                {
                    MemberId = memberId,
                    Type = "signin",
                    Amount = rewardAmount,  // Directly store reward value (1,2,3...10)
                    Note = "daily check-in",
                    VerificationCode = verificationCode,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.PointsLogs.Add(checkinLog);
                await _context.SaveChangesAsync();

                // Atomic update MemberStats
                var memberStat = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                if (memberStat == null)
                {
                    // If no data, create new record
                    memberStat = new MemberStat
                    {
                        MemberId = memberId,
                        TotalPoints = rewardAmount,
                        UpdatedAt = now
                        // Current_Level_Id keep default or use 1
                    };
                    _context.MemberStats.Add(memberStat);
                }
                else
                {
                    // Update existing record
                    memberStat.TotalPoints += rewardAmount;
                    memberStat.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var afterBalance = beforeBalance + rewardAmount;

                _logger.LogInformation("Member {MemberId} check-in successful, streak: {Streak}, reward: {Reward} J-Coin, balance: {BeforeBalance} -> {AfterBalance}",
                    memberId, newStreak, rewardAmount, beforeBalance, afterBalance);

                return new CheckinResultDto
                {
                    MemberId = memberId,
                    SignedToday = true,
                    CheckinStreak = newStreak,
                    Reward = rewardAmount,  // Directly return reward value (1,2,3...10)
                    BeforeBalance = beforeBalance,  // Remove division, directly display integer balance
                    AfterBalance = afterBalance,    // Remove division, directly display integer balance
                    VerificationCode = verificationCode,
                    CreatedAt = now
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await LogError(memberId, "checkin", ex.Message, new { memberId });
                _logger.LogError(ex, "Failed to check-in for member {MemberId}", memberId);
                throw;
            }
        }

        /// <summary>
        /// Calculate consecutive check-in days
        /// </summary>
        private async Task<int> CalculateCheckinStreakAsync(int memberId, DateTime today, bool includeToday)
        {
            try
            {
                // Get check-in records for the last 60 days
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

                // Calculate consecutive days
                int streak = 0;
                var checkDate = today;

                // If including today and today has check-in record
                if (includeToday && checkinDates.Contains(today))
                {
                    streak = 1;
                    checkDate = today.AddDays(-1);
                }
                else if (!includeToday)
                {
                    // Before check-in, start checking from yesterday
                    checkDate = today.AddDays(-1);
                }

                // Check backwards day by day
                while (checkinDates.Contains(checkDate))
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }

                return streak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate check-in streak for member {MemberId}", memberId);
                return 0;
            }
        }

        /// <summary>
        /// Calculate today's reward (return JCoin integer value, no division by 10)
        /// </summary>
        private int CalculateTodayReward(int checkinStreak, bool signedToday)
        {
            if (signedToday)
            {
                // Already checked in, reward is the current streak's corresponding reward
                int rewardCycle = ((checkinStreak - 1) % 7) + 1;
                return CheckinRewards[rewardCycle]; // Directly return configuration value, no division by 10
            }
            else
            {
                // Not checked in, reward is the reward after check-in
                int newStreak = checkinStreak + 1;
                int rewardCycle = ((newStreak - 1) % 7) + 1;
                return CheckinRewards[rewardCycle]; // Directly return configuration value, no division by 10
            }
        }

        /// <summary>
        /// Get current balance from MemberStats
        /// </summary>
        private async Task<int> GetCurrentBalanceFromStats(int memberId)
        {
            var memberStat = await _context.MemberStats
                .AsNoTracking()
                .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

            return memberStat?.TotalPoints ?? 0;
        }

        /// <summary>
        /// Log error to PointsLogError
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
                _logger.LogError(ex, "Failed to log points error, member: {MemberId}, error type: {ErrorType}", memberId, errorType);
            }
        }
    }
}