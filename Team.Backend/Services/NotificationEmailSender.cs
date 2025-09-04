using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Team.Backend.Models.EfModel;
using Microsoft.Extensions.Logging;

namespace Team.Backend.Services
{
	public class NotificationEmailSender : INotificationEmailSender
	{
		private readonly IConfiguration _config;
		private readonly ILogger<NotificationEmailSender> _logger;

		public NotificationEmailSender(IConfiguration config, ILogger<NotificationEmailSender> logger)
		{
			_config = config;
			_logger = logger;
		}

		public async Task<bool> SendNotificationEmailAsync(Notification notification)
		{
			if (notification == null)
				throw new ArgumentNullException(nameof(notification), "通知不可為空");

			if (string.IsNullOrWhiteSpace(notification.Email_Address))
				throw new ArgumentException("收件人信箱不可為空", nameof(notification.Email_Address));

			try
			{
				_logger.LogInformation("準備發送通知郵件至 {Email}，通知ID: {Id}", notification.Email_Address, notification.Id);

				var email = new MimeKit.MimeMessage();
				email.From.Add(new MailboxAddress(
					_config["EmailSettings:FromName"],
					_config["EmailSettings:FromEmail"]));
				email.To.Add(MailboxAddress.Parse(notification.Email_Address));

				// 根據通知類別設定主旨
				string subject = GetSubjectByCategory(notification.Category);
				email.Subject = subject;

				// 建立HTML內容
				var body = GenerateEmailBody(notification);
				var builder = new BodyBuilder { HtmlBody = body };
				email.Body = builder.ToMessageBody();

				// 連接SMTP伺服器並發送
				using var smtp = new SmtpClient();
				await smtp.ConnectAsync(
					_config["EmailSettings:SmtpHost"],
					int.Parse(_config["EmailSettings:SmtpPort"]),
					SecureSocketOptions.StartTls);

				await smtp.AuthenticateAsync(
					_config["EmailSettings:SmtpUser"],
					_config["EmailSettings:SmtpPass"]);

				await smtp.SendAsync(email);
				await smtp.DisconnectAsync(true);

				_logger.LogInformation("成功發送通知郵件至 {Email}，通知ID: {Id}", notification.Email_Address, notification.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "發送通知郵件失敗，收件人: {Email}，通知ID: {Id}", notification.Email_Address, notification.Id);
				return false;
			}
		}

		private string GetSubjectByCategory(string category)
		{
			// 根據通知類別設定合適的主旨
			return category.ToLower() switch
			{
				"order" => "【JADE電商】訂單通知",
				"payment" => "【JADE電商】付款通知",
				"account" => "【JADE電商】帳戶通知",
				"security" => "【JADE電商】安全通知",
				"promotion" => "【JADE電商】促銷活動通知",
				"system" => "【JADE電商】系統通知",
				"restock" => "【JADE電商】補貨通知",
				_ => "【JADE電商】重要通知"
			};
		}

		private string GenerateEmailBody(Notification notification)
		{
			// 建立美觀的HTML郵件內容
			return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>JADE電商通知</title>
    <style>
        body {{
            font-family: Arial, '微軟正黑體', sans-serif;
            line-height: 1.6;
            color: #333;
            background-color: #f9f9f9;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 8px rgba(0,0,0,0.05);
        }}
        .email-header {{
            background-color: #4a6cf7;
            color: white;
            padding: 20px;
            text-align: center;
        }}
        .email-body {{
            padding: 30px;
        }}
        .email-footer {{
            background-color: #f5f5f5;
            padding: 15px;
            text-align: center;
            font-size: 12px;
            color: #666;
        }}
        .category-badge {{
            display: inline-block;
            padding: 5px 10px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: bold;
            margin-bottom: 15px;
            color: white;
        }}
        .category-order {{ background-color: #4CAF50; }}
        .category-payment {{ background-color: #2196F3; }}
        .category-account {{ background-color: #9C27B0; }}
        .category-security {{ background-color: #FF5722; }}
        .category-promotion {{ background-color: #E91E63; }}
        .category-system {{ background-color: #607D8B; }}
        .category-restock {{ background-color: #00BCD4; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='email-header'>
            <h2>JADE 電商服飾</h2>
        </div>
        <div class='email-body'>
            <div class='category-badge category-{notification.Category.ToLower()}'>{GetCategoryLabel(notification.Category)}</div>
            <h3>親愛的會員，您好：</h3>
            <div>
                {notification.Message}
            </div>
            <div style='margin-top: 30px; font-size: 13px;'>
                <p>此為系統自動發送郵件，請勿直接回覆。</p>
                <p>若有任何問題，請聯繫我們的客服團隊。</p>
            </div>
        </div>
        <div class='email-footer'>
            <p>© {DateTime.Now.Year} JADE電商服飾 | 隱私權政策 | 使用條款</p>
            <p>本郵件通知於 {DateTime.Now:yyyy/MM/dd HH:mm:ss} 發送</p>
        </div>
    </div>
</body>
</html>";
		}

		private string GetCategoryLabel(string category)
		{
			// 轉換分類為中文標籤
			return category.ToLower() switch
			{
				"order" => "訂單通知",
				"payment" => "付款通知",
				"account" => "帳戶通知",
				"security" => "安全通知",
				"promotion" => "促銷通知",
				"system" => "系統通知",
				"restock" => "補貨通知",
				_ => "一般通知"
			};
		}
	}

	public interface INotificationEmailSender
	{
		Task<bool> SendNotificationEmailAsync(Notification notification);
	}
}