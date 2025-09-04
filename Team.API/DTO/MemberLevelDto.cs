using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// �|������ Summary DTO
    /// </summary>
    public class MemberLevelSummaryDto
    {
        /// <summary>
        /// �|��ID
        /// </summary>
        public int MemberId { get; set; }

        /// <summary>
        /// �ֿn���O���B
        /// </summary>
        public int TotalSpent { get; set; }

        /// <summary>
        /// �ثe���Ÿ�T
        /// </summary>
        public LevelInfoDto? CurrentLevel { get; set; }

        /// <summary>
        /// �U�@���Ÿ�T�]�Y�w�O�̰��ūh�� null�^
        /// </summary>
        public LevelInfoDto? NextLevel { get; set; }

        /// <summary>
        /// �ɯŶi��
        /// </summary>
        public LevelProgressDto Progress { get; set; } = new();

        /// <summary>
        /// �̫��s�ɶ�
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// ���Ÿ�T DTO
    /// </summary>
    public class LevelInfoDto
    {
        /// <summary>
        /// ����ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ���ŦW��
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// �һݮ��O���B
        /// </summary>
        public int RequiredAmount { get; set; }

        /// <summary>
        /// �O�_���ҥΪ��A
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// ���Ŵy�z
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// �C���u�f��ID�]��t�^
        /// </summary>
        public int? MonthlyCouponId { get; set; }

        /// <summary>
        /// �C���u�f����D�]��t�^
        /// </summary>
        public string? MonthlyCouponTitle { get; set; }
    }

    /// <summary>
    /// ���Ŷi�� DTO
    /// </summary>
    public class LevelProgressDto
    {
        /// <summary>
        /// �ثe���O���B�]�۹��ثe���Ū��e�^
        /// </summary>
        public int CurrentAmount { get; set; }

        /// <summary>
        /// �Z���U�@�����ٻݭn�����B
        /// </summary>
        public int RequiredForNext { get; set; }

        /// <summary>
        /// �i�צʤ���]0-100�^
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// �O�_�w�F�̰�����
        /// </summary>
        public bool IsMaxLevel { get; set; }
    }

    /// <summary>
    /// ���s�p�⵲�G DTO
    /// </summary>
    public class RecalculateResultDto : MemberLevelSummaryDto
    {
        /// <summary>
        /// �O�_���ɯ�
        /// </summary>
        public bool LevelUp { get; set; }

        /// <summary>
        /// �쵥�Ÿ�T�]�p���ɯš^
        /// </summary>
        public LevelInfoDto? OldLevel { get; set; }

        /// <summary>
        /// �s���Ÿ�T�]�p���ɯš^
        /// </summary>
        public LevelInfoDto? NewLevel { get; set; }

        /// <summary>
        /// ����e���ֿn���O
        /// </summary>
        public int PreviousTotalSpent { get; set; }

        /// <summary>
        /// �q�q��p�⪺�s�ֿn���O
        /// </summary>
        public int RecalculatedTotalSpent { get; set; }
    }
}