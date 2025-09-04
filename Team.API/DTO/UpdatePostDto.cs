using System.ComponentModel.DataAnnotations;

namespace Team.API.DTO
{
    public class UpdatePostDto
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Status { get; set; }
        public int? MembersId { get; set; }
    }

    
}

