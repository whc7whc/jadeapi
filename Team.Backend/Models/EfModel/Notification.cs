// Notification.cs - 通知資料模型類別，新增時間和更新/建立時間列

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Team.Backend.Models.EfModel
{
    [Table("Notification")]
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public int? Member_Id { get; set; }

        public int? Seller_Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string Category { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; }

        public DateTime Sent_At { get; set; }

        [MaxLength(256)]
        public string Email_Address { get; set; }

        [MaxLength(20)]
        public string Email_Status { get; set; }

        public DateTime? Email_Sent_At { get; set; }

        public int Email_Retry { get; set; } = 0;

        [MaxLength(20)]
        public string Channel { get; set; }

        public DateTime Created_At { get; set; } = DateTime.Now;

        public DateTime Updated_At { get; set; } = DateTime.Now;

        public bool Is_Deleted { get; set; } = false;

        // 關聯屬性
        public virtual Member Member { get; set; }

        public virtual Seller Seller { get; set; }

        // 為了同時支援程式碼慣用法，添加屬性映射
        [NotMapped]
        public int? MemberId
        {
            get => Member_Id;
            set => Member_Id = value;
        }

        [NotMapped]
        public int? SellerId
        {
            get => Seller_Id;
            set => Seller_Id = value;
        }

        [NotMapped]
        public DateTime SentAt
        {
            get => Sent_At;
            set => Sent_At = value;
        }

        [NotMapped]
        public string EmailAddress
        {
            get => Email_Address;
            set => Email_Address = value;
        }

        [NotMapped]
        public string EmailStatus
        {
            get => Email_Status;
            set => Email_Status = value;
        }

        [NotMapped]
        public DateTime? EmailSentAt
        {
            get => Email_Sent_At;
            set => Email_Sent_At = value;
        }

        [NotMapped]
        public int EmailRetry
        {
            get => Email_Retry;
            set => Email_Retry = value;
        }

        [NotMapped]
        public bool IsDeleted
        {
            get => Is_Deleted;
            set => Is_Deleted = value;
        }

        [NotMapped]
        public DateTime CreatedAt
        {
            get => Created_At;
            set => Created_At = value;
        }

        [NotMapped]
        public DateTime UpdatedAt
        {
            get => Updated_At;
            set => Updated_At = value;
        }

        // 唯讀屬性 - 用於顯示標籤
        [NotMapped]
        public string CategoryLabel => GetCategoryLabel();

        [NotMapped]
        public string EmailStatusLabel => GetEmailStatusLabel();

        [NotMapped]
        public string ChannelLabel => GetChannelLabel();

        [NotMapped]
        public string FormattedSentAt => Sent_At.ToString("yyyy/MM/dd HH:mm");

        [NotMapped]
        public string FormattedCreatedAt => Created_At.ToString("yyyy/MM/dd HH:mm");

        [NotMapped]
        public string FormattedUpdatedAt => Updated_At.ToString("yyyy/MM/dd HH:mm");

        // 特別處理SpecificAccount的屬性
        [NotMapped]
        public string SpecificAccount
        {
            get => Email_Address;
            set => Email_Address = value;
        }

        private string GetCategoryLabel()
        {
            return Category?.ToLower() switch
            {
                "order" => "訂單通知",
                "payment" => "付款通知",
                "account" => "帳戶通知",
                "security" => "安全通知",
                "promotion" => "優惠通知",
                "system" => "系統通知",
                "restock" => "補貨通知",
                _ => Category ?? "未知類別"
            };
        }

        private string GetEmailStatusLabel()
        {
            return Email_Status?.ToLower() switch
            {
                "immediate" => "立即發送",
                "scheduled" => "排程發送",
                "draft" => "草稿",
                _ => Email_Status ?? "未知狀態"
            };
        }

        private string GetChannelLabel()
        {
            return Channel?.ToLower() switch
            {
                "email" => "電子郵件",
                "push" => "推播通知",
                "internal" => "站內通知",
                _ => Channel ?? "未知管道"
            };
        }
    }
}