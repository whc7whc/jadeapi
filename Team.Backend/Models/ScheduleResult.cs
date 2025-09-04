namespace Team.Backend.Models
{
    public class ScheduleResult
    {
        public bool Success { get; set; }
        public string ScheduleId { get; set; }
        public string ErrorMessage { get; set; }

        public static ScheduleResult SuccessResult(string scheduleId)
        {
            return new ScheduleResult
            {
                Success = true,
                ScheduleId = scheduleId,
                ErrorMessage = null
            };
        }

        public static ScheduleResult ErrorResult(string errorMessage)
        {
            return new ScheduleResult
            {
                Success = false,
                ScheduleId = null,
                ErrorMessage = errorMessage
            };
        }
    }
}