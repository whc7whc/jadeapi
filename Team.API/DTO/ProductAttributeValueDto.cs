namespace Team.API.DTO
{
    public class ProductAttributeValueDto
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public int? AttributeValueId { get; set; }
        public int Stock { get; set; }
        public string? Sku { get; set; }
        public int? SkuGroupId { get; set; }
        public decimal? AdditionalPrice { get; set; }
    }
}
