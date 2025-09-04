using Team.Backend.Models.ViewModels.Orders;

namespace Team.Backend.Services;

public interface IOrderService
{
    Task<OrderIndexVm> SearchAsync(OrderQueryVm q);
    Task<OrderDetailVm?> GetDetailAsync(int id);

    // 新增：由 Service 產 CSV，回傳位元組和檔名/型別
    Task<(byte[] Content, string FileName, string ContentType)> ExportCsvAsync(OrderQueryVm q);
}
