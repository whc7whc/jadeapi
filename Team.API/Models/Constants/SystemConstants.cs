namespace Team.API.Models.EfModel
{
    /// <summary>
    /// 點數類型枚舉
    /// </summary>
    public static class PointsType
    {
        public const string Earned = "earned";
        public const string Used = "used";
    }

    /// <summary>
    /// 訂單狀態枚舉
    /// </summary>
    public static class OrderStatus
    {
        public const string Pending = "pending";
        public const string Confirmed = "confirmed";
        public const string Processing = "processing";
        public const string Shipped = "shipped";
        public const string Delivered = "delivered";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";
        public const string Returned = "returned";
    }

    /// <summary>
    /// 付款狀態枚舉
    /// </summary>
    public static class PaymentStatus
    {
        public const string Pending = "pending";
        public const string Processing = "processing";
        public const string Completed = "completed";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
        public const string Refunded = "refunded";
    }

    /// <summary>
    /// 會員優惠券狀態枚舉
    /// </summary>
    public static class MemberCouponStatus
    {
        public const string Active = "active";
        public const string Used = "used";
        public const string Expired = "expired";
        public const string Cancelled = "cancelled";
    }

    /// <summary>
    /// 配送方式常數
    /// </summary>
    public static class DeliveryMethods
    {
        public const string Standard = "standard";
        public const string Express = "express";
        public const string Pickup = "pickup";
        public const string SevenEleven = "seven_eleven";
        public const string FamilyMart = "family_mart";
    }

    /// <summary>
    /// 付款方式常數
    /// </summary>
    public static class PaymentMethods
    {
        public const string CreditCard = "credit_card";
        public const string ATM = "atm";
        public const string LinePay = "linepay";
        public const string COD = "cod";
        public const string ApplePay = "apple_pay";
        public const string GooglePay = "google_pay";
    }

    /// <summary>
    /// 優惠券折扣類型
    /// </summary>
    public static class DiscountTypes
    {
        public const string Percentage = "percentage";
        public const string Fixed = "fixed";
        public const string FreeShipping = "free_shipping";
    }

    /// <summary>
    /// 結帳驗證錯誤類型
    /// </summary>
    public static class CheckoutValidationErrorTypes
    {
        public const string MemberNotFound = "MEMBER_NOT_FOUND";
        public const string EmptyCart = "EMPTY_CART";
        public const string ProductUnavailable = "PRODUCT_UNAVAILABLE";
        public const string InsufficientStock = "INSUFFICIENT_STOCK";
        public const string InvalidCoupon = "INVALID_COUPON";
        public const string InsufficientPoints = "INSUFFICIENT_POINTS";
        public const string SystemError = "SYSTEM_ERROR";
    }

    /// <summary>
    /// 商品狀態
    /// </summary>
    public static class ProductStatus
    {
        public const bool Active = true;
        public const bool Inactive = false;
    }

    /// <summary>
    /// 會員狀態
    /// </summary>
    public static class MemberStatus
    {
        public const bool Active = true;
        public const bool Inactive = false;
    }

    /// <summary>
    /// 預設配置常數
    /// </summary>
    public static class DefaultConfigurations
    {
        /// <summary>
        /// 免運費門檻（元）
        /// </summary>
        public const decimal FreeShippingThreshold = 1000m;

        /// <summary>
        /// 標準運費（元）
        /// </summary>
        public const decimal StandardShippingFee = 60m;

        /// <summary>
        /// 快速配送運費（元）
        /// </summary>
        public const decimal ExpressShippingFee = 120m;

        /// <summary>
        /// 貨到付款手續費（元）
        /// </summary>
        public const decimal CODProcessingFee = 30m;

        /// <summary>
        /// 點數最大抵扣比例（30%）
        /// </summary>
        public const decimal MaxPointsDeductionRatio = 0.3m;

        /// <summary>
        /// 點數兌換比例（1點 = 1元）
        /// </summary>
        public const decimal PointsToMoneyRatio = 1m;

        /// <summary>
        /// 庫存安全預留數量
        /// </summary>
        public const int SafetyStockBuffer = 1;

        /// <summary>
        /// 訂單逾期未付款取消天數
        /// </summary>
        public const int OrderCancellationDays = 7;

        /// <summary>
        /// 優惠券驗證碼長度
        /// </summary>
        public const int CouponCodeLength = 8;
    }

    /// <summary>
    /// 系統通知類型
    /// </summary>
    public static class NotificationTypes
    {
        public const string OrderCreated = "order_created";
        public const string OrderConfirmed = "order_confirmed";
        public const string OrderShipped = "order_shipped";
        public const string OrderDelivered = "order_delivered";
        public const string OrderCancelled = "order_cancelled";
        public const string PaymentCompleted = "payment_completed";
        public const string PaymentFailed = "payment_failed";
        public const string CouponAssigned = "coupon_assigned";
        public const string CouponExpiring = "coupon_expiring";
        public const string PointsEarned = "points_earned";
        public const string PointsExpiring = "points_expiring";
    }

    /// <summary>
    /// 錯誤代碼
    /// </summary>
    public static class ErrorCodes
    {
        // 會員相關
        public const string MEMBER_NOT_FOUND = "E001";
        public const string MEMBER_INACTIVE = "E002";
        public const string INVALID_MEMBER_CREDENTIALS = "E003";

        // 商品相關
        public const string PRODUCT_NOT_FOUND = "E101";
        public const string PRODUCT_INACTIVE = "E102";
        public const string INSUFFICIENT_STOCK = "E103";
        public const string INVALID_PRODUCT_VARIANT = "E104";

        // 購物車相關
        public const string CART_EMPTY = "E201";
        public const string CART_ITEM_NOT_FOUND = "E202";
        public const string INVALID_CART_OPERATION = "E203";

        // 訂單相關
        public const string ORDER_NOT_FOUND = "E301";
        public const string ORDER_CANNOT_BE_MODIFIED = "E302";
        public const string INVALID_ORDER_STATUS = "E303";

        // 付款相關
        public const string PAYMENT_FAILED = "E401";
        public const string PAYMENT_TIMEOUT = "E402";
        public const string INVALID_PAYMENT_METHOD = "E403";

        // 優惠券相關
        public const string COUPON_NOT_FOUND = "E501";
        public const string COUPON_EXPIRED = "E502";
        public const string COUPON_ALREADY_USED = "E503";
        public const string COUPON_NOT_APPLICABLE = "E504";

        // 點數相關
        public const string INSUFFICIENT_POINTS = "E601";
        public const string INVALID_POINTS_OPERATION = "E602";

        // 系統相關
        public const string SYSTEM_ERROR = "E901";
        public const string VALIDATION_ERROR = "E902";
        public const string UNAUTHORIZED = "E903";
        public const string FORBIDDEN = "E904";
    }
}