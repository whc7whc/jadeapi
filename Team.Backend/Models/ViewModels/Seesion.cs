
using Microsoft.AspNetCore.Http;
using Team.Backend.Models.EfModel;
using System.ComponentModel.DataAnnotations;
using static System.Runtime.InteropServices.JavaScript.JSType;
public class SessionViewModel
{
    public DateTime LoginTime { get; set; }
    public required string IpAddress { get; set; }
    public required string DeviceInfo { get; set; }
    public bool IsSuccess { get; set; } // 判斷是否登入成功
}













