using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.DTO;
using Team.API.Services;
using Team.API.Models.DTOs; // 使用正確的分頁DTO命名空間
using System.ComponentModel.DataAnnotations;

namespace Team.API.Controllers
{
    /// <summary>
    /// 會員等級 API 控制器
    /// 
    /// 🔓 公開查詢端點：等級清單與統計允許匿名存取
    /// 📊 快取：使用記憶體快取 60 秒，讀多寫少最適化
    /// 📄 分頁：嚴格限制 pageSize 上限 500，避免大量資料查詢
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MembershipLevelsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMembershipLevelPublicService _publicService;
        private readonly ILogger<MembershipLevelsController> _logger;

        public MembershipLevelsController(
            AppDbContext context,
            IMembershipLevelPublicService publicService,
            ILogger<MembershipLevelsController> logger)
        {
            _context = context;
            _publicService = publicService;
            _logger = logger;
        }

        #region 新增：公開查詢端點（會員等級清單與統計）

        /// <summary>
        /// 取得會員等級清單（分頁）
        /// 
        /// 排序：Required_Amount 升冪（門檻由低到高），其次 Id ASC
        /// 快取：60秒記憶體快取，參數組合為快取鍵
        /// 
        /// 範例請求：
        /// - GET /api/MembershipLevels?activeOnly=true&page=1&pageSize=10
        /// - GET /api/MembershipLevels?includeDescription=true&includeMonthlyCoupon=true
        /// - GET /api/MembershipLevels?activeOnly=false&page=2&pageSize=50
        /// </summary>
        /// <param name="activeOnly">是否只回啟用中的等級（預設 true）</param>
        /// <param name="includeDescription">是否包含描述欄位（預設 false，避免列表冗長）</param>
        /// <param name="includeMonthlyCoupon">是否包含每月配券ID（預設 false）</param>
        /// <param name="page">頁碼（預設 1，最小 1）</param>
        /// <param name="pageSize">每頁筆數（預設 100，最大 500）</param>
        /// <returns>
        /// 分頁容器：
        /// {
        ///   "success": true,
        ///   "message": "取得會員等級清單成功",
        ///   "data": [
        ///     { "id": 1, "levelName": "銅卡", "requiredAmount": 0, "isActive": true },
        ///     { "id": 2, "levelName": "銀卡", "requiredAmount": 1000, "isActive": true }
        ///   ],
        ///   "totalCount": 4,
        ///   "currentPage": 1,
        ///   "itemsPerPage": 100,
        ///   "totalPages": 1
        /// }
        /// </returns>
        [HttpGet]
        public async Task<ActionResult<PagedResponseDto<MembershipLevelItemDto>>> GetMembershipLevels(
            [FromQuery] bool activeOnly = true,
            [FromQuery] bool includeDescription = false,
            [FromQuery] bool includeMonthlyCoupon = false,
            [FromQuery, Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")] int page = 1,
            [FromQuery, Range(1, 500, ErrorMessage = "每頁筆數必須在 1-500 之間")] int pageSize = 100)
        {
            try
            {
                // 參數驗證（ModelState 會自動處理 Range 驗證）
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key, 
                            kvp => kvp.Value!.Errors.First().ErrorMessage
                        );

                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "參數格式錯誤", 
                        errors = errors 
                    });
                }

                _logger.LogInformation("API 請求：取得會員等級清單 - activeOnly:{ActiveOnly}, includeDescription:{IncludeDescription}, includeMonthlyCoupon:{IncludeMonthlyCoupon}, page:{Page}, pageSize:{PageSize}",
                    activeOnly, includeDescription, includeMonthlyCoupon, page, pageSize);

                var result = await _publicService.GetMembershipLevelsAsync(
                    activeOnly, includeDescription, includeMonthlyCoupon, page, pageSize);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級清單 API 失敗");
                return StatusCode(500, new PagedResponseDto<MembershipLevelItemDto>
                {
                    Success = false,
                    Message = "伺服器錯誤：" + ex.Message,
                    Data = new List<MembershipLevelItemDto>(),
                    TotalCount = 0,
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalPages = 0
                });
            }
        }

        /// <summary>
        /// 取得會員等級統計資訊
        /// 
        /// 快取：60秒記憶體快取
        /// 
        /// 範例請求：
        /// - GET /api/MembershipLevels/Stats
        /// - GET /api/MembershipLevels/Stats?activeOnly=false
        /// </summary>
        /// <param name="activeOnly">是否只統計啟用中的等級（預設 true）</param>
        /// <returns>
        /// 統計資訊：
        /// {
        ///   "totalLevels": 4,
        ///   "activeLevels": 3,
        ///   "inactiveLevels": 1,
        ///   "minRequiredAmount": 0,
        ///   "maxRequiredAmount": 10000
        /// }
        /// </returns>
        [HttpGet("Stats")]
        public async Task<ActionResult<MembershipLevelsStatsDto>> GetMembershipLevelsStats(
            [FromQuery] bool activeOnly = true)
        {
            try
            {
                _logger.LogInformation("API 請求：取得會員等級統計 - activeOnly:{ActiveOnly}", activeOnly);

                var stats = await _publicService.GetMembershipLevelsStatsAsync(activeOnly);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級統計 API 失敗");
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = "伺服器錯誤：" + ex.Message 
                });
            }
        }

        #endregion

        #region 原有端點：管理用途（保留現有功能）

        // GET: api/MembershipLevels/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<MembershipLevelItemDto>> GetMembershipLevel(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "參數格式錯誤：等級ID必須大於 0" 
                    });
                }

                _logger.LogInformation("API 請求：取得單一會員等級 - Id:{Id}", id);

                var level = await _publicService.GetMembershipLevelByIdAsync(id);

                if (level == null)
                {
                    return NotFound(new 
                    { 
                        success = false, 
                        message = "找不到指定的會員等級" 
                    });
                }

                return Ok(level);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得單一會員等級 API 失敗：Id={Id}", id);
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = "伺服器錯誤：" + ex.Message 
                });
            }
        }

        // PUT: api/MembershipLevels/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMembershipLevel(int id, MembershipLevel membershipLevel)
        {
            if (id != membershipLevel.Id)
            {
                return BadRequest();
            }

            _context.Entry(membershipLevel).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MembershipLevelExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/MembershipLevels
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<MembershipLevel>> PostMembershipLevel(MembershipLevel membershipLevel)
        {
            _context.MembershipLevels.Add(membershipLevel);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMembershipLevel", new { id = membershipLevel.Id }, membershipLevel);
        }

        // DELETE: api/MembershipLevels/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMembershipLevel(int id)
        {
            var membershipLevel = await _context.MembershipLevels.FindAsync(id);
            if (membershipLevel == null)
            {
                return NotFound();
            }

            _context.MembershipLevels.Remove(membershipLevel);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MembershipLevelExists(int id)
        {
            return _context.MembershipLevels.Any(e => e.Id == id);
        }

        #endregion
    }
}
