using Team.API.Models.DTOs;

namespace Team.API.Services
{
    /// <summary>
    /// 購物車服務介面
    /// </summary>
    public interface ICartService
    {
        // === 基本購物車操作 ===

        /// <summary>
        /// 取得用戶購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <returns>購物車資料</returns>
        Task<CartResponseDto> GetCartAsync(int userId);

        /// <summary>
        /// 取得購物車摘要 (用於結帳等場景)
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <returns>購物車摘要</returns>
        //1
        Task<CartSummaryDto> GetCartSummaryAsync(int userId);

        /// <summary>
        /// 加入商品到購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="dto">加入購物車資料</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> AddToCartAsync(int userId, AddCartItemDto dto);

        /// <summary>
        /// 批量加入商品到購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="dto">批量加入資料</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> QuickAddToCartAsync(int userId, QuickAddCartDto dto);

        /// <summary>
        /// 更新購物車商品數量
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="itemId">購物車商品ID</param>
        /// <param name="dto">更新資料</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> UpdateCartItemAsync(int userId, int itemId, UpdateCartItemDto dto);

        /// <summary>
        /// 移除購物車商品
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="itemId">購物車商品ID</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> RemoveCartItemAsync(int userId, int itemId);

        /// <summary>
        /// 批量移除購物車商品
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="itemIds">要移除的商品ID清單</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> RemoveMultipleItemsAsync(int userId, List<int> itemIds);

        /// <summary>
        /// 清空購物車
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <returns>操作結果</returns>
        Task<bool> ClearCartAsync(int userId);

        // === 優惠券相關 ===

        /// <summary>
        /// 套用優惠券
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="couponCode">優惠券代碼</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> ApplyCouponAsync(int userId, string couponCode);

        /// <summary>
        /// 移除優惠券
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <returns>操作結果</returns>
        Task<CartOperationResult> RemoveCouponAsync(int userId);

        /// <summary>
        /// 驗證購物車 (檢查庫存、價格變動等)
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <returns>驗證結果</returns>
        Task<CartValidationDto> ValidateCartAsync(int userId);

        /// <summary>
        /// 檢查商品庫存
        /// </summary>
        /// <param name="productId">商品ID</param>
        /// <param name="attributeValueId">屬性值ID</param>
        /// <param name="quantity">需要的數量</param>
        /// <returns>庫存檢查結果</returns>
        Task<StockCheckDto> CheckStockAsync(int productId, int attributeValueId, int quantity);

        /// <summary>
        /// 檢查購物車是否可以結帳
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <returns>是否可以結帳</returns>
        Task<bool> CanCheckoutAsync(int userId);

        // === 購物車管理 ===

        /// <summary>
        /// 合併購物車 (登入時合併訪客購物車)
        /// </summary>
        /// <param name="guestCartId">訪客購物車ID</param>
        /// <param name="memberUserId">會員用戶ID</param>
        /// <returns>合併後的購物車</returns>
        Task<CartOperationResult> MergeCartsAsync(int guestCartId, int memberUserId);

        /// <summary>
        /// 取得購物車統計資料 (管理員用)
        /// </summary>
        /// <returns>統計資料</returns>
        Task<CartStatisticsDto> GetCartStatisticsAsync();

        /// <summary>
        /// 清理過期的購物車
        /// </summary>
        /// <param name="daysOld">幾天前的購物車</param>
        /// <returns>清理的購物車數量</returns>
        Task<int> CleanupExpiredCartsAsync(int daysOld = 30);

        // === 其他功能 ===

        /// <summary>
        /// 計算運費
        /// </summary>
        /// <param name="subtotal">小計金額</param>
        /// <param name="userId">用戶ID (用於查詢會員等級等)</param>
        /// <returns>運費</returns>
        Task<decimal> CalculateShippingAsync(decimal subtotal, int? userId = null);

        /// <summary>
        /// 取得推薦商品 (基於購物車內容)
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="limit">推薦數量限制</param>
        /// <returns>推薦商品清單</returns>
        Task<List<object>> GetRecommendedProductsAsync(int userId, int limit = 5);
    }
}