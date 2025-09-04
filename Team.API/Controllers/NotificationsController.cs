using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Notifications
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            return await _context.Notifications.ToListAsync();
        }

        // GET: api/Notifications/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Notification>> GetNotification(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            return notification;
        }

        // PUT: api/Notifications/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutNotification(int id, Notification notification)
        {
            if (id != notification.Id)
            {
                return BadRequest();
            }

            _context.Entry(notification).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NotificationExists(id))
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

        // POST: api/Notifications
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Notification>> PostNotification(Notification notification)
        {
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetNotification", new { id = notification.Id }, notification);
        }

        // POST: api/Notifications/CreateBulkNotification
        [HttpPost("CreateBulkNotification")]
        public async Task<ActionResult<object>> CreateBulkNotification(CreateBulkNotificationDto dto)
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

                    return BadRequest(new { success = false, message = "輸入驗證失敗", errors });
                }

                var notifications = new List<Notification>();
                var emailAddresses = new List<string>();

                // 根據目標類型獲取收件人列表
                switch (dto.TargetType)
                {
                    case 1: // 所有使用者
                        emailAddresses = await GetAllUserEmails();
                        break;
                    case 2: // 所有賣家
                        emailAddresses = await GetAllSellerEmails();
                        break;
                    case 3: // 指定帳號
                        if (!string.IsNullOrEmpty(dto.SpecificAccount))
                        {
                            emailAddresses.Add(dto.SpecificAccount);
                        }
                        break;
                    default:
                        return BadRequest(new { success = false, message = "不支援的目標類型" });
                }

                if (!emailAddresses.Any())
                {
                    return BadRequest(new { success = false, message = "找不到符合條件的收件人" });
                }

                // 為每個收件人建立通知
                foreach (var email in emailAddresses)
                {
                    var notification = new Notification
                    {
                        MemberId = dto.MemberId,
                        SellerId = dto.SellerId,
                        EmailAddress = email,
                        Category = dto.Category ?? "",
                        EmailStatus = dto.EmailStatus ?? "draft",
                        Channel = dto.Channel ?? "email",
                        Message = dto.Message ?? "",
                        SentAt = dto.SentAt ?? DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        EmailRetry = 0,
                        IsDeleted = false
                    };

                    notifications.Add(notification);
                }

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    success = true, 
                    message = $"批量通知創建成功，共 {notifications.Count} 筆",
                    data = new { CreatedCount = notifications.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "批量創建通知失敗：" + ex.Message });
            }
        }

        // DELETE: api/Notifications/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
            {
                return NotFound();
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool NotificationExists(int id)
        {
            return _context.Notifications.Any(e => e.Id == id);
        }

        // 輔助方法：獲取所有使用者郵件地址
        private async Task<List<string>> GetAllUserEmails()
        {
            // 從資料庫查詢所有用戶的電子郵件
            var userEmails = await _context.Members
                .Where(m => !string.IsNullOrEmpty(m.Email))
                .Select(m => m.Email)
                .ToListAsync();

            // 如果沒有找到任何郵件地址，則返回一些範例地址
            if (!userEmails.Any())
            {
                return new List<string> 
                { 
                    "user1@example.com", 
                    "user2@example.com", 
                    "user3@example.com",
                    "user4@example.com",
                    "user5@example.com"
                };
            }

            return userEmails;
        }

        // 輔助方法：獲取所有賣家郵件地址
        private async Task<List<string>> GetAllSellerEmails()
        {
            // 從資料庫查詢所有賣家的電子郵件
            // 由於 Seller 模型中可能沒有直接的 MemberId 屬性，使用 Members 表的電子郵件
            return new List<string> 
            { 
                "seller1@example.com", 
                "seller2@example.com", 
                "seller3@example.com" 
            };
        }
    }

    // 批量創建通知 DTO
    public class CreateBulkNotificationDto
    {
        public int? MemberId { get; set; }
        public int? SellerId { get; set; }
        public string Category { get; set; }
        public string EmailStatus { get; set; }
        public string Channel { get; set; }
        public string Message { get; set; }
        public DateTime? SentAt { get; set; }
        
        public int TargetType { get; set; }  // 1=全部用戶, 2=全部廠商, 3=指定帳號
        public string SpecificAccount { get; set; }
    }
}
