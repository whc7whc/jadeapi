namespace Team.Backend.Models.ViewModels
{
    public class CategoryHierarchyViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public string SubCategoryDescription { get; set; }
        public int Level { get; set; }
        public bool IsParent { get; set; }
        public string DisplayText => IsParent ? CategoryName : SubCategoryName;
        public string CategoryType => IsParent ? "父分類" : "子分類";
        public string BadgeClass => IsParent ? "badge-primary" : "badge-info";
        public string IconClass => IsParent ? "fas fa-folder text-primary" : "fas fa-file-alt text-info";
    }
}