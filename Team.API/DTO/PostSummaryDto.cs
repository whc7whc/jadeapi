namespace Team.API.DTO
{
    public class PostSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string Status { get; set; } = "draft";
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? MemberName { get; set; }
        public int LikesCount { get; set; }
        public int ViewsCount { get; set; }
    }
}