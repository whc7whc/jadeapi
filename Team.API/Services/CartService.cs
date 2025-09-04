using Microsoft.EntityFrameworkCore;
using Team.API.Models.EfModel;
using Team.API.Models.DTOs;
using System.Collections.Concurrent;

namespace Team.API.Services
{
    /// <summary>
    /// 購物車服務實作
    /// </summary>
    public class CartService : ICartService
    {
        private readonly AppDbContext _context;

        // 內存存儲已套用的優惠券（因為 Cart 表沒有 CouponId 欄位）
        private static readonly ConcurrentDictionary<int, AppliedCouponInfo> _appliedCoupons = new();

        public CartService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CartResponseDto> GetCartAsync(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.Sellers) // 加載賣家資訊
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
                    Shipping = await CalculateShippingAsync(0, userId),
                    Total = await CalculateShippingAsync(0, userId)
                };
            }

            return MapToCartResponseDto(cart, userId);
        }

        public async Task<CartSummaryDto> GetCartSummaryAsync(int userId)
        {
            var cart = await GetCartAsync(userId);
            return new CartSummaryDto
            {
                CartId = cart.CartId,
                ItemCount = cart.ItemCount,
                Subtotal = cart.Subtotal,
                Shipping = cart.Shipping,
                Discount = cart.Discount,
                Total = cart.Total,
                HasInvalidItems = false // TODO: 實作驗證邏輯
            };
        }

        public async Task<CartOperationResult> AddToCartAsync(int userId, AddCartItemDto dto)
        {
            try
            {
                // 驗證商品
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);

                if (product == null)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "商品不存在或已下架"
                    };
                }

                // 檢查庫存
                var stockCheck = await CheckStockAsync(dto.ProductId, dto.AttributeValueId, dto.Quantity);
                if (!stockCheck.IsAvailable)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = $"庫存不足，目前可用數量：{stockCheck.AvailableStock}"
                    };
                }

                // 取得或建立購物車
                var cart = await GetOrCreateCartAsync(userId);

                // 檢查是否已存在相同商品
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.Id &&
                                             ci.ProductId == dto.ProductId &&
                                             ci.AttributeValueId == dto.AttributeValueId);

                if (existingItem != null)
                {
                    // 🔥 重要：檢查購物車現有數量 + 新增數量是否超過庫存
                    var totalQuantity = existingItem.Quantity + dto.Quantity;
                    var totalStockCheck = await CheckStockAsync(dto.ProductId, dto.AttributeValueId, totalQuantity);
                    
                    if (!totalStockCheck.IsAvailable)
                    {
                        var availableToAdd = totalStockCheck.AvailableStock - existingItem.Quantity;
                        return new CartOperationResult
                        {
                            Success = false,
                            Message = availableToAdd > 0 
                                ? $"庫存不足，購物車已有 {existingItem.Quantity} 個，最多還能加入 {availableToAdd} 個"
                                : $"庫存不足，購物車已有 {existingItem.Quantity} 個，目前庫存僅 {totalStockCheck.AvailableStock} 個"
                        };
                    }
                    
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
                        PriceAtAdded = product.IsDiscount == true && product.DiscountPrice.HasValue
                            ? product.DiscountPrice.Value
                            : product.Price,
                        CreatedAt = DateTime.Now
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                var updatedCart = await GetCartAsync(userId);
                return new CartOperationResult
                {
                    Success = true,
                    Message = "商品已成功加入購物車",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                return new CartOperationResult
                {
                    Success = false,
                    Message = $"加入購物車失敗：{ex.Message}"
                };
            }
        }

        public async Task<CartOperationResult> QuickAddToCartAsync(int userId, QuickAddCartDto dto)
        {
            var results = new List<string>();
            var hasErrors = false;

            foreach (var item in dto.Items)
            {
                var result = await AddToCartAsync(userId, item);
                if (!result.Success)
                {
                    hasErrors = true;
                    results.Add($"商品 {item.ProductId}: {result.Message}");
                }
            }

            var cart = await GetCartAsync(userId);
            return new CartOperationResult
            {
                Success = !hasErrors,
                Message = hasErrors ? "部分商品加入失敗" : "所有商品已成功加入購物車",
                Cart = cart,
                Warnings = results
            };
        }

        public async Task<CartOperationResult> UpdateCartItemAsync(int userId, int itemId, UpdateCartItemDto dto)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.MemberId == userId);

                if (cartItem == null)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "購物車商品不存在"
                    };
                }

                // 檢查庫存
                var stockCheck = await CheckStockAsync(cartItem.ProductId, cartItem.AttributeValueId, dto.Quantity);
                if (!stockCheck.IsAvailable)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = $"庫存不足，目前可用數量：{stockCheck.AvailableStock}"
                    };
                }

                cartItem.Quantity = dto.Quantity;
                await _context.SaveChangesAsync();

                var cart = await GetCartAsync(userId);
                return new CartOperationResult
                {
                    Success = true,
                    Message = "數量已更新",
                    Cart = cart
                };
            }
            catch (Exception ex)
            {
                return new CartOperationResult
                {
                    Success = false,
                    Message = $"更新失敗：{ex.Message}"
                };
            }
        }

        public async Task<CartOperationResult> RemoveCartItemAsync(int userId, int itemId)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.MemberId == userId);

                if (cartItem == null)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "購物車商品不存在"
                    };
                }

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                var cart = await GetCartAsync(userId);
                return new CartOperationResult
                {
                    Success = true,
                    Message = "商品已移除",
                    Cart = cart
                };
            }
            catch (Exception ex)
            {
                return new CartOperationResult
                {
                    Success = false,
                    Message = $"移除失敗：{ex.Message}"
                };
            }
        }

        public async Task<CartOperationResult> RemoveMultipleItemsAsync(int userId, List<int> itemIds)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                var itemsToRemove = await _context.CartItems
                    .Where(ci => ci.CartId == cart.Id && itemIds.Contains(ci.Id))
                    .ToListAsync();

                _context.CartItems.RemoveRange(itemsToRemove);
                await _context.SaveChangesAsync();

                var updatedCart = await GetCartAsync(userId);
                return new CartOperationResult
                {
                    Success = true,
                    Message = $"已移除 {itemsToRemove.Count} 個商品",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                return new CartOperationResult
                {
                    Success = false,
                    Message = $"批量移除失敗：{ex.Message}"
                };
            }
        }

        public async Task<bool> ClearCartAsync(int userId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.MemberId == userId);

                if (cart != null && cart.CartItems.Any())
                {
                    _context.CartItems.RemoveRange(cart.CartItems);
                    await _context.SaveChangesAsync();
                }

                // 清除套用的優惠券
                _appliedCoupons.TryRemove(userId, out _);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<CartOperationResult> ApplyCouponAsync(int userId, string couponCode)
        {
            try
            {
                MemberCoupon memberCoupon = null;
                
                // 方式1: 如果是純數字，優先當作 Coupon ID 處理（點擊領取場景）
                if (int.TryParse(couponCode, out int couponId))
                {
                    memberCoupon = await _context.MemberCoupons
                        .Include(mc => mc.Coupon)
                        .FirstOrDefaultAsync(mc => mc.MemberId == userId && 
                                                 mc.CouponId == couponId);
                }
                
                // 方式2: 如果方式1找不到，再用 Verification Code 查詢（手動輸入場景）
                if (memberCoupon == null)
                {
                    memberCoupon = await _context.MemberCoupons
                        .Include(mc => mc.Coupon)
                        .FirstOrDefaultAsync(mc => mc.MemberId == userId && 
                                                 mc.VerificationCode == couponCode);
                }

                if (memberCoupon == null)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "找不到此優惠券或您沒有使用權限"
                    };
                }

                // 驗證優惠券狀態
                if (memberCoupon.Status == "used" || memberCoupon.UsedAt.HasValue)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "此優惠券已使用過"
                    };
                }

                var coupon = memberCoupon.Coupon;
                var now = DateTime.Now;
                
                if (!coupon.IsActive)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "優惠券未啟用"
                    };
                }

                if (coupon.StartAt > now)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "優惠券尚未開始"
                    };
                }

                if (coupon.ExpiredAt < now)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "優惠券已過期"
                    };
                }

                if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = "優惠券使用次數已達上限"
                    };
                }

                // 檢查用戶會員等級限制
                if (coupon.ApplicableLevelId.HasValue)
                {
                    var member = await _context.Members
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == userId);
                    
                    if (member?.Level != coupon.ApplicableLevelId.Value)
                    {
                        return new CartOperationResult
                        {
                            Success = false,
                            Message = "您的會員等級無法使用此優惠券"
                        };
                    }
                }

                // 檢查購物車金額是否符合最低消費
                var cart = await GetCartAsync(userId);
                
                if (coupon.MinSpend > 0 && cart.Subtotal < coupon.MinSpend)
                {
                    return new CartOperationResult
                    {
                        Success = false,
                        Message = $"最低消費金額需達 NT$ {coupon.MinSpend:N0}"
                    };
                }

                // 計算折扣金額
                decimal discountAmount = CalculateDiscount(coupon, cart.Subtotal);

                // 將優惠券資訊儲存到內存
                _appliedCoupons[userId] = new AppliedCouponInfo
                {
                    CouponId = coupon.Id,
                    CouponTitle = coupon.Title,
                    DiscountAmount = discountAmount,
                    AppliedAt = DateTime.Now
                };

                // 重新取得更新後的購物車
                var updatedCart = await GetCartAsync(userId);
                
                return new CartOperationResult
                {
                    Success = true,
                    Message = $"優惠券「{coupon.Title}」套用成功，折抵 NT$ {discountAmount:N0}",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                return new CartOperationResult
                {
                    Success = false,
                    Message = $"套用優惠券失敗：{ex.Message}"
                };
            }
        }

        public async Task<CartOperationResult> RemoveCouponAsync(int userId)
        {
            try
            {
                // 從內存中移除套用的優惠券
                _appliedCoupons.TryRemove(userId, out _);

                // 重新取得更新後的購物車
                var updatedCart = await GetCartAsync(userId);
                
                return new CartOperationResult
                {
                    Success = true,
                    Message = "優惠券已移除",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                return new CartOperationResult
                {
                    Success = false,
                    Message = $"移除優惠券失敗：{ex.Message}"
                };
            }
        }

        public async Task<CartValidationDto> ValidateCartAsync(int userId)
        {
            var validation = new CartValidationDto { IsValid = true };

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.MemberId == userId);

            if (cart == null)
            {
                return validation;
            }

            foreach (var item in cart.CartItems)
            {
                // 檢查商品是否仍然有效
                if (!item.Product.IsActive)
                {
                    validation.IsValid = false;
                    validation.Errors.Add(new CartValidationError
                    {
                        ItemId = item.Id,
                        ProductName = item.Product.Name,
                        ErrorType = "ProductInactive",
                        Message = "商品已下架"
                    });
                }

                    // 檢查庫存
                    var stockCheck = await CheckStockAsync(item.ProductId, item.AttributeValueId, item.Quantity);
                    if (!stockCheck.IsAvailable)
                    {
                        validation.IsValid = false;
                        validation.Errors.Add(new CartValidationError
                        {
                            ItemId = item.Id,
                            ProductName = item.Product.Name,
                            ErrorType = "OutOfStock",
                            Message = $"庫存不足，可用數量：{stockCheck.AvailableStock}",
                            AvailableStock = stockCheck.AvailableStock
                        });
                    }

                // 檢查價格變動
                var currentPrice = item.Product.IsDiscount == true && item.Product.DiscountPrice.HasValue
                    ? item.Product.DiscountPrice.Value
                    : item.Product.Price;

                if (currentPrice != item.PriceAtAdded)
                {
                    validation.Errors.Add(new CartValidationError
                    {
                        ItemId = item.Id,
                        ProductName = item.Product.Name,
                        ErrorType = "PriceChanged",
                        Message = "商品價格已變動",
                        OldPrice = item.PriceAtAdded,
                        NewPrice = currentPrice
                    });
                }
            }

            var cartDto = await GetCartAsync(userId);
            validation.UpdatedTotal = cartDto.Total;

            return validation;
        }

        public async Task<StockCheckDto> CheckStockAsync(int productId, int attributeValueId, int quantity)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            var productAttributeValue = await _context.ProductAttributeValues
                .Include(pav => pav.AttributeValue)
                    .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(pav => pav.Id == attributeValueId);

            return new StockCheckDto
            {
                ProductId = productId,
                AttributeValueId = attributeValueId,
                RequestedQuantity = quantity,
                AvailableStock = productAttributeValue?.Stock ?? 0,
                IsAvailable = (productAttributeValue?.Stock ?? 0) >= quantity,
                ProductName = product?.Name ?? "未知商品",
                AttributeInfo = productAttributeValue?.AttributeValue != null
                    ? $"{productAttributeValue.AttributeValue.Attribute?.Name}: {productAttributeValue.AttributeValue.Value}"
                    : "無屬性資訊"
            };
        }

        public async Task<bool> CanCheckoutAsync(int userId)
        {
            var validation = await ValidateCartAsync(userId);
            var cart = await GetCartAsync(userId);

            return validation.IsValid && cart.Items.Any();
        }

        public async Task<CartOperationResult> MergeCartsAsync(int guestCartId, int memberUserId)
        {
            // TODO: 實作購物車合併邏輯
            throw new NotImplementedException("購物車合併功能尚未實作");
        }

        public async Task<CartStatisticsDto> GetCartStatisticsAsync()
        {
            // TODO: 實作統計功能
            throw new NotImplementedException("統計功能尚未實作");
        }

        public async Task<int> CleanupExpiredCartsAsync(int daysOld = 30)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            var expiredCarts = await _context.Carts
                .Where(c => c.CreatedAt < cutoffDate)
                .ToListAsync();

            _context.Carts.RemoveRange(expiredCarts);
            await _context.SaveChangesAsync();

            return expiredCarts.Count;
        }

        public async Task<decimal> CalculateShippingAsync(decimal subtotal, int? userId = null)
        {
            // 簡單的運費計算邏輯
            if (subtotal >= 1000)
                return 0; // 滿千免運

            return 60; // 基本運費
        }

        public async Task<List<object>> GetRecommendedProductsAsync(int userId, int limit = 5)
        {
            // TODO: 實作推薦商品邏輯
            return new List<object>();
        }

        // === 私有方法 ===

        private async Task<Cart> GetOrCreateCartAsync(int userId)
        {
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

            return cart;
        }

        private CartResponseDto MapToCartResponseDto(Cart cart, int userId)
        {
            var items = cart.CartItems.Select(item => new CartItemDto
            {
                ItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "未知商品",
                ProductImage = item.Product?.ProductImages?.FirstOrDefault()?.ImagesUrl ?? "/images/placeholder.jpg",
                ProductSku = item.AttributeValue?.Sku ?? "",
                AttributeValueId = item.AttributeValueId,
                AttributeName = item.AttributeValue?.AttributeValue?.Attribute?.Name ?? "",
                AttributeValue = item.AttributeValue?.AttributeValue?.Value ?? "",
                Price = item.PriceAtAdded,
                DiscountPrice = item.Product?.DiscountPrice,
                Quantity = item.Quantity,
                Subtotal = item.PriceAtAdded * item.Quantity,
                IsActive = item.Product?.IsActive ?? false,
                Stock = item.AttributeValue?.Stock,
                CreatedAt = item.CreatedAt,
                
                // 賣家資訊 - 最小改動，只加這兩行
                SellerId = item.Product?.SellersId,
                SellerName = item.Product?.Sellers?.RealName ?? "未知賣家"
            }).ToList();

            var subtotal = items.Sum(i => i.Subtotal);
            var shipping = CalculateShippingAsync(subtotal, userId).Result;
            
            // 處理優惠券折扣
            var discount = 0m;
            string couponCode = null;
            
            if (_appliedCoupons.TryGetValue(userId, out var appliedCoupon))
            {
                discount = appliedCoupon.DiscountAmount;
                couponCode = appliedCoupon.CouponTitle;
            }
            
            var total = subtotal + shipping - discount;

            return new CartResponseDto
            {
                CartId = cart.Id,
                MemberId = cart.MemberId,
                Items = items,
                ItemCount = items.Sum(i => i.Quantity),
                Subtotal = subtotal,
                Shipping = shipping,
                Discount = discount,
                Total = total,
                CouponCode = couponCode,
                CreatedAt = cart.CreatedAt
            };
        }

        /// <summary>
        /// 計算優惠券折扣金額
        /// </summary>
        private decimal CalculateDiscount(Coupon coupon, decimal subtotal)
        {
            return coupon.DiscountType switch
            {
                "%數折扣" => Math.Round(subtotal * (coupon.DiscountAmount / 100m), 0),
                "滿減" => coupon.DiscountAmount,
                "J幣回饋" => 0, // J幣回饋不影響當前訂單金額
                _ => 0
            };
        }

        /// <summary>
        /// 套用優惠券資訊類別
        /// </summary>
        private class AppliedCouponInfo
        {
            public int CouponId { get; set; }
            public string CouponTitle { get; set; } = string.Empty;
            public decimal DiscountAmount { get; set; }
            public DateTime AppliedAt { get; set; }
        }
    }
}