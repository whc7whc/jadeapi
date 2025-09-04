namespace Team.API.DTO
{
    public class PostSearchDto
    {
        public string? Keyword { get; set; }
        public string? Status { get; set; }
        public int? MemberId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? SortBy { get; set; } = "createdAt";
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}