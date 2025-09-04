using Microsoft.EntityFrameworkCore;
using Team.API.Models.DTOs;
using Team.API.Models.EfModel;

namespace Team.API.Services
{
    /// <summary>
    /// 結帳服務實作
    /// </summary>
    public class CheckoutService : ICheckoutService
    {
        private readonly AppDbContext _context;
        private readonly ICartService _cartService;
        private readonly MemberLevelUpgradeService _upgradeService;
        private readonly ILogger<CheckoutService> _logger;

        public CheckoutService(
            AppDbContext context,
            ICartService cartService,
            MemberLevelUpgradeService upgradeService,
            ILogger<CheckoutService> logger)
        {
            _context = context;
            _cartService = cartService;
            _upgradeService = upgradeService;
            _logger = logger;
        }

        #region 結帳前驗證

        public async Task<CheckoutValidationDto> ValidateCheckoutAsync(int memberId)
        {
            var validation = new CheckoutValidationDto { IsValid = true };

            try
            {
                // 1. 檢查會員是否存在
                var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == memberId);
                if (member == null)
                {
                    validation.IsValid = false;
                    validation.Errors.Add(new CheckoutValidationError
                    {
                        Type = "MEMBER_NOT_FOUND",
                        Message = "會員不存在"
                    });
                    return validation;
                }

                // 2. 檢查購物車是否為空
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.MemberId == memberId);

                if (cart == null || !cart.CartItems.Any())
                {
                    validation.IsValid = false;
                    validation.Errors.Add(new CheckoutValidationError
                    {
                        Type = "EMPTY_CART",
                        Message = "購物車為空"
                    });
                    return validation;
                }

                // 3. 檢查商品庫存和狀態
                foreach (var item in cart.CartItems)
                {
                    _logger.LogInformation($"🛒 檢查購物車項目 - ProductId: {item.ProductId}, AttributeValueId: {item.AttributeValueId}, Quantity: {item.Quantity}");
                    
                    var product = await _context.Products
                        .Include(p => p.ProductAttributeValues)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    if (product == null || !product.IsActive)
                    {
                        _logger.LogWarning($"❌ 商品不可用 - ProductId: {item.ProductId}, 商品存在: {product != null}, 是否啟用: {product?.IsActive}");
                        validation.IsValid = false;
                        validation.Errors.Add(new CheckoutValidationError
                        {
                            Type = "PRODUCT_UNAVAILABLE",
                            Message = $"商品已下架或不存在",
                            Data = new { ProductId = item.ProductId }
                        });
                        continue;
                    }

                    var attributeValue = product.ProductAttributeValues
                        .FirstOrDefault(pav => pav.Id == item.AttributeValueId);

                    _logger.LogInformation($"🔍 庫存檢查 - ProductId: {item.ProductId}, AttributeValueId: {item.AttributeValueId}, 需求數量: {item.Quantity}");
                    _logger.LogInformation($"📦 找到的庫存記錄: {(attributeValue != null ? $"Stock={attributeValue.Stock}" : "NULL")}");

                    if (attributeValue == null || attributeValue.Stock < item.Quantity)
                    {
                        _logger.LogWarning($"❌ 庫存不足 - 需要: {item.Quantity}, 可用: {attributeValue?.Stock ?? 0}");
                        validation.IsValid = false;
                        validation.Errors.Add(new CheckoutValidationError
                        {
                            Type = "INSUFFICIENT_STOCK",
                            Message = $"商品庫存不足",
                            Data = new
                            {
                                ProductId = item.ProductId,
                                ProductName = product.Name,
                                RequestedQuantity = item.Quantity,
                                AvailableStock = attributeValue?.Stock ?? 0
                            }
                        });
                    }
                }

                // 4. 如果通過基本驗證，取得結帳摘要
                if (validation.IsValid)
                {
                    validation.Summary = await GetCheckoutSummaryAsync(memberId, null, 0, null);
                }

                return validation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "結帳驗證發生錯誤");
                validation.IsValid = false;
                validation.Errors.Add(new CheckoutValidationError
                {
                    Type = "SYSTEM_ERROR",
                    Message = "系統錯誤，請稍後再試"
                });
                return validation;
            }
        }

        public async Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(int memberId, string? couponCode = null, int usedPoints = 0, string? paymentMethod = null)
        {
            var summary = new CheckoutSummaryDto();

            try
            {
                // 取得購物車資料
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.AttributeValue)
                            .ThenInclude(av => av.AttributeValue)
                                .ThenInclude(av => av.Attribute)
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Product)
                            .ThenInclude(p => p.ProductAttributeValues)
                    .FirstOrDefaultAsync(c => c.MemberId == memberId);

                if (cart == null || !cart.CartItems.Any())
                {
                    return summary;
                }

                // 計算商品小計
                foreach (var item in cart.CartItems)
                {
                    var productAttributeValue = item.Product?.ProductAttributeValues?
                        .FirstOrDefault(pav => pav.Id == item.AttributeValueId);

                    var checkoutItem = new CheckoutItemDto
                    {
                        CartItemId = item.Id,
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? "未知商品",
                        ProductImage = item.Product?.ProductImages?.FirstOrDefault()?.ImagesUrl ?? "/images/placeholder.jpg",
                        AttributeValueId = item.AttributeValueId,
                        AttributeName = item.AttributeValue?.AttributeValue?.Attribute?.Name ?? "",
                        AttributeValue = item.AttributeValue?.AttributeValue?.Value ?? "",
                        Price = item.PriceAtAdded,
                        Quantity = item.Quantity,
                        Subtotal = item.PriceAtAdded * item.Quantity,
                        IsAvailable = item.Product?.IsActive == true,
                        AvailableStock = productAttributeValue?.Stock ?? 0
                    };

                    summary.Items.Add(checkoutItem);
                }

                summary.ItemCount = summary.Items.Sum(i => i.Quantity);
                summary.SubtotalAmount = summary.Items.Where(i => i.IsAvailable).Sum(i => i.Subtotal);

                // 計算運費
                summary.ShippingFee = await CalculateShippingFeeAsync(memberId, "standard");
                summary.FreeShipping = summary.SubtotalAmount >= 1000;
                if (summary.FreeShipping) summary.ShippingFee = 0;

                // 處理優惠券
                if (!string.IsNullOrEmpty(couponCode) && !string.IsNullOrWhiteSpace(couponCode))
                {
                    var (isValid, couponInfo, _) = await ValidateCouponAsync(memberId, couponCode);
                    if (isValid && couponInfo != null)
                    {
                        summary.AppliedCoupon = couponInfo;
                        summary.DiscountAmount = couponInfo.CalculatedDiscount;
                    }
                    else
                    {
                        // 優惠券無效時，確保折扣為0
                        summary.DiscountAmount = 0;
                        summary.AppliedCoupon = null;
                    }
                }
                else
                {
                    // 沒有提供優惠券代碼時，確保折扣為0
                    summary.DiscountAmount = 0;
                    summary.AppliedCoupon = null;
                }

                // 處理點數抵扣
                summary.AvailablePoints = await GetAvailablePointsAsync(memberId);
                summary.MaxPointsDeduction = await CalculateMaxPointsDeductionAsync(memberId, summary.SubtotalAmount);
                
                if (usedPoints > 0 && usedPoints <= summary.AvailablePoints)
                {
                    var pointsValue = Math.Min(usedPoints, (int)summary.MaxPointsDeduction);
                    summary.PointsDeductAmount = pointsValue; // 1點 = 1元
                }

                // 獲取付款方式手續費
                var processingFee = await GetPaymentProcessingFeeAsync(paymentMethod ?? "");
                
                // 計算總金額（加入手續費）
                summary.TotalAmount = summary.SubtotalAmount + summary.ShippingFee + processingFee - summary.DiscountAmount - summary.PointsDeductAmount;
                summary.TotalAmount = Math.Max(0, summary.TotalAmount); // 確保不為負數

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得結帳摘要發生錯誤");
                return summary;
            }
        }

        #endregion

        #region 配送與付款選項

        public async Task<List<DeliveryMethodDto>> GetAvailableDeliveryMethodsAsync(int memberId, int? addressId = null)
        {
            try
            {
                var deliveryMethods = new List<DeliveryMethodDto>();

                // 1. 驗證會員是否存在
                var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == memberId);
                if (member == null)
                {
                    return deliveryMethods;
                }

                // 2. 從資料庫讀取物流商資料
                var carriers = await _context.Carriers
                    .Where(c => c.Name != null)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                if (carriers.Any())
                {
                    // 3. 根據物流商建立配送選項（對應綠界物流）
                    foreach (var carrier in carriers)
                    {
                        var deliveryMethod = CreateECPayDeliveryMethodFromCarrier(carrier);
                        if (deliveryMethod != null)
                        {
                            deliveryMethods.Add(deliveryMethod);
                        }
                    }
                }

                // 4. 如果資料庫沒有物流商資料，使用預設的綠界物流選項
                if (!deliveryMethods.Any())
                {
                    deliveryMethods.AddRange(GetDefaultECPayDeliveryMethods());
                }

                // 5. 根據會員等級和地址調整選項
                await ApplyMemberAndAddressRestrictions(member, addressId, deliveryMethods);

                // 6. 根據購物車金額調整運費
                await ApplyCartAmountDiscounts(memberId, deliveryMethods);

                return deliveryMethods;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得配送方式時發生錯誤");
                // 異常時返回預設綠界配送選項
                return GetDefaultECPayDeliveryMethods();
            }
        }

        /// <summary>
        /// 根據物流商資料建立綠界物流配送選項
        /// </summary>
        private DeliveryMethodDto? CreateECPayDeliveryMethodFromCarrier(Carrier carrier)
        {
            // 對應綠界物流 API 的配送類型
            return carrier.Name switch
            {
                "黑貓宅急便" or "黑貓物流" => new DeliveryMethodDto
                {
                    Method = "HOME_TCAT", // 綠界黑貓宅急便代碼
                    Name = "黑貓宅急便",
                    Fee = 60,
                    Description = "黑貓宅急便到府配送 - 1-2個工作天",
                    IsAvailable = true,
                    EstimatedDays = 2
                },
                "7-11 超商取貨" or "7-ELEVEN" or "7-11" => new DeliveryMethodDto
                {
                    Method = "UNIMART", // 綠界7-11代碼
                    Name = "7-ELEVEN 超商取貨",
                    Fee = 60,
                    Description = "7-ELEVEN 超商取貨付款 - 2-4個工作天",
                    IsAvailable = true,
                    EstimatedDays = 3
                },
                "全家便利商店" or "全家超商" or "全家" => new DeliveryMethodDto
                {
                    Method = "FAMI", // 綠界全家代碼
                    Name = "全家便利商店取貨",
                    Fee = 60,
                    Description = "全家便利商店取貨付款 - 2-4個工作天",
                    IsAvailable = true,
                    EstimatedDays = 3
                },
                _ => null
            };
        }

        /// <summary>
        /// 取得預設的綠界物流配送選項
        /// </summary>
        private List<DeliveryMethodDto> GetDefaultECPayDeliveryMethods()
        {
            return new List<DeliveryMethodDto>
            {
                new DeliveryMethodDto
                {
                    Method = "HOME_TCAT",
                    Name = "黑貓宅急便",
                    Fee = 60,
                    Description = "黑貓宅急便到府配送 - 1-2個工作天",
                    IsAvailable = true,
                    EstimatedDays = 2
                },
                new DeliveryMethodDto
                {
                    Method = "UNIMART",
                    Name = "7-ELEVEN 超商取貨",
                    Fee = 60,
                    Description = "7-ELEVEN 超商取貨付款 - 2-4個工作天",
                    IsAvailable = true,
                    EstimatedDays = 3
                },
                new DeliveryMethodDto
                {
                    Method = "FAMI",
                    Name = "全家便利商店取貨",
                    Fee = 60,
                    Description = "全家便利商店取貨付款 - 2-4個工作天",
                    IsAvailable = true,
                    EstimatedDays = 3
                }
            };
        }

        /// <summary>
        /// 應用會員等級和地址限制
        /// </summary>
        private async Task ApplyMemberAndAddressRestrictions(Member member, int? addressId, List<DeliveryMethodDto> deliveryMethods)
        {
            // 檢查會員等級優惠 - 修正：使用 Level 屬性
            var memberLevel = await _context.MembershipLevels
                .FirstOrDefaultAsync(ml => ml.Id == member.Level);

            if (memberLevel?.LevelName == "金牌會員")
            {
                // 金牌會員享有運費優惠
                foreach (var method in deliveryMethods)
                {
                    if (method.Fee > 0)
                    {
                        method.Fee = Math.Max(0, method.Fee - 20); // 運費折20元
                        method.Description += " (金牌會員優惠)";
                    }
                }
            }

            // 檢查地址限制（離島等）
            if (addressId.HasValue)
            {
                var address = await _context.MemberAddresses
                    .FirstOrDefaultAsync(a => a.Id == addressId && a.MembersId == member.Id);

                if (address != null && IsRemoteArea(address.City))
                {
                    // 離島地區限制
                    deliveryMethods.RemoveAll(d => d.Method == "UNIMART" || d.Method == "FAMI"); // 超商取貨不送離島
                    
                    foreach (var method in deliveryMethods)
                    {
                        method.Fee += 100; // 離島加收運費
                        method.Description += " (離島地區加收100元)";
                        method.EstimatedDays += 1; // 配送時間延長
                    }
                }
            }
        }

        /// <summary>
        /// 檢查是否為偏遠地區
        /// </summary>
        private bool IsRemoteArea(string city)
        {
            var remoteAreas = new[] { "澎湖縣", "金門縣", "連江縣", "台東縣" };
            return remoteAreas.Any(area => city.Contains(area.Replace("縣", "")));
        }

        /// <summary>
        /// 應用購物車金額折扣
        /// </summary>
        private async Task ApplyCartAmountDiscounts(int memberId, List<DeliveryMethodDto> deliveryMethods)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.MemberId == memberId);

            if (cart != null)
            {
                var subtotal = cart.CartItems.Sum(item => item.PriceAtAdded * item.Quantity);

                // 滿1000免運
                if (subtotal >= 1000)
                {
                    foreach (var method in deliveryMethods)
                    {
                        if (method.Fee > 0)
                        {
                            method.Fee = 0;
                            method.Description = method.Description.Replace(" (離島地區加收100元)", "") + " (滿千免運)";
                        }
                    }
                }
            }
        }

        public async Task<List<PaymentMethodDto>> GetAvailablePaymentMethodsAsync(int memberId)
        {
            var paymentMethods = new List<PaymentMethodDto>
            {
                new PaymentMethodDto
                {
                    Method = "credit_card",
                    Name = "信用卡",
                    Description = "支援VISA、MasterCard、JCB",
                    IsAvailable = true,
                    ProcessingFee = null,
                    IconUrl = "/images/payment/credit-card.png"
                },
                new PaymentMethodDto
                {
                    Method = "atm",
                    Name = "ATM轉帳",
                    Description = "虛擬帳號轉帳",
                    IsAvailable = true,
                    ProcessingFee = null,
                    IconUrl = "/images/payment/atm.png"
                },
                new PaymentMethodDto
                {
                    Method = "linepay",
                    Name = "Line Pay",
                    Description = "使用Line Pay付款",
                    IsAvailable = true,
                    ProcessingFee = null,
                    IconUrl = "/images/payment/linepay.png"
                },
                new PaymentMethodDto
                {
                    Method = "cod",
                    Name = "貨到付款",
                    Description = "商品送達時付款",
                    IsAvailable = true,
                    ProcessingFee = 30,
                    IconUrl = "/images/payment/cod.png"
                }
            };

            return await Task.FromResult(paymentMethods);
        }

        public async Task<decimal> GetPaymentProcessingFeeAsync(string paymentMethod)
        {
            var paymentMethods = await GetAvailablePaymentMethodsAsync(0); // memberId 不影響手續費
            var method = paymentMethods.FirstOrDefault(pm => pm.Method == paymentMethod);
            return method?.ProcessingFee ?? 0;
        }

        public async Task<decimal> CalculateShippingFeeAsync(int memberId, string deliveryMethod, int? addressId = null)
        {
            try
            {
                // 綠界物流配送方式的運費計算
                var baseFee = deliveryMethod switch
                {
                    "HOME_TCAT" => 60m,    // 黑貓宅急便
                    "UNIMART" => 60m,      // 7-11超商取貨
                    "FAMI" => 60m,         // 全家便利商店
                    // 向下兼容原有的配送方式
                    "standard" => 60m,
                    "express" => 120m,
                    "pickup" => 0m,
                    _ => 60m
                };

                // 檢查會員等級優惠 - 修正：使用 Level 屬性
                var member = await _context.Members
                    .Include(m => m.LevelNavigation)
                    .FirstOrDefaultAsync(m => m.Id == memberId);

                if (member?.LevelNavigation?.LevelName == "金牌會員" && baseFee > 0)
                {
                    baseFee = Math.Max(0, baseFee - 20); // 金牌會員運費折20元
                }

                // 檢查是否為離島地區
                if (addressId.HasValue)
                {
                    var address = await _context.MemberAddresses
                        .FirstOrDefaultAsync(a => a.Id == addressId && a.MembersId == memberId);

                    if (address != null && IsRemoteArea(address.City))
                    {
                        // 超商取貨不送離島
                        if (deliveryMethod == "UNIMART" || deliveryMethod == "FAMI")
                        {
                            return 0; // 不可用，返回0但在上層會被過濾掉
                        }
                        baseFee += 100; // 離島宅配加收100元
                    }
                }

                // 檢查購物車金額免運
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.MemberId == memberId);

                if (cart != null)
                {
                    var subtotal = cart.CartItems.Sum(item => item.PriceAtAdded * item.Quantity);
                    if (subtotal >= 1000) // 滿千免運
                    {
                        return 0;
                    }
                }

                return baseFee;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "計算運費發生錯誤");
                return 60; // 預設運費
            }
        }

        #endregion

        #region 優惠券與點數

        public async Task<(bool IsValid, CouponInfoDto? CouponInfo, String Message)> ValidateCouponAsync(int memberId, string couponCode)
        {
            try
            {
                MemberCoupon memberCoupon = null;
                
                // 🔧 修正：統一優惠券查找邏輯，與購物車 API 一致
                // 方式1: 如果是純數字，優先當作 Coupon ID 處理（點擊領取場景）
                if (int.TryParse(couponCode, out int couponId))
                {
                    memberCoupon = await _context.MemberCoupons
                        .Include(mc => mc.Coupon)
                        .FirstOrDefaultAsync(mc => mc.MemberId == memberId && 
                                                 mc.CouponId == couponId &&
                                                 mc.Status == "active" &&
                                                 mc.UsedAt == null);
                }
                
                // 方式2: 如果方式1找不到，再用 VerificationCode 查詢（手動輸入場景）
                if (memberCoupon == null)
                {
                    memberCoupon = await _context.MemberCoupons
                        .Include(mc => mc.Coupon)
                        .FirstOrDefaultAsync(mc => mc.MemberId == memberId && 
                                                 mc.VerificationCode == couponCode &&
                                                 mc.Status == "active" &&
                                                 mc.UsedAt == null);
                }
                
                // 方式3: 最後嘗試用 Title.Contains 查詢（向下兼容）
                if (memberCoupon == null)
                {
                    memberCoupon = await _context.MemberCoupons
                        .Include(mc => mc.Coupon)
                        .FirstOrDefaultAsync(mc => mc.MemberId == memberId &&
                                                 mc.Coupon.Title.Contains(couponCode) &&
                                                 mc.Status == "active" &&
                                                 mc.UsedAt == null);
                }

                if (memberCoupon == null)
                {
                    return (false, null, "優惠券不存在或已使用");
                }

                var coupon = memberCoupon.Coupon;

                // 檢查優惠券是否過期
                if (DateTime.Now < coupon.StartAt || DateTime.Now > coupon.ExpiredAt)
                {
                    return (false, null, "優惠券已過期或尚未生效");
                }

                // 檢查優惠券是否啟用
                if (!coupon.IsActive)
                {
                    return (false, null, "優惠券已停用");
                }

                // 取得購物車小計以計算折扣
                var cartSummary = await GetCheckoutSummaryAsync(memberId, null, 0, null);
                var subtotal = cartSummary.SubtotalAmount;

                // 檢查最低消費限制
                if (coupon.MinSpend != null && subtotal < coupon.MinSpend)
                {
                    return (false, null, $"需消費滿 {coupon.MinSpend} 元才能使用此優惠券");
                }

                // 🔧 修正：統一折扣計算邏輯，與購物車 API 一致
                decimal calculatedDiscount = coupon.DiscountType switch
                {
                    "%數折扣" => Math.Round(subtotal * (coupon.DiscountAmount / 100m), 0),
                    "滿減" => coupon.DiscountAmount,
                    "J幣回饋" => 0, // J幣回饋不影響當前訂單金額
                    // 向下兼容原有的英文類型
                    "percentage" => subtotal * (coupon.DiscountAmount / 100m),
                    "fixed" => coupon.DiscountAmount,
                    _ => 0
                };

                var couponInfo = new CouponInfoDto
                {
                    CouponId = coupon.Id,
                    Code = couponCode,
                    Title = coupon.Title,
                    DiscountType = coupon.DiscountType,
                    DiscountAmount = coupon.DiscountAmount,
                    MinSpend = coupon.MinSpend,
                    CalculatedDiscount = Math.Min(calculatedDiscount, subtotal)
                };

                return (true, couponInfo, "優惠券可以使用");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "驗證優惠券發生錯誤");
                return (false, null, "驗證優惠券時發生錯誤");
            }
        }

        public async Task<int> GetAvailablePointsAsync(int memberId)
        {
            try
            {
                var memberStats = await _context.MemberStats
                    .FirstOrDefaultAsync(ms => ms.MemberId == memberId);

                return memberStats?.TotalPoints ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得可用點數發生錯誤");
                return 0;
            }
        }

        public async Task<decimal> CalculateMaxPointsDeductionAsync(int memberId, decimal subtotal)
        {
            // 最多可以用點數抵扣訂單金額的30%
            var maxDeduction = subtotal * 0.3m;
            var availablePoints = await GetAvailablePointsAsync(memberId);
            
            return Math.Min(maxDeduction, availablePoints);
        }

        #endregion

        #region 訂單處理

        public async Task<(bool Success, CheckoutResponseDto? Response, string Message)> CreateOrderAsync(CheckoutRequestDto checkoutRequest)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                _logger.LogInformation($"🏁 開始建立訂單流程 - MemberId: {checkoutRequest.MemberId}");
                
                // 1. 驗證結帳前狀態
                _logger.LogInformation($"🔍 Step 1: 驗證結帳前狀態");
                var validation = await ValidateCheckoutAsync(checkoutRequest.MemberId);
                if (!validation.IsValid)
                {
                    var errorMessage = string.Join(", ", validation.Errors.Select(e => e.Message));
                    _logger.LogWarning($"❌ 驗證失敗: {errorMessage}");
                    return (false, null, errorMessage);
                }
                _logger.LogInformation($"✅ Step 1: 驗證通過");

                // 2. 鎖定庫存
                _logger.LogInformation($"🔒 Step 2: 鎖定庫存");
                var inventoryLocked = await LockInventoryAsync(checkoutRequest.MemberId);
                if (!inventoryLocked)
                {
                    _logger.LogWarning($"❌ 庫存鎖定失敗");
                    return (false, null, "庫存鎖定失敗");
                }
                _logger.LogInformation($"✅ Step 2: 庫存鎖定成功");

                // 3. 取得結帳摘要
                _logger.LogInformation($"📊 Step 3: 取得結帳摘要");
                var summary = await GetCheckoutSummaryAsync(
                    checkoutRequest.MemberId,
                    checkoutRequest.CouponCode,
                    checkoutRequest.UsedPoints,
                    checkoutRequest.PaymentMethod);
                _logger.LogInformation($"✅ Step 3: 摘要取得成功，總金額: {summary.TotalAmount}");

                // 4. 按賣家分組購物車商品
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.MemberId == checkoutRequest.MemberId);

                if (cart?.CartItems == null || !cart.CartItems.Any())
                {
                    return (false, null, "購物車為空");
                }

                // 按賣家分組
                var itemsByVendor = cart.CartItems
                    .GroupBy(item => item.Product.SellersId ?? 0)
                    .ToList();

                var createdOrders = new List<Order>();
                var totalOrderAmount = 0m;
                string masterOrderNumber = ""; // 主訂單編號

                // 為每個賣家創建獨立訂單
                foreach (var vendorGroup in itemsByVendor)
                {
                    var vendorId = vendorGroup.Key;
                    var vendorItems = vendorGroup.ToList();
                    
                    // 計算該賣家的小計
                    var vendorSubtotal = vendorItems.Sum(item => item.PriceAtAdded * item.Quantity);
                    
                    // 計算該賣家的運費（簡化：平均分攤或按比例）
                    var vendorShippingFee = itemsByVendor.Count == 1 ? summary.ShippingFee : 
                                          Math.Round(summary.ShippingFee * (vendorSubtotal / summary.SubtotalAmount), 0);
                    
                    // 計算該賣家的折扣（按比例分攤）
                    var vendorDiscount = itemsByVendor.Count == 1 ? summary.DiscountAmount :
                                       Math.Round(summary.DiscountAmount * (vendorSubtotal / summary.SubtotalAmount), 0);
                    
                    // 建立賣家訂單
                    var vendorOrder = new Order
                    {
                        MemberId = checkoutRequest.MemberId,
                        SellersId = vendorId == 0 ? null : vendorId, // 0 表示平台自營
                        RecipientName = checkoutRequest.RecipientName,
                        PhoneNumber = checkoutRequest.PhoneNumber,
                        City = checkoutRequest.City,
                        District = checkoutRequest.District,
                        AddressDetail = checkoutRequest.AddressDetail,
                        DeliveryMethod = checkoutRequest.DeliveryMethod,
                        PaymentMethod = checkoutRequest.PaymentMethod,
                        SubtotalAmount = vendorSubtotal,
                        ShippingFee = vendorShippingFee,
                        DiscountAmount = vendorDiscount,
                        PointsDeductAmount = 0, // 點數折扣只適用於第一個訂單
                        TotalAmount = vendorSubtotal + vendorShippingFee - vendorDiscount,
                        UsedPoints = 0, // 點數只適用於第一個訂單
                        FreeShipping = summary.FreeShipping,
                        OrderStatus = "pending",
                        PaymentStatus = "pending",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    // 🔧 修正：處理 AddressId 外鍵約束問題
                    // 方法1: 如果前端提供了有效的 AddressId，使用它
                    if (checkoutRequest.AddressId.HasValue && checkoutRequest.AddressId.Value > 0)
                    {
                        var addressExists = await _context.MemberAddresses
                            .AnyAsync(a => a.Id == checkoutRequest.AddressId.Value && a.MembersId == checkoutRequest.MemberId);
                        
                        if (addressExists)
                        {
                            vendorOrder.AddressId = checkoutRequest.AddressId.Value;
                            _logger.LogInformation($"✅ 使用指定地址 AddressId: {checkoutRequest.AddressId.Value}");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ 指定的 AddressId {checkoutRequest.AddressId.Value} 不存在");
                            // 嘗試使用會員的預設地址
                            var defaultAddress = await _context.MemberAddresses
                                .FirstOrDefaultAsync(a => a.MembersId == checkoutRequest.MemberId && a.IsDefault);
                            
                            if (defaultAddress != null)
                            {
                                vendorOrder.AddressId = defaultAddress.Id;
                                _logger.LogInformation($"✅ 使用預設地址 AddressId: {defaultAddress.Id}");
                            }
                            else
                            {
                                // 使用會員的第一個地址
                                var firstAddress = await _context.MemberAddresses
                                    .FirstOrDefaultAsync(a => a.MembersId == checkoutRequest.MemberId);
                                
                                if (firstAddress != null)
                                {
                                    vendorOrder.AddressId = firstAddress.Id;
                                    _logger.LogInformation($"✅ 使用第一個地址 AddressId: {firstAddress.Id}");
                                }
                                else
                                {
                                    // 如果會員沒有任何地址記錄，創建一個臨時地址
                                    var tempAddress = new MemberAddress
                                    {
                                        MembersId = checkoutRequest.MemberId,
                                        RecipientName = checkoutRequest.RecipientName,
                                        PhoneNumber = checkoutRequest.PhoneNumber,
                                        City = checkoutRequest.City,
                                        District = checkoutRequest.District,
                                        StreetAddress = checkoutRequest.AddressDetail,
                                        ZipCode = "000",
                                        IsDefault = true,
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    
                                    _context.MemberAddresses.Add(tempAddress);
                                    await _context.SaveChangesAsync();
                                    
                                    vendorOrder.AddressId = tempAddress.Id;
                                    _logger.LogInformation($"✅ 創建臨時地址 AddressId: {tempAddress.Id}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // 方法2: 前端沒有提供 AddressId，尋找或創建地址
                        var defaultAddress = await _context.MemberAddresses
                            .FirstOrDefaultAsync(a => a.MembersId == checkoutRequest.MemberId && a.IsDefault);
                        
                        if (defaultAddress != null)
                        {
                            vendorOrder.AddressId = defaultAddress.Id;
                            _logger.LogInformation($"✅ 使用會員預設地址 AddressId: {defaultAddress.Id}");
                        }
                        else
                        {
                            var firstAddress = await _context.MemberAddresses
                                .FirstOrDefaultAsync(a => a.MembersId == checkoutRequest.MemberId);
                            
                            if (firstAddress != null)
                            {
                                vendorOrder.AddressId = firstAddress.Id;
                                _logger.LogInformation($"✅ 使用會員第一個地址 AddressId: {firstAddress.Id}");
                            }
                            else
                            {
                                // 創建新的地址記錄
                                var newAddress = new MemberAddress
                                {
                                    MembersId = checkoutRequest.MemberId,
                                    RecipientName = checkoutRequest.RecipientName,
                                    PhoneNumber = checkoutRequest.PhoneNumber,
                                    City = checkoutRequest.City,
                                    District = checkoutRequest.District,
                                    StreetAddress = checkoutRequest.AddressDetail,
                                    ZipCode = "000",
                                    IsDefault = true,
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now
                                };
                                
                                _context.MemberAddresses.Add(newAddress);
                                await _context.SaveChangesAsync();
                                
                                vendorOrder.AddressId = newAddress.Id;
                                _logger.LogInformation($"✅ 創建新地址記錄 AddressId: {newAddress.Id}");
                            }
                        }
                    }

                    // 只有第一個訂單使用優惠券和點數
                    if (createdOrders.Count == 0)
                    {
                        if (summary.AppliedCoupon != null)
                        {
                            vendorOrder.CouponId = summary.AppliedCoupon.CouponId;
                        }
                        vendorOrder.UsedPoints = checkoutRequest.UsedPoints;
                        vendorOrder.PointsDeductAmount = summary.PointsDeductAmount;
                        vendorOrder.TotalAmount -= summary.PointsDeductAmount;
                    }

                    _context.Orders.Add(vendorOrder);
                    await _context.SaveChangesAsync();

                    // 建立該賣家的訂單明細
                    foreach (var item in vendorItems)
                    {
                        var orderDetail = new OrderDetail
                        {
                            OrderId = vendorOrder.Id,
                            ProductId = item.ProductId,
                            AttributeValueId = item.AttributeValueId,
                            UnitPrice = item.PriceAtAdded,
                            Quantity = item.Quantity,
                            Subtotal = item.PriceAtAdded * item.Quantity
                        };

                        _context.OrderDetails.Add(orderDetail);
                    }

                    await _context.SaveChangesAsync();
                    
                    createdOrders.Add(vendorOrder);
                    totalOrderAmount += vendorOrder.TotalAmount;

                    // 生成主訂單編號（使用第一個訂單的ID）
                    if (string.IsNullOrEmpty(masterOrderNumber))
                    {
                        masterOrderNumber = "ORD" + vendorOrder.Id.ToString("D8");
                    }

                    var subOrderNumber = itemsByVendor.Count == 1 ? 
                        masterOrderNumber : 
                        $"{masterOrderNumber}-{createdOrders.Count}"; // 子訂單編號

                    _logger.LogInformation($"✅ 賣家訂單建立成功 - 賣家ID: {vendorId}, 訂單ID: {vendorOrder.Id}, 編號: {subOrderNumber}, 金額: {vendorOrder.TotalAmount}");
                }

                // 6. 更新優惠券使用狀態（只適用於第一個訂單）
                if (summary.AppliedCoupon != null && createdOrders.Count > 0)
                {
                    var memberCoupon = await _context.MemberCoupons
                        .FirstOrDefaultAsync(mc => mc.CouponId == summary.AppliedCoupon.CouponId &&
                                                 mc.MemberId == checkoutRequest.MemberId &&
                                                 mc.Status == "active");
                    
                    if (memberCoupon != null)
                    {
                        memberCoupon.Status = "used";
                        memberCoupon.UsedAt = DateTime.Now;
                        memberCoupon.OrderId = createdOrders.First().Id; // 關聯到第一個訂單
                        await _context.SaveChangesAsync();
                    }
                }

                // 7. 扣除點數（只適用於第一個訂單）
                if (checkoutRequest.UsedPoints > 0)
                {
                    var pointsLog = new PointsLog
                    {
                        MemberId = checkoutRequest.MemberId,
                        Amount = -checkoutRequest.UsedPoints,
                        Type = "used",
                        TransactionId = createdOrders.First().Id.ToString(),
                        Note = $"訂單 {createdOrders.First().Id} 使用點數",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.PointsLogs.Add(pointsLog);

                    // 更新會員點數統計
                    var memberStats = await _context.MemberStats
                        .FirstOrDefaultAsync(ms => ms.MemberId == checkoutRequest.MemberId);
                    
                    if (memberStats != null)
                    {
                        memberStats.TotalPoints -= checkoutRequest.UsedPoints;
                        memberStats.UpdatedAt = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();
                }

                // 8. 確認庫存扣除（針對所有訂單）
                foreach (var order in createdOrders)
                {
                    await ConfirmInventoryDeductionAsync(order.Id);
                }

                // 9. 清空購物車
                await ClearCartAfterCheckoutAsync(checkoutRequest.MemberId);

                await transaction.CommitAsync();

                // 10. 建立回應（使用第一個訂單作為主要回應，但包含總金額）
                var mainOrder = createdOrders.First();
                var response = new CheckoutResponseDto
                {
                    OrderId = mainOrder.Id,
                    OrderNumber = mainOrder.Id.ToString().PadLeft(8, '0'),
                    TotalAmount = totalOrderAmount, // 所有訂單的總金額
                    OrderStatus = mainOrder.OrderStatus,
                    PaymentStatus = mainOrder.PaymentStatus,
                    CreatedAt = mainOrder.CreatedAt
                };

                // 11. 發送訂單確認通知（為所有訂單發送）
                foreach (var order in createdOrders)
                {
                    _ = Task.Run(async () => await SendOrderConfirmationAsync(order.Id));
                }

                var orderCount = createdOrders.Count;
                var message = orderCount == 1 ? "訂單建立成功" : $"成功建立 {orderCount} 個賣家訂單";
                
                return (true, response, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "建立訂單發生錯誤");
                _logger.LogError($"❌ Exception 詳細資訊: {ex.Message}");
                _logger.LogError($"❌ Exception StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"❌ Inner Exception: {ex.InnerException.Message}");
                    _logger.LogError($"❌ Inner Exception StackTrace: {ex.InnerException.StackTrace}");
                }
                return (false, null, $"建立訂單失敗: {ex.Message}");
            }
        }

        public async Task<(bool Success, CheckoutResponseDto? Response, string Message)> QuickCheckoutAsync(QuickCheckoutDto quickCheckout)
        {
            // 實作快速結帳邏輯
            // 這裡簡化實作，實際可能需要更複雜的邏輯
            return await CreateOrderAsync(quickCheckout.DeliveryInfo);
        }

        public async Task<OrderConfirmationDto?> GetOrderConfirmationAsync(int orderId, int memberId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Member)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.AttributeValue)
                            .ThenInclude(av => av.AttributeValue)
                                .ThenInclude(av => av.Attribute)
                    .Include(o => o.Coupon)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.MemberId == memberId);

                if (order == null) return null;

                var confirmation = new OrderConfirmationDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.Id.ToString().PadLeft(8, '0'),
                    MemberId = order.MemberId,
                    MemberEmail = order.Member?.Email ?? "",
                    RecipientName = order.RecipientName,
                    PhoneNumber = order.PhoneNumber,
                    DeliveryAddress = $"{order.City}{order.District}{order.AddressDetail}",
                    DeliveryMethod = order.DeliveryMethod,
                    SubtotalAmount = order.SubtotalAmount,
                    ShippingFee = order.ShippingFee,
                    DiscountAmount = (decimal)(order.DiscountAmount ?? 0),
                    PointsDeductAmount = (decimal)(order.PointsDeductAmount ?? 0),
                    TotalAmount = order.TotalAmount,
                    PaymentMethod = order.PaymentMethod,
                    PaymentStatus = order.PaymentStatus,
                    OrderStatus = order.OrderStatus,
                    CouponCode = order.Coupon?.Title,
                    CouponTitle = order.Coupon?.Title,
                    CreatedAt = order.CreatedAt,
                    EstimatedDeliveryDate = await GetEstimatedDeliveryDateAsync(order.DeliveryMethod)
                };

                // 處理訂單商品
                foreach (var detail in order.OrderDetails)
                {
                    var item = new OrderItemDto
                    {
                        Id = detail.Id,
                        OrderDetailId = detail.Id,
                        ProductId = detail.ProductId,
                        ProductName = detail.Product?.Name ?? "未知商品",
                        ProductImage = detail.Product?.ProductImages?.FirstOrDefault()?.ImagesUrl ?? "/images/placeholder.jpg",
                        AttributeValueId = detail.AttributeValueId,
                        AttributeName = detail.AttributeValue?.AttributeValue?.Attribute?.Name ?? "",
                        AttributeValue = detail.AttributeValue?.AttributeValue?.Value ?? "",
                        UnitPrice = (decimal)(detail.UnitPrice ?? 0),
                        Price = (decimal)(detail.UnitPrice ?? 0), // 兼容舊屬性名
                        Quantity = (int)(detail.Quantity ?? 0),
                        Subtotal = (decimal)(detail.Subtotal ?? 0)
                    };

                    confirmation.Items.Add(item);
                }

                return confirmation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得訂單確認資訊發生錯誤");
                return null;
            }
        }

        #endregion

        #region 付款處理

        public async Task<(bool Success, PaymentInfoDto? PaymentInfo, string Message)> ProcessPaymentAsync(int orderId, Dictionary<string, object> paymentData)
        {
            // 這裡應該整合實際的付款服務
            // 目前僅提供模擬實作
            
            var paymentInfo = new PaymentInfoDto
            {
                PaymentMethod = paymentData.GetValueOrDefault("method", "").ToString(),
                TransactionId = Guid.NewGuid().ToString(),
                AdditionalInfo = paymentData
            };

            return (true, paymentInfo, "付款處理成功");
        }

        public async Task<bool> ConfirmPaymentAsync(int orderId, string transactionId)
        {
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null) return false;

                order.PaymentStatus = "completed";
                order.OrderStatus = "confirmed";
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // 觸發會員等級升等檢查
                var upgraded = await _upgradeService.CheckAndUpgradeMemberLevel(order.MemberId, (int)order.TotalAmount);
                if (upgraded)
                {
                    _logger.LogInformation("會員 {MemberId} 在訂單 {OrderId} 付款後升等成功", order.MemberId, orderId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "確認付款發生錯誤");
                return false;
            }
        }

        #endregion

        #region 庫存管理

        public async Task<bool> LockInventoryAsync(int memberId)
        {
            // 實作庫存鎖定邏輯
            // 這裡簡化處理，實際應該要有更複雜的庫存管理
            return true;
        }

        public async Task<bool> ReleaseInventoryAsync(int memberId)
        {
            // 實作庫存釋放邏輯
            return true;
        }

        public async Task<bool> ConfirmInventoryDeductionAsync(int orderId)
        {
            try
            {
                var orderDetails = await _context.OrderDetails
                    .Include(od => od.Product)
                        .ThenInclude(p => p.ProductAttributeValues)
                    .Where(od => od.OrderId == orderId)
                    .ToListAsync();

                foreach (var detail in orderDetails)
                {
                    var productAttributeValue = detail.Product?.ProductAttributeValues?
                        .FirstOrDefault(pav => pav.AttributeValueId == detail.AttributeValueId);

                    if (productAttributeValue != null)
                    {
                        productAttributeValue.Stock -= (int)(detail.Quantity ?? 0);
                        productAttributeValue.UpdatedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "確認庫存扣除發生錯誤");
                return false;
            }
        }

        #endregion

        #region 其他功能

        public async Task<bool> ClearCartAfterCheckoutAsync(int memberId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.MemberId == memberId);

                if (cart != null && cart.CartItems.Any())
                {
                    _context.CartItems.RemoveRange(cart.CartItems);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空購物車發生錯誤");
                return false;
            }
        }

        public async Task<bool> SendOrderConfirmationAsync(int orderId)
        {
            // 實作發送訂單確認通知邏輯
            // 可以發送 Email、簡訊等通知
            _logger.LogInformation($"發送訂單 {orderId} 確認通知");
            return true;
        }

        public async Task<DateTime> GetEstimatedDeliveryDateAsync(string deliveryMethod, int? addressId = null)
        {
            var estimatedDays = deliveryMethod switch
            {
                "standard" => 4,
                "express" => 1,
                "pickup" => 2,
                _ => 4
            };

            return DateTime.Now.AddDays(estimatedDays);
        }

        /// <summary>
        /// 取得訂單付款資訊
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <returns>訂單付款資訊</returns>
        public async Task<OrderPaymentDto?> GetOrderForPaymentAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Member)
                    .ThenInclude(m => m.MemberProfile)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return null;

            return new OrderPaymentDto
            {
                Id = order.Id,
                OrderNumber = "ORD" + order.Id.ToString("D8"),
                TotalAmount = order.TotalAmount,
                MemberName = order.Member?.MemberProfile?.Name ?? "未知會員",
                CreatedAt = order.CreatedAt,
                Items = order.OrderDetails.Select(od => new PaymentOrderItemDto
                {
                    ProductName = od.Product?.Name ?? "未知商品",
                    Quantity = od.Quantity ?? 0,
                    SubTotal = od.Subtotal ?? 0
                }).ToList()
            };
        }

        #endregion
    }
}