namespace Team.Backend.Models.ViewModels
{
    public class MemberSearchViewModel
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public bool? IsActive { get; set; }
        public int? Level { get; set; }

        // 顯示用結果列表
        public List<MemberFullViewModel> Results { get; set; } = new();
    }
}
