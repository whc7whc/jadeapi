using Team.API.Models.DTOs;

namespace Team.API.Services
{
    /// <summary>
    /// 結帳服務介面
    /// </summary>
    public interface ICheckoutService
    {
        // === 結帳前驗證 ===
        
        /// <summary>
        /// 驗證結帳前的購物車狀態
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>驗證結果</returns>
        Task<CheckoutValidationDto> ValidateCheckoutAsync(int memberId);

        /// <summary>
        /// 取得結帳摘要資訊
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="couponCode">優惠券代碼（可選）</param>
        /// <param name="usedPoints">使用點數（可選）</param>
        /// <returns>結帳摘要</returns>
        Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(int memberId, string? couponCode = null, int usedPoints = 0, string? paymentMethod = null);

        // === 配送與付款選項 ===

        /// <summary>
        /// 取得可用的配送方式
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="addressId">地址ID（可選）</param>
        /// <returns>配送方式清單</returns>
        Task<List<DeliveryMethodDto>> GetAvailableDeliveryMethodsAsync(int memberId, int? addressId = null);

        /// <summary>
        /// 取得可用的付款方式
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>付款方式清單</returns>
        Task<List<PaymentMethodDto>> GetAvailablePaymentMethodsAsync(int memberId);

        /// <summary>
        /// 計算運費
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="deliveryMethod">配送方式</param>
        /// <param name="addressId">地址ID（可選）</param>
        /// <returns>運費金額</returns>
        Task<decimal> CalculateShippingFeeAsync(int memberId, string deliveryMethod, int? addressId = null);

        // === 優惠券與點數 ===

        /// <summary>
        /// 驗證優惠券是否可用
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="couponCode">優惠券代碼</param>
        /// <returns>優惠券資訊</returns>
        Task<(bool IsValid, CouponInfoDto? CouponInfo, string Message)> ValidateCouponAsync(int memberId, string couponCode);

        /// <summary>
        /// 取得會員可用點數
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>可用點數</returns>
        Task<int> GetAvailablePointsAsync(int memberId);

        /// <summary>
        /// 計算點數可抵扣的最大金額
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <param name="subtotal">小計金額</param>
        /// <returns>最大抵扣金額</returns>
        Task<decimal> CalculateMaxPointsDeductionAsync(int memberId, decimal subtotal);

        // === 訂單處理 ===

        /// <summary>
        /// 建立訂單
        /// </summary>
        /// <param name="checkoutRequest">結帳請求</param>
        /// <returns>結帳結果</returns>
        Task<(bool Success, CheckoutResponseDto? Response, string Message)> CreateOrderAsync(CheckoutRequestDto checkoutRequest);

        /// <summary>
        /// 快速結帳（直接購買商品）
        /// </summary>
        /// <param name="quickCheckout">快速結帳請求</param>
        /// <returns>結帳結果</returns>
        Task<(bool Success, CheckoutResponseDto? Response, string Message)> QuickCheckoutAsync(QuickCheckoutDto quickCheckout);

        /// <summary>
        /// 取得訂單確認資訊
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <param name="memberId">會員ID</param>
        /// <returns>訂單確認資訊</returns>
        Task<OrderConfirmationDto?> GetOrderConfirmationAsync(int orderId, int memberId);

        // === 付款處理 ===

        /// <summary>
        /// 處理付款
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <param name="paymentData">付款資料</param>
        /// <returns>付款結果</returns>
        Task<(bool Success, PaymentInfoDto? PaymentInfo, string Message)> ProcessPaymentAsync(int orderId, Dictionary<string, object> paymentData);

        /// <summary>
        /// 確認付款完成
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <param name="transactionId">交易ID</param>
        /// <returns>是否成功</returns>
        Task<bool> ConfirmPaymentAsync(int orderId, string transactionId);

        // === 庫存管理 ===

        /// <summary>
        /// 鎖定庫存
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>是否成功鎖定</returns>
        Task<bool> LockInventoryAsync(int memberId);

        /// <summary>
        /// 釋放庫存
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>是否成功釋放</returns>
        Task<bool> ReleaseInventoryAsync(int memberId);

        /// <summary>
        /// 確認庫存扣除
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <returns>是否成功扣除</returns>
        Task<bool> ConfirmInventoryDeductionAsync(int orderId);

        // === 其他功能 ===

        /// <summary>
        /// 清空購物車（結帳成功後）
        /// </summary>
        /// <param name="memberId">會員ID</param>
        /// <returns>是否成功</returns>
        Task<bool> ClearCartAfterCheckoutAsync(int memberId);

        /// <summary>
        /// 發送訂單確認通知
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <returns>是否成功發送</returns>
        Task<bool> SendOrderConfirmationAsync(int orderId);

        /// <summary>
        /// 取得預計配送日期
        /// </summary>
        /// <param name="deliveryMethod">配送方式</param>
        /// <param name="addressId">地址ID（可選）</param>
        /// <returns>預計配送日期</returns>
        Task<DateTime> GetEstimatedDeliveryDateAsync(string deliveryMethod, int? addressId = null);

        /// <summary>
        /// 取得付款方式手續費
        /// </summary>
        /// <param name="paymentMethod">付款方式</param>
        /// <returns>手續費金額</returns>
        Task<decimal> GetPaymentProcessingFeeAsync(string paymentMethod);

        /// <summary>
        /// 取得訂單付款資訊
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        /// <returns>訂單付款資訊</returns>
        Task<OrderPaymentDto?> GetOrderForPaymentAsync(int orderId);
    }
}