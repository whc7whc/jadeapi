using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.DTOs;

namespace Team.Backend.Controllers
{
    [Route("MembershipLevels")]
    public class MembershipLevelsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MembershipLevelsController> _logger;

        public MembershipLevelsController(AppDbContext context, ILogger<MembershipLevelsController> logger)
            : base(context, logger)
        {
            _context = context;
            _logger = logger;
        }

        // 主要頁面
        [HttpGet("")]
        public IActionResult Index() => View();

        // GET: 取得會員等級列表
        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            try
            {
                var levels = await _context.MembershipLevels
                    .Select(m => new MembershipLevelListItemDto
                    {
                        Id = m.Id,
                        LevelName = m.LevelName,
                        RequiredAmount = m.RequiredAmount,
                        IsActive = m.IsActive,
                        CreatedAt = m.CreatedAt,
                    })
                    .OrderBy(m => m.RequiredAmount)
                    .ToListAsync();

                return Json(new { success = true, message = "會員等級列表載入成功", data = levels });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得會員等級列表失敗");
                return Json(new { success = false, message = "取得會員等級列表失敗：" + ex.Message });
            }
        }

        // POST: 新增等級
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] CreateMembershipLevelDto dto)
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

                    return Json(new { success = false, message = "輸入資料格式錯誤", errors = errors });
                }

                // 檢查名稱是否重複
                var existingLevel = await _context.MembershipLevels
                    .FirstOrDefaultAsync(m => m.LevelName == dto.LevelName);
                if (existingLevel != null)
                {
                    return Json(new { success = false, message = "等級名稱已存在" });
                }


                var level = new MembershipLevel
                {
                    LevelName = dto.LevelName,
                    RequiredAmount = dto.RequiredAmount,
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.Now
                };

                _context.MembershipLevels.Add(level);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "等級新增成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新增等級失敗");
                return Json(new { success = false, message = "新增等級失敗：" + ex.Message });
            }
        }

        // PUT: 更新等級  /MembershipLevels/Update/{id}
        [HttpPut("Update/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMembershipLevelDto dto)
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

                    return Json(new { success = false, message = "輸入資料格式錯誤", errors = errors });
                }

                var level = await _context.MembershipLevels.FindAsync(id);
                if (level == null)
                {
                    return Json(new { success = false, message = "找不到指定的等級" });
                }

                // 檢查名稱是否重複（排除自己）
                var existingLevel = await _context.MembershipLevels
                    .FirstOrDefaultAsync(m => m.LevelName == dto.LevelName && m.Id != id);
                if (existingLevel != null)
                {
                    return Json(new { success = false, message = "等級名稱已存在" });
                }


                level.LevelName = dto.LevelName;
                level.RequiredAmount = dto.RequiredAmount;
                level.IsActive = dto.IsActive;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "等級更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新等級失敗");
                return Json(new { success = false, message = "更新等級失敗：" + ex.Message });
            }
        }

        // DELETE: 刪除等級  /MembershipLevels/Delete/{id}
        [HttpDelete("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var level = await _context.MembershipLevels.FindAsync(id);
                if (level == null)
                {
                    return Json(new { success = false, message = "找不到指定的等級" });
                }

                // 檢查是否有會員正在使用此等級
                var membersUsingLevel = await _context.MemberStats
                    .AnyAsync(ms => ms.CurrentLevelId == id);
                if (membersUsingLevel)
                {
                    return Json(new { success = false, message = "無法刪除：有會員正在使用此等級" });
                }

                _context.MembershipLevels.Remove(level);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "等級刪除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除等級失敗");
                return Json(new { success = false, message = "刪除等級失敗：" + ex.Message });
            }
        }

    }
}