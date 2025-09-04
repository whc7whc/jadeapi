using Microsoft.AspNetCore.Http;
using Team.Backend.Models.EfModel;

public class MemberFullViewModel
{
    public required Member Member { get; set; }
    public required MemberProfile Profile { get; set; }

    public string? ProfileImg { get; set; } 
    public List<MemberAddress> Addresses { get; set; } = new List<MemberAddress>();
    public List<Session> Sessions { get; set; } = new List<Session>();
}
