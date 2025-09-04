using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;

namespace Team.Backend.Repositories.Impl;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    public IQueryable<Order> Query() =>
        _db.Orders.AsNoTracking()
            .Include(o => o.Member).ThenInclude(m => m.Profile) // 會員姓名
            .Include(o => o.Shipments);                         // 出貨時間

    public Task<Order?> GetByIdWithRelationsAsync(int id) =>
        _db.Orders
            .Include(o => o.Member).ThenInclude(m => m.Profile)
            .Include(o => o.Shipments).ThenInclude(s => s.Carrier)
            .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
            .Include(o => o.OrderDetails).ThenInclude(d => d.AttributeValue).ThenInclude(av => av.AttributeValue)
            .FirstOrDefaultAsync(o => o.Id == id);

    public Task<bool> CanConnectAsync()
    {
        try { return _db.Database.CanConnectAsync(); }
        catch { return Task.FromResult(false); }
    }


    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
