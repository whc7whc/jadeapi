using Microsoft.Extensions.Configuration;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Services
{
    public static class ScheduleServiceFactory
    {
        public static IScheduleService CreateService(AppDbContext context, IServiceProvider serviceProvider)
        {
            // ✅ 從設定檔讀取是否使用 Hangfire
            var configuration = serviceProvider.GetService<IConfiguration>();
            bool useHangfire = configuration?.GetValue<bool>("ScheduleSystem:UseHangfire") ?? false;

            if (useHangfire)
            {
                try
                {
                    // 嘗試建立 Hangfire 服務
                    return new HangfireScheduleService(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Hangfire 初始化失敗，降級為基礎服務: {ex.Message}");
                    return new BasicScheduleService(context, serviceProvider);
                }
            }
            else
            {
                return new BasicScheduleService(context, serviceProvider);
            }
        }
    }
}