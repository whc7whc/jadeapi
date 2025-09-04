using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// �|�����Ŷ��� DTO
    /// </summary>
    public class MembershipLevelItemDto
    {
        /// <summary>
        /// ����ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ���ŦW��
        /// </summary>
        public string LevelName { get; set; } = string.Empty;

        /// <summary>
        /// �һݮ��O���B
        /// </summary>
        public int RequiredAmount { get; set; }

        /// <summary>
        /// �O�_�ҥ�
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// ���Ŵy�z�]��t���^
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// �C��t��ID�]��t���^
        /// </summary>
        public int? MonthlyCouponId { get; set; }
    }

    /// <summary>
    /// �|�����Ųέp DTO
    /// </summary>
    public class MembershipLevelsStatsDto
    {
        /// <summary>
        /// �`���ż�
        /// </summary>
        public int TotalLevels { get; set; }

        /// <summary>
        /// �ҥε��ż�
        /// </summary>
        public int ActiveLevels { get; set; }

        /// <summary>
        /// ���ε��ż�
        /// </summary>
        public int InactiveLevels { get; set; }

        /// <summary>
        /// �̧C���e���B
        /// </summary>
        public int MinRequiredAmount { get; set; }

        /// <summary>
        /// �̰����e���B
        /// </summary>
        public int MaxRequiredAmount { get; set; }
    }
}