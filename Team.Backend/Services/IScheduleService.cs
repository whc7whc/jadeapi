using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Team.Backend.Models;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Services
{
    public interface IScheduleService
    {
        Task<ScheduleResult> ScheduleTaskAsync(string contentType, int contentId, DateTime scheduledTime, int userId, string actionType = "publish");
        Task<bool> CancelScheduleAsync(int scheduleId);
        Task<bool> IsAvailable();
        Task<List<ContentPublishingSchedule>> GetScheduledTasksAsync(string contentType = null);
        string GetSystemType();
    }
}
