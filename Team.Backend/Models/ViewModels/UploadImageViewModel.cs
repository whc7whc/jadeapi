using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

namespace Team.Backend.Models.ViewModels
{
    public class UploadImageViewModel
    {
        [Display(Name = "選擇圖片")]
        public IFormFile ImageFile { get; set; }
        public string ImageUrl { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
        public int ProductId { get; set; }
        public int SortOrder { get; set; }
        public int? SkuId { get; set; }
    }
}
