using Microsoft.AspNetCore.Mvc;
using Team.API.Models.DTOs;
using Team.API.Services;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(ICheckoutService checkoutService, ILogger<CheckoutController> logger)
        {
            _checkoutService = checkoutService;
            _logger = logger;
        }

        #region 結帳前驗證與資訊

        /// <summary>
        /// 驗證結帳前的購物車狀態
        /// </summary>
        /// <param name="memberId">會員ID</param>
        [HttpPost("validate/{memberId}")]
        public async Task<ActionResult<ApiResponse<CheckoutValidationDto>>> ValidateCheckout(int memberId)
        {
            try
            {
                var validation = await _checkoutService.ValidateCheckoutAsync(memberId);
                
                if (validation.IsValid)
                {
                    return Ok(ApiResponse<CheckoutValidationDto>.SuccessResult(validation, "結帳驗證通過"));
                }
                else
                {
                    return BadRequest(ApiResponse<CheckoutValidationDto>.ErrorResult(
                        "結帳驗證失敗", 
                        validation.Errors.Select(e => e.Message).ToList(),
                        validation));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "結帳驗證發生錯誤");
                return StatusCode(500, ApiResponse<CheckoutValidationDto>.ErrorResult("系統錯誤，請稍後再試"));
            }
        }

        /// <summary>
        /// 取得結帳摘要資訊
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="couponCode">優惠券代碼（可選）</param>
        /// <param name="usedPoints">使用點數（可選）</param>
        /// <param name="paymentMethod">付款方式（可選）</param>
        [HttpGet("summary/{memberId}")]
        public async Task<ActionResult<ApiResponse<CheckoutSummaryDto>>> GetCheckoutSummary(
            int memberId, 
            [FromQuery] string? couponCode = null, 
            [FromQuery] int usedPoints = 0,
            [FromQuery] string? paymentMethod = null)
        {
            try
            {
                var summary = await _checkoutService.GetCheckoutSummaryAsync(memberId, couponCode, usedPoints, paymentMethod);
                return Ok(ApiResponse<CheckoutSummaryDto>.SuccessResult(summary, "成功取得結帳摘要"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得結帳摘要發生錯誤");
                return StatusCode(500, ApiResponse<CheckoutSummaryDto>.ErrorResult("取得結帳摘要失敗"));
            }
        }

        /// <summary>
        /// 取得可用的配送方式
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="addressId">地址ID（可選）</param>
        [HttpGet("delivery-methods/{memberId}")]
        public async Task<ActionResult<ApiResponse<List<DeliveryMethodDto>>>> GetDeliveryMethods(
            int memberId, 
            [FromQuery] int? addressId = null)
        {
            try
            {
                var methods = await _checkoutService.GetAvailableDeliveryMethodsAsync(memberId, addressId);
                return Ok(ApiResponse<List<DeliveryMethodDto>>.SuccessResult(methods, "成功取得配送方式"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得配送方式發生錯誤");
                return StatusCode(500, ApiResponse<List<DeliveryMethodDto>>.ErrorResult("取得配送方式失敗"));
            }
        }

        /// <summary>
        /// 取得可用的付款方式
        /// </summary>
        /// <param name="memberId">會員ID</param>
        [HttpGet("payment-methods/{memberId}")]
        public async Task<ActionResult<ApiResponse<List<PaymentMethodDto>>>> GetPaymentMethods(int memberId)
        {
            try
            {
                var methods = await _checkoutService.GetAvailablePaymentMethodsAsync(memberId);
                return Ok(ApiResponse<List<PaymentMethodDto>>.SuccessResult(methods, "成功取得付款方式"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得付款方式發生錯誤");
                return StatusCode(500, ApiResponse<List<PaymentMethodDto>>.ErrorResult("取得付款方式失敗"));
            }
        }

        /// <summary>
        /// 計算運費
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="deliveryMethod">配送方式</param>
        /// <param name="addressId">地址ID（可選）</param>
        [HttpGet("shipping-fee/{memberId}")]
        public async Task<ActionResult<ApiResponse<decimal>>> CalculateShippingFee(
            int memberId, 
            [FromQuery] string deliveryMethod, 
            [FromQuery] int? addressId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(deliveryMethod))
                {
                    return BadRequest(ApiResponse<decimal>.ErrorResult("配送方式不能為空"));
                }

                var fee = await _checkoutService.CalculateShippingFeeAsync(memberId, deliveryMethod, addressId);
                return Ok(ApiResponse<decimal>.SuccessResult(fee, "成功計算運費"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "計算運費發生錯誤");
                return StatusCode(500, ApiResponse<decimal>.ErrorResult("計算運費失敗"));
            }
        }

        #endregion

        #region 優惠券與點數

        /// <summary>
        /// 驗證優惠券
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="couponCode">優惠券代碼</param>
        [HttpPost("validate-coupon/{memberId}")]
        public async Task<ActionResult<ApiResponse<CouponInfoDto>>> ValidateCoupon(
            int memberId, 
            [FromBody] string couponCode)
        {
            try
            {
                if (string.IsNullOrEmpty(couponCode))
                {
                    return BadRequest(ApiResponse<CouponInfoDto>.ErrorResult("優惠券代碼不能為空"));
                }

                var (isValid, couponInfo, message) = await _checkoutService.ValidateCouponAsync(memberId, couponCode);
                
                if (isValid && couponInfo != null)
                {
                    return Ok(ApiResponse<CouponInfoDto>.SuccessResult(couponInfo, message));
                }
                else
                {
                    return BadRequest(ApiResponse<CouponInfoDto>.ErrorResult(message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "驗證優惠券發生錯誤");
                return StatusCode(500, ApiResponse<CouponInfoDto>.ErrorResult("驗證優惠券失敗"));
            }
        }

        /// <summary>
        /// 取得會員可用點數
        /// </summary>
        /// <param name="memberId">會員ID</param>
        [HttpGet("available-points/{memberId}")]
        public async Task<ActionResult<ApiResponse<int>>> GetAvailablePoints(int memberId)
        {
            try
            {
                var points = await _checkoutService.GetAvailablePointsAsync(memberId);
                return Ok(ApiResponse<int>.SuccessResult(points, "成功取得可用點數"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得可用點數發生錯誤");
                return StatusCode(500, ApiResponse<int>.ErrorResult("取得可用點數失敗"));
            }
        }

        /// <summary>
        /// 計算點數最大抵扣金額
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="subtotal">小計金額</param>
        [HttpGet("max-points-deduction/{memberId}")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetMaxPointsDeduction(
            int memberId, 
            [FromQuery] decimal subtotal)
        {
            try
            {
                var maxDeduction = await _checkoutService.CalculateMaxPointsDeductionAsync(memberId, subtotal);
                return Ok(ApiResponse<decimal>.SuccessResult(maxDeduction, "成功計算最大點數抵扣金額"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "計算最大點數抵扣金額發生錯誤");
                return StatusCode(500, ApiResponse<decimal>.ErrorResult("計算最大點數抵扣金額失敗"));
            }
        }

        #endregion

        #region 訂單處理

        /// <summary>
        /// 建立訂單（結帳）
        /// </summary>
        /// <param name="checkoutRequest">結帳請求</param>
        [HttpPost("create-order")]
        public async Task<ActionResult<ApiResponse<CheckoutResponseDto>>> CreateOrder([FromBody] CheckoutRequestDto checkoutRequest)
        {
            try
            {
                _logger.LogInformation($"🔥 收到建立訂單請求 - MemberId: {checkoutRequest?.MemberId}");
                
                if (checkoutRequest == null)
                {
                    _logger.LogError("❌ checkoutRequest 為 null");
                    return BadRequest(ApiResponse<CheckoutResponseDto>.ErrorResult("請求資料不能為空"));
                }
                
                _logger.LogInformation($"📝 請求資料: {System.Text.Json.JsonSerializer.Serialize(checkoutRequest)}");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogError($"❌ 模型驗證失敗: {string.Join(", ", errors)}");
                    
                    // 詳細記錄每個欄位的驗證狀態
                    foreach (var field in ModelState)
                    {
                        if (field.Value.Errors.Any())
                        {
                            _logger.LogError($"🔍 欄位 '{field.Key}' 驗證失敗: {string.Join(", ", field.Value.Errors.Select(e => e.ErrorMessage))}");
                        }
                    }
                    
                    return BadRequest(ApiResponse<CheckoutResponseDto>.ErrorResult("輸入資料有誤", errors));
                }

                var (success, response, message) = await _checkoutService.CreateOrderAsync(checkoutRequest);
                
                _logger.LogInformation($"📊 訂單建立結果 - Success: {success}, Message: {message}");
                
                if (success && response != null)
                {
                    return Ok(ApiResponse<CheckoutResponseDto>.SuccessResult(response, message));
                }
                else
                {
                    _logger.LogWarning($"❌ 訂單建立失敗: {message}");
                    return BadRequest(ApiResponse<CheckoutResponseDto>.ErrorResult(message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立訂單發生錯誤");
                return StatusCode(500, ApiResponse<CheckoutResponseDto>.ErrorResult($"建立訂單失敗: {ex.Message} | StackTrace: {ex.StackTrace}"));
            }
        }

        /// <summary>
        /// 快速結帳（立即購買）
        /// </summary>
        /// <param name="quickCheckout">快速結帳請求</param>
        [HttpPost("quick-checkout")]
        public async Task<ActionResult<ApiResponse<CheckoutResponseDto>>> QuickCheckout([FromBody] QuickCheckoutDto quickCheckout)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ApiResponse<CheckoutResponseDto>.ErrorResult("輸入資料有誤", errors));
                }

                var (success, response, message) = await _checkoutService.QuickCheckoutAsync(quickCheckout);
                
                if (success && response != null)
                {
                    return Ok(ApiResponse<CheckoutResponseDto>.SuccessResult(response, message));
                }
                else
                {
                    return BadRequest(ApiResponse<CheckoutResponseDto>.ErrorResult(message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "快速結帳發生錯誤");
                return StatusCode(500, ApiResponse<CheckoutResponseDto>.ErrorResult("快速結帳失敗"));
            }
        }

        /// <summary>
        /// 取得訂單確認資訊
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <param name="memberId">會員ID</param>
        [HttpGet("order-confirmation")]
        public async Task<ActionResult<ApiResponse<OrderConfirmationDto>>> GetOrderConfirmation(
            [FromQuery] int orderId, 
            [FromQuery] int memberId)
        {
            try
            {
                var confirmation = await _checkoutService.GetOrderConfirmationAsync(orderId, memberId);
                
                if (confirmation != null)
                {
                    return Ok(ApiResponse<OrderConfirmationDto>.SuccessResult(confirmation, "成功取得訂單確認資訊"));
                }
                else
                {
                    return NotFound(ApiResponse<OrderConfirmationDto>.ErrorResult("找不到訂單資訊"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得訂單確認資訊發生錯誤");
                return StatusCode(500, ApiResponse<OrderConfirmationDto>.ErrorResult("取得訂單確認資訊失敗"));
            }
        }

        #endregion

        #region 付款處理

        /// <summary>
        /// 處理付款
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <param name="paymentData">付款資料</param>
        [HttpPost("process-payment/{orderId}")]
        public async Task<ActionResult<ApiResponse<PaymentInfoDto>>> ProcessPayment(
            int orderId, 
            [FromBody] Dictionary<string, object> paymentData)
        {
            try
            {
                if (paymentData == null || !paymentData.Any())
                {
                    return BadRequest(ApiResponse<PaymentInfoDto>.ErrorResult("付款資料不能為空"));
                }

                var (success, paymentInfo, message) = await _checkoutService.ProcessPaymentAsync(orderId, paymentData);
                
                if (success && paymentInfo != null)
                {
                    return Ok(ApiResponse<PaymentInfoDto>.SuccessResult(paymentInfo, message));
                }
                else
                {
                    return BadRequest(ApiResponse<PaymentInfoDto>.ErrorResult(message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理付款發生錯誤");
                return StatusCode(500, ApiResponse<PaymentInfoDto>.ErrorResult("處理付款失敗"));
            }
        }

        /// <summary>
        /// 確認付款完成
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <param name="transactionId">交易ID</param>
        [HttpPost("confirm-payment")]
        public async Task<ActionResult<ApiResponse<bool>>> ConfirmPayment(
            [FromQuery] int orderId, 
            [FromQuery] string transactionId)
        {
            try
            {
                if (string.IsNullOrEmpty(transactionId))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResult("交易ID不能為空"));
                }

                var success = await _checkoutService.ConfirmPaymentAsync(orderId, transactionId);
                
                if (success)
                {
                    return Ok(ApiResponse<bool>.SuccessResult(true, "付款確認成功"));
                }
                else
                {
                    return BadRequest(ApiResponse<bool>.ErrorResult("付款確認失敗"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "確認付款發生錯誤");
                return StatusCode(500, ApiResponse<bool>.ErrorResult("確認付款失敗"));
            }
        }

        #endregion

        #region 其他功能

        /// <summary>
        /// 取得預計配送日期
        /// </summary>
        /// <param name="deliveryMethod">配送方式</param>
        /// <param name="addressId">地址ID（可選）</param>
        [HttpGet("estimated-delivery")]
        public async Task<ActionResult<ApiResponse<DateTime>>> GetEstimatedDeliveryDate(
            [FromQuery] string deliveryMethod, 
            [FromQuery] int? addressId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(deliveryMethod))
                {
                    return BadRequest(ApiResponse<DateTime>.ErrorResult("配送方式不能為空"));
                }

                var estimatedDate = await _checkoutService.GetEstimatedDeliveryDateAsync(deliveryMethod, addressId);
                return Ok(ApiResponse<DateTime>.SuccessResult(estimatedDate, "成功取得預計配送日期"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得預計配送日期發生錯誤");
                return StatusCode(500, ApiResponse<DateTime>.ErrorResult("取得預計配送日期失敗"));
            }
        }

        #endregion
    }

    /// <summary>
    /// API 回應統一格式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static ApiResponse<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null, T? data = default)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data,
                Errors = errors
            };
        }
    }
}