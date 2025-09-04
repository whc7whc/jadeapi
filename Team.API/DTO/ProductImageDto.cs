namespace Team.API.DTO
{
    public class ProductImageDto
    {
        public int Id { get; set; }
        public string ImagesUrl { get; set; }
        public int? SkuId { get; set; }
        public int SortOrder { get; set; }
        public int ProductId { get; set; }
    }
}
