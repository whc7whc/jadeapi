// Notification.cs - �q����Ƽҫ����O�A�s�W�ɶ��M��s/�إ߮ɶ��C

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

        // ���p�ݩ�
        public virtual Member Member { get; set; }

        public virtual Seller Seller { get; set; }

        // ���F�P�ɤ䴩�{���X�D�Ϊk�A�K�[�ݩʬM�g
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

        // ��Ū�ݩ� - �Ω���ܼ���
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

        // �S�O�B�zSpecificAccount���ݩ�
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
                "order" => "�q��q��",
                "payment" => "�I�ڳq��",
                "account" => "�b��q��",
                "security" => "�w���q��",
                "promotion" => "�u�f�q��",
                "system" => "�t�γq��",
                "restock" => "�ɳf�q��",
                _ => Category ?? "�������O"
            };
        }

        private string GetEmailStatusLabel()
        {
            return Email_Status?.ToLower() switch
            {
                "immediate" => "�ߧY�o�e",
                "scheduled" => "�Ƶ{�o�e",
                "draft" => "��Z",
                _ => Email_Status ?? "�������A"
            };
        }

        private string GetChannelLabel()
        {
            return Channel?.ToLower() switch
            {
                "email" => "�q�l�l��",
                "push" => "�����q��",
                "internal" => "�����q��",
                _ => Channel ?? "�����޹D"
            };
        }
    }
}