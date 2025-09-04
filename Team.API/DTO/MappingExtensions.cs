using Team.API.Models.EfModel;
using Microsoft.EntityFrameworkCore;

namespace Team.API.DTO
{
    public static class MappingExtensions
    {
        // 這個擴充方法可以讓我們用 .ToDto() 的方式輕鬆轉換物件
        public static ProductImageDto ToDto(this ProductImage productImage)
        {
            return new ProductImageDto
            {
                Id = productImage.Id,
                ProductId = productImage.ProductId,
                SkuId = productImage.SkuId,
                ImagesUrl = productImage.ImagesUrl,
                SortOrder = productImage.SortOrder
            };
        }

        public static ProductAttributeValueDto ToDto(this ProductAttributeValue pav)
        {
            return new ProductAttributeValueDto
            {
                Id = pav.Id,
                ProductId = pav.ProductId,
                AttributeValueId = pav.AttributeValueId,
                Stock = pav.Stock,
                Sku = pav.Sku,
                SkuGroupId = pav.SkuGroupId,
                AdditionalPrice = pav.AdditionalPrice
            };
        }

        public static ProductDto ToDto(this Product product)
        {
            var dto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                SubCategoryId = product.SubCategoryId,
                SellersId = product.SellersId,
                Price = product.Price,
                IsDiscount = product.IsDiscount,
                DiscountPrice = product.DiscountPrice,
                IsActive = product.IsActive,
                CategoryId = product.SubCategory?.CategoryId,
                ProductImages = product.ProductImages?.OrderBy(i => i.SortOrder).Select(i => i.ToDto()).ToList() ?? new List<ProductImageDto>(),
                ProductAttributeValues = product.ProductAttributeValues?.Select(v => v.ToDto()).ToList() ?? new List<ProductAttributeValueDto>(),
                TotalStock = product.ProductAttributeValues?.Sum(v => v.Stock) ?? 0
            };
            return dto;
        }
    }
}
