namespace Team.Backend.Models.ViewModels
{
    public class BannerViewModel
    {
        public string Page { get; set; }

        public string Position { get; set; }

        public string ImageUrl { get; set; }

        public string LinkUrl { get; set; }

        public int? ProductId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public int? ClickCount { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
