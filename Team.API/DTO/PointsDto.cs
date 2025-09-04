using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// �I�ƾl�B�d�ߦ^�� DTO
    /// </summary>
    public class PointsBalanceDto
    {
        public int MemberId { get; set; }
        public int Balance { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// �I�ƾ��v�������� DTO
    /// </summary>
    public class PointHistoryItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string? Note { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// �I�Ʋ��ʵ��G DTO
    /// </summary>
    public class PointsMutationResultDto
    {
        public int MemberId { get; set; }
        public int BeforeBalance { get; set; }
        public int ChangeAmount { get; set; }
        public int AfterBalance { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public string? VerificationCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// �[�I�ШD DTO
    /// </summary>
    public class PointsEarnRequestDto
    {
        [Required(ErrorMessage = "���B������")]
        [Range(1, int.MaxValue, ErrorMessage = "���B�����j�� 0")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "����������")]
        [RegularExpression("^(earned|adjustment)$", ErrorMessage = "�����u��O earned �� adjustment")]
        public string Type { get; set; } = string.Empty;

        public string? Note { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public string? TransactionId { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// ���I�ШD DTO
    /// </summary>
    public class PointsUseRequestDto
    {
        [Required(ErrorMessage = "���B������")]
        [Range(1, int.MaxValue, ErrorMessage = "���B�����j�� 0")]
        public int Amount { get; set; }

        public string? Note { get; set; }

        public string? TransactionId { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// �^�ɽШD DTO
    /// </summary>
    public class PointsRefundRequestDto
    {
        [Required(ErrorMessage = "���B������")]
        [Range(1, int.MaxValue, ErrorMessage = "���B�����j�� 0")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "�ӷ����ID������")]
        public string SourceTransactionId { get; set; } = string.Empty;

        public string? Note { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// ������I�ШD DTO
    /// </summary>
    public class PointsExpireRequestDto
    {
        [Required(ErrorMessage = "���B������")]
        [Range(1, int.MaxValue, ErrorMessage = "���B�����j�� 0")]
        public int Amount { get; set; }

        public string? Note { get; set; }

        public string? VerificationCode { get; set; }
    }

    /// <summary>
    /// �I�ƾ��v�d�߰Ѽ� DTO
    /// </summary>
    public class PointsHistoryQueryDto
    {
        /// <summary>
        /// �����z��: signin|used|refund|earned|expired|adjustment
        /// </summary>
        [RegularExpression("^(signin|used|refund|earned|expired|adjustment)?$", 
            ErrorMessage = "�����u��O signin, used, refund, earned, expired, adjustment �Ϊŭ�")]
        public string? Type { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "���X�����j�� 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "�C�����ƥ����b 1-100 ����")]
        public int PageSize { get; set; } = 20;
    }

    // ======== ñ����� DTO ========

    /// <summary>
    /// ñ���T�^�� DTO
    /// </summary>
    public class CheckinInfoDto
    {
        /// <summary>
        /// �|��ID
        /// </summary>
        public int MemberId { get; set; }

        /// <summary>
        /// ���Ѥ�� (YYYY-MM-DD)
        /// </summary>
        public string Today { get; set; } = string.Empty;

        /// <summary>
        /// ���ѬO�_�wñ��
        /// </summary>
        public bool SignedToday { get; set; }

        /// <summary>
        /// �s��ñ��Ѽ�
        /// </summary>
        public int CheckinStreak { get; set; }

        /// <summary>
        /// ������y (��� J��)
        /// </summary>
        public int TodayReward { get; set; }

        /// <summary>
        /// ���A���ɶ�
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// ��ܳ��
        /// </summary>
        public string Unit { get; set; } = "J��";

        /// <summary>
        /// ������ (�{�b�T�w�� 1�A���ݭn�Y��)
        /// </summary>
        public int Scale { get; set; } = 1;
    }

    /// <summary>
    /// ?? �״_�Gñ�����^�� DTO - �l�B�אּ���
    /// </summary>
    public class CheckinResultDto
    {
        /// <summary>
        /// �|��ID
        /// </summary>
        public int MemberId { get; set; }

        /// <summary>
        /// ���ѬO�_�wñ��
        /// </summary>
        public bool SignedToday { get; set; }

        /// <summary>
        /// �s��ñ��Ѽ�
        /// </summary>
        public int CheckinStreak { get; set; }

        /// <summary>
        /// �������y (��� J��)
        /// </summary>
        public int Reward { get; set; }

        /// <summary>
        /// ?? �״_�Gñ��e�l�B (��ƭȡA�������)
        /// </summary>
        public int BeforeBalance { get; set; }

        /// <summary>
        /// ?? �״_�Gñ���l�B (��ƭȡA�������)
        /// </summary>
        public int AfterBalance { get; set; }

        /// <summary>
        /// ���ҽX
        /// </summary>
        public string VerificationCode { get; set; } = string.Empty;

        /// <summary>
        /// �إ߮ɶ�
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// ñ��ШD DTO (���F�ۮe�ʫO�d)
    /// </summary>
    public class CheckinRequestDto
    {
        public int MemberId { get; set; }
    }
}