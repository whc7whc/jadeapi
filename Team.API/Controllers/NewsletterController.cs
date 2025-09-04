using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsletterController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public NewsletterController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] NewsletterRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest("Email 必填");

            // 這裡你也可以存到資料庫 (方便以後行銷)
            // _context.Subscribers.Add(new Subscriber { Email = request.Email });
            // await _context.SaveChangesAsync();

            // 立即寄送優惠券
            string subject = "🎁 歡迎加入！這是您的專屬優惠券";
            string body = "感謝您訂閱我們的電子報，這是您專屬的 85 折優惠券：WELCOME15。結帳時輸入此優惠碼即可享有折扣。趕快逛逛我們的新款商品吧！";

            await _emailService.SendEmailAsync(request.Email, subject, body);

            return Ok(new { success = true, message = "優惠券已寄送" });
        }
    }
    public class NewsletterRequest
    {
        public string Email { get; set; }
    }
}
