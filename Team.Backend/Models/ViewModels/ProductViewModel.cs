namespace Team.Backend.Models.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Sku { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public int Stock { get; set; }
        public int SafetyStock { get; set; } // 假設安全庫存量為固定值或從其他表計算
        public string StockStatus { get; set; }
        public string SellerName { get; set; }
        public string ImageUrl { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; } // 商品狀態：上架/下架
    }
}
