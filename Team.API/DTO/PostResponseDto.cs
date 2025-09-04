using System.Text.Json.Serialization;

namespace Team.API.DTO
{
    public class PostResponseDto
    {
        // 🔥 使用 JsonPropertyName 確保前端收到正確格式
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("membersId")]
        public int MembersId { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "draft";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonPropertyName("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("reviewedBy")]
        public int? ReviewedBy { get; set; }

        [JsonPropertyName("reviewedAt")]
        public DateTime? ReviewedAt { get; set; }

        [JsonPropertyName("rejectedReason")]
        public string? RejectedReason { get; set; }

        // 會員資訊
        [JsonPropertyName("memberName")]
        public string? MemberName { get; set; }

        [JsonPropertyName("memberAvatar")]
        public string? MemberAvatar { get; set; }

        // 互動數據
        [JsonPropertyName("likesCount")]
        public int LikesCount { get; set; }

        [JsonPropertyName("viewsCount")]
        public int ViewsCount { get; set; }

        [JsonPropertyName("isLiked")]
        public bool IsLiked { get; set; }
    }
}