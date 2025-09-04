using Team.Backend.Models.EfModel; // 你的 EF 命名空間

namespace Team.Backend.Repositories;

public interface IOrderRepository
{
    IQueryable<Order> Query();
    Task<Order?> GetByIdWithRelationsAsync(int id);
    Task SaveChangesAsync();
    Task<bool> CanConnectAsync(); // ← 新增
}
