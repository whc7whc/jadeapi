using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using Team.API.Services;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICartService _cartService;

        public CartsController(AppDbContext context, ICartService cartService = null)
        {
            _context = context;
            _cartService = cartService;
        }

        // === 購物車 API (前端使用) ===

        /// <summary>
        /// 取得用戶購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> GetUserCart(int userId)
        {
            try
            {
                if (_cartService != null)
                {
                    var cart = await _cartService.GetCartAsync(userId);
                    return Ok(ApiResponse<CartResponseDto>.SuccessResult(cart, "購物車取得成功"));
                }

                // 如果沒有 Service，使用原始邏輯
                var cartEntity = await GetUserCartEntity(userId);
                return Ok(ApiResponse<CartResponseDto>.SuccessResult(cartEntity, "購物車取得成功"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"取得購物車失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 加入商品到購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="dto">加入購物車資料</param>
        [HttpPost("user/{userId}/items")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> AddToCart(int userId, [FromBody] AddCartItemDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("輸入資料有誤", errors));
                }

                if (_cartService != null)
                {
                    var result = await _cartService.AddToCartAsync(userId, dto);
                    if (result.Success)
                    {
                        return Ok(ApiResponse<CartResponseDto>.SuccessResult(result.Cart, result.Message));
                    }
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult(result.Message));
                }

                // 如果沒有 Service，使用原始邏輯
                var cart = await AddToCartEntity(userId, dto);
                return Ok(ApiResponse<CartResponseDto>.SuccessResult(cart, "商品已加入購物車"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"加入購物車失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 更新購物車商品數量
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="itemId">購物車商品ID</param>
        /// <param name="dto">更新資料</param>
        [HttpPut("user/{userId}/items/{itemId}")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> UpdateCartItem(int userId, int itemId, [FromBody] UpdateCartItemDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("輸入資料有誤", errors));
                }

                if (_cartService != null)
                {
                    var result = await _cartService.UpdateCartItemAsync(userId, itemId, dto);
                    if (result.Success)
                    {
                        return Ok(ApiResponse<CartResponseDto>.SuccessResult(result.Cart, result.Message));
                    }
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult(result.Message));
                }

                // 如果沒有 Service，使用原始邏輯
                var cart = await UpdateCartItemEntity(userId, itemId, dto.Quantity);
                return Ok(ApiResponse<CartResponseDto>.SuccessResult(cart, "數量已更新"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"更新失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 移除購物車商品
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="itemId">購物車商品ID</param>
        [HttpDelete("user/{userId}/items/{itemId}")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> RemoveCartItem(int userId, int itemId)
        {
            try
            {
                if (_cartService != null)
                {
                    var result = await _cartService.RemoveCartItemAsync(userId, itemId);
                    if (result.Success)
                    {
                        return Ok(ApiResponse<CartResponseDto>.SuccessResult(result.Cart, result.Message));
                    }
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult(result.Message));
                }

                // 如果沒有 Service，使用原始邏輯
                var cart = await RemoveCartItemEntity(userId, itemId);
                return Ok(ApiResponse<CartResponseDto>.SuccessResult(cart, "商品已移除"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"移除失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 清空購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        [HttpDelete("user/{userId}")]
        public async Task<ActionResult<ApiResponse<bool>>> ClearCart(int userId)
        {
            try
            {
                if (_cartService != null)
                {
                    var result = await _cartService.ClearCartAsync(userId);
                    return Ok(ApiResponse<bool>.SuccessResult(result, "購物車已清空"));
                }

                // 如果沒有 Service，使用原始邏輯
                var success = await ClearCartEntity(userId);
                return Ok(ApiResponse<bool>.SuccessResult(success, "購物車已清空"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResult($"清空購物車失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 套用優惠券
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="dto">優惠券代碼</param>
        [HttpPost("user/{userId}/coupon")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> ApplyCoupon(int userId, [FromBody] ApplyCouponDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("輸入資料有誤", errors));
                }

                if (_cartService != null)
                {
                    var result = await _cartService.ApplyCouponAsync(userId, dto.CouponCode);
                    if (result.Success)
                    {
                        return Ok(ApiResponse<CartResponseDto>.SuccessResult(result.Cart, result.Message));
                    }
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult(result.Message));
                }

                // 如果沒有 Service，返回未實作
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("優惠券功能尚未實作"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"套用優惠券失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 驗證購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        [HttpPost("user/{userId}/validate")]
        public async Task<ActionResult<ApiResponse<CartValidationDto>>> ValidateCart(int userId)
        {
            try
            {
                if (_cartService != null)
                {
                    var result = await _cartService.ValidateCartAsync(userId);
                    return Ok(ApiResponse<CartValidationDto>.SuccessResult(result, "購物車驗證完成"));
                }

                // 簡單的驗證邏輯
                var validation = new CartValidationDto { IsValid = true };
                return Ok(ApiResponse<CartValidationDto>.SuccessResult(validation, "購物車驗證完成"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartValidationDto>.ErrorResult($"驗證失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 移除優惠券
        /// </summary>
        /// <param name="userId">用戶ID</param>
        [HttpDelete("user/{userId}/coupon")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> RemoveCoupon(int userId)
        {
            try
            {
                if (_cartService != null)
                {
                    var result = await _cartService.RemoveCouponAsync(userId);
                    if (result.Success)
                    {
                        return Ok(ApiResponse<CartResponseDto>.SuccessResult(result.Cart, result.Message));
                    }
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult(result.Message));
                }

                // 如果沒有 Service，返回未實作
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("移除優惠券功能尚未實作"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"移除優惠券失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 取得購物車摘要
        /// </summary>
        /// <param name="userId">用戶ID</param>
        [HttpGet("user/{userId}/summary")]
        public async Task<ActionResult<ApiResponse<CartSummaryDto>>> GetCartSummary(int userId)
        {
            try
            {
                if (_cartService != null)
                {
                    var cartSummary = await _cartService.GetCartSummaryAsync(userId);
                    return Ok(ApiResponse<CartSummaryDto>.SuccessResult(cartSummary, "取得購物車摘要成功"));
                }

                // 如果沒有 Service，使用簡化邏輯
                var cart = await GetUserCartEntity(userId);
                var cartSummaryDto = new CartSummaryDto
                {
                    CartId = cart.CartId,
                    ItemCount = cart.ItemCount,
                    Subtotal = cart.Subtotal,
                    Shipping = cart.Shipping,
                    Discount = cart.Discount,
                    Total = cart.Total,
                    HasInvalidItems = false
                };
                return Ok(ApiResponse<CartSummaryDto>.SuccessResult(cartSummaryDto, "取得購物車摘要成功"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartSummaryDto>.ErrorResult($"取得購物車摘要失敗: {ex.Message}"));
            }
        }

        /// <summary>
        /// 批量移除購物車商品
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="itemIds">要移除的商品ID清單</param>
        [HttpDelete("user/{userId}/items/batch")]
        public async Task<ActionResult<ApiResponse<CartResponseDto>>> RemoveMultipleItems(int userId, [FromBody] List<int> itemIds)
        {
            try
            {
                if (!ModelState.IsValid || itemIds == null || !itemIds.Any())
                {
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("請提供要移除的商品ID清單"));
                }

                if (_cartService != null)
                {
                    var result = await _cartService.RemoveMultipleItemsAsync(userId, itemIds);
                    if (result.Success)
                    {
                        return Ok(ApiResponse<CartResponseDto>.SuccessResult(result.Cart, result.Message));
                    }
                    return BadRequest(ApiResponse<CartResponseDto>.ErrorResult(result.Message));
                }

                // 如果沒有 Service，返回未實作
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult("批量移除功能尚未實作"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<CartResponseDto>.ErrorResult($"批量移除失敗: {ex.Message}"));
            }
        }

        // === 私有方法 (暫時的實作，之後會由 Service 取代) ===

        private async Task<CartResponseDto> GetUserCartEntity(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.AttributeValue)
                        .ThenInclude(pav => pav.AttributeValue)
                            .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(c => c.MemberId == userId);

            if (cart == null)
            {
                return new CartResponseDto
                {
                    CartId = 0,
                    MemberId = userId,
                    Items = new List<CartItemDto>(),
                    ItemCount = 0,
                    Subtotal = 0,
                    Shipping = 60,
                    Total = 60
                };
            }

            var items = cart.CartItems.Select(item => new CartItemDto
            {
                ItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "未知商品",
                ProductImage = item.Product?.ProductImages?.OrderBy(pi => pi.SortOrder).FirstOrDefault()?.ImagesUrl ?? "/images/default-product.png",
                AttributeValueId = item.AttributeValueId,
                AttributeName = item.AttributeValue?.AttributeValue?.Attribute?.Name ?? "",
                AttributeValue = item.AttributeValue?.AttributeValue?.Value ?? "",
                Price = item.PriceAtAdded,
                Quantity = item.Quantity,
                Subtotal = item.PriceAtAdded * item.Quantity,
                CreatedAt = item.CreatedAt
            }).ToList();

            var subtotal = items.Sum(i => i.Subtotal);
            var shipping = subtotal >= 1000 ? 0 : 60;

            return new CartResponseDto
            {
                CartId = cart.Id,
                MemberId = cart.MemberId,
                Items = items,
                ItemCount = items.Sum(i => i.Quantity),
                Subtotal = subtotal,
                Shipping = shipping,
                Total = subtotal + shipping,
                CreatedAt = cart.CreatedAt
            };
        }

        private async Task<CartResponseDto> AddToCartEntity(int userId, AddCartItemDto dto)
        {
            // 檢查商品
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);
            
            if (product == null)
                throw new ArgumentException("商品不存在或已下架");

            // 取得或建立購物車
            var cart = await _context.Carts
                .FirstOrDefaultAsync(c => c.MemberId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    MemberId = userId,
                    CreatedAt = DateTime.Now
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // 檢查是否已存在
            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && 
                                         ci.ProductId == dto.ProductId &&
                                         ci.AttributeValueId == dto.AttributeValueId);

            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = dto.ProductId,
                    AttributeValueId = dto.AttributeValueId,
                    Quantity = dto.Quantity,
                    PriceAtAdded = product.Price,
                    CreatedAt = DateTime.Now
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            return await GetUserCartEntity(userId);
        }

        private async Task<CartResponseDto> UpdateCartItemEntity(int userId, int itemId, int quantity)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.MemberId == userId);

            if (cartItem == null)
                throw new ArgumentException("購物車商品不存在");

            if (quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = quantity;
            }

            await _context.SaveChangesAsync();
            return await GetUserCartEntity(userId);
        }

        private async Task<CartResponseDto> RemoveCartItemEntity(int userId, int itemId)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.MemberId == userId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            return await GetUserCartEntity(userId);
        }

        private async Task<bool> ClearCartEntity(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.MemberId == userId);

            if (cart != null && cart.CartItems.Any())
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
            }

            return true;
        }
    }
}
