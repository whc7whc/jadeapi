using Hangfire.Dashboard;

namespace Team.Backend.Services
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // ✅ 在開發環境允許所有存取
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                return true;
            }

            // ✅ 生產環境可以加入更嚴格的授權邏輯
            // 例如：檢查使用者是否為管理員
            var httpContext = context.GetHttpContext();

            // 範例：檢查是否已登入且為管理員
            return httpContext.User.Identity.IsAuthenticated &&
                   httpContext.User.IsInRole("Admin");
        }
    }
}