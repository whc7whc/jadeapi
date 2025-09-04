namespace Team.API.DTO
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? SubCategoryId { get; set; }
        public int? SellersId { get; set; }
        public int Price { get; set; }
        public bool? IsDiscount { get; set; }
        public int? DiscountPrice { get; set; }
        public bool IsActive { get; set; }

        public int? CategoryId { get; set; }
        public int TotalStock { get; set; }

        public List<ProductImageDto> ProductImages { get; set; } = new();
        public List<ProductAttributeValueDto> ProductAttributeValues { get; set; } = new();
    }
}

