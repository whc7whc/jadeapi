using System.ComponentModel.DataAnnotations;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Models.ViewModels
{
    public class OfficialPostImageViewModel
    {
        public int Id { get; set; }

        public int PostId { get; set; }

        public string? ImagePath { get; set; }

        public int SortOrder { get; set; }
    }
}
