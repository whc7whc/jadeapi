using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    /// <summary>
    /// 審核貼文的 DTO
    /// </summary>
    public class ApprovePostDto
    {
        /// <summary>
        /// 是否核准
        /// </summary>
        [Required]
        public bool Approved { get; set; }

        /// <summary>
        /// 審核意見或拒絕原因
        /// </summary>
        [MaxLength(255, ErrorMessage = "審核意見長度不能超過255字元")]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 批量取得按讚狀態的請求 DTO
    /// </summary>
    public class BatchLikeStatusRequest
    {
        /// <summary>
        /// 貼文ID列表
        /// </summary>
        [Required]
        public List<int> PostIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// 按讚狀態回應 DTO
    /// </summary>
    public class LikeStatusResponse
    {
        /// <summary>
        /// 貼文ID
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// 按讚數量
        /// </summary>
        public int LikesCount { get; set; }

        /// <summary>
        /// 當前使用者是否已按讚
        /// </summary>
        public bool IsLiked { get; set; }
    }

    /// <summary>
    /// 批量按讚狀態回應 DTO
    /// </summary>
    public class BatchLikeStatusResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 訊息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 按讚狀態資料
        /// </summary>
        public List<LikeStatusResponse> Data { get; set; } = new List<LikeStatusResponse>();

    }
}