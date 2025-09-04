using System.ComponentModel.DataAnnotations;

namespace Team.API.Models.DTOs
{
    /// <summary>
    /// 購物車回應 DTO
    /// </summary>
    public class CartResponseDto
    {
        public int CartId { get; set; }
        public int MemberId { get; set; }
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
        public int ItemCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? CouponCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// 購物車商品項目 DTO
    /// </summary>
    public class CartItemDto
    {
        public int ItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
        public int AttributeValueId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string AttributeValue { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public bool IsActive { get; set; } = true;
        public int? Stock { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // 賣家資訊 - 最小改動，只加這兩個欄位
        public int? SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 加入購物車請求 DTO
    /// </summary>
    public class AddCartItemDto
    {
        [Required(ErrorMessage = "商品ID不能為空")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "屬性值ID不能為空")]
        public int AttributeValueId { get; set; }

        [Required(ErrorMessage = "數量不能為空")]
        [Range(1, 99, ErrorMessage = "數量必須在 1 到 99 之間")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 更新購物車商品數量請求 DTO
    /// </summary>
    public class UpdateCartItemDto
    {
        [Required(ErrorMessage = "數量不能為空")]
        [Range(1, 99, ErrorMessage = "數量必須在 1 到 99 之間")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 套用優惠券請求 DTO
    /// </summary>
    public class ApplyCouponDto
    {
        [Required(ErrorMessage = "優惠券代碼不能為空")]
        [StringLength(50, ErrorMessage = "優惠券代碼長度不能超過 50 字元")]
        public string CouponCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// 購物車驗證結果 DTO
    /// </summary>
    public class CartValidationDto
    {
        public bool IsValid { get; set; }
        public List<CartValidationError> Errors { get; set; } = new List<CartValidationError>();
        public decimal UpdatedTotal { get; set; }
    }

    /// <summary>
    /// 購物車驗證錯誤 DTO
    /// </summary>
    public class CartValidationError
    {
        public int ItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty; // OutOfStock, PriceChanged, ProductInactive
        public string Message { get; set; } = string.Empty;
        public decimal? OldPrice { get; set; }
        public decimal? NewPrice { get; set; }
        public int? AvailableStock { get; set; }
    }

    /// <summary>
    /// 購物車摘要 DTO (用於結帳等簡化場景)
    /// </summary>
    public class CartSummaryDto
    {
        public int CartId { get; set; }
        public int ItemCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public bool HasInvalidItems { get; set; }
    }

    /// <summary>
    /// 批量操作購物車請求 DTO
    /// </summary>
    public class BatchCartOperationDto
    {
        public List<int> ItemIds { get; set; } = new List<int>();
        public string Operation { get; set; } = string.Empty; // remove, update
        public int? NewQuantity { get; set; } // 用於批量更新數量
    }

    /// <summary>
    /// 購物車商品快速加入 DTO (一鍵加入多個商品)
    /// </summary>
    public class QuickAddCartDto
    {
        public List<AddCartItemDto> Items { get; set; } = new List<AddCartItemDto>();
    }
}