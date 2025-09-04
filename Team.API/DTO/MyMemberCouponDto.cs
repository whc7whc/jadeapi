using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// �|�������u�f�� DTO - �󥭤Ƴ]�p�A�]�t�|�������M�u�f��w�q��T
    /// </summary>
    public class MyMemberCouponDto
    {
        // �|�������h�]Member_Coupons�^
        public int MemberCouponId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public int? OrderId { get; set; }
        public string VerificationCode { get; set; } = string.Empty;

        // ��w�q�h�]Coupons�^
        public int CouponId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public int DiscountAmount { get; set; }
        public int? MinSpend { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public bool IsActive { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public int? SellersId { get; set; }
        public int? CategoryId { get; set; }
        public int? ApplicableLevelId { get; set; }

        // �l�����
        public string Source => SellersId.HasValue && SellersId > 0 ? "seller" : "platform";
        public string? SellerName { get; set; }

        // �榡��������
        public string FormattedDiscount => DiscountType?.ToLower() switch
        {
            "%�Ƨ馩" or "percentage" => $"{DiscountAmount}% �馩",
            "j���^�X" or "�I�ƪ���" => $"{DiscountAmount} J���^�X",
            "����" => $"�� ${MinSpend} �� ${DiscountAmount}",
            _ => $"{DiscountAmount}"
        };

        public string ValidityPeriod => $"{StartAt:yyyy-MM-dd} ~ {ExpiredAt:yyyy-MM-dd}";
        
        public string UsageInfo => UsageLimit.HasValue 
            ? $"{UsedCount}/{UsageLimit}" 
            : $"{UsedCount}/�L��";

        public bool IsCurrentlyActive => Status == "active";
    }

    /// <summary>
    /// �|���u�f��d�߰Ѽ� DTO
    /// </summary>
    public class MemberCouponQueryDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "�|��ID�����j�� 0")]
        public int MemberId { get; set; }

        /// <summary>
        /// �O�_�u�^�u�ثe�i�Ρv��������
        /// </summary>
        public bool ActiveOnly { get; set; } = false;

        /// <summary>
        /// ���A�z��: active|used|expired|cancelled
        /// </summary>
        public string Status { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "���X�����j�� 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "�C�����ƥ����b 1-100 ����")]
        public int PageSize { get; set; } = 20;
    }
}