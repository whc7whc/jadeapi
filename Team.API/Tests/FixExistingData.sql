-- ========== 修正現有資料的優惠券關聯 (Snake_Case 版本) ==========
-- 根據你現有的資料庫內容進行修正
-- 🔧 使用正確的 snake_case 資料庫結構

-- 1. 先檢查現有的優惠券資料
SELECT 'Current Coupons:' AS [Status];
SELECT Id, Title, Discount_Type, Discount_Amount, Min_Spend, Is_Active, Start_At, Expired_At
FROM Coupons 
WHERE Id IN (1, 2)
ORDER BY Id;

-- 2. 檢查現有的用戶資料
SELECT 'Current Members:' AS [Status];
SELECT Id, Email, Level, Is_Active
FROM Members 
WHERE Id = 1;

-- 3. 檢查現有的 Member_Coupons 關聯
SELECT 'Current Member_Coupons:' AS [Status];
SELECT mc.*, c.Title 
FROM Member_Coupons mc
LEFT JOIN Coupons c ON mc.Coupon_Id = c.Id
WHERE mc.Member_Id = 1;

-- 4. 清理並重新建立正確的關聯
PRINT '🧹 清理舊的測試資料...';
DELETE FROM Member_Coupons WHERE Member_Id = 1 AND Coupon_Id IN (1, 2);

-- 5. 確保測試用戶存在
IF NOT EXISTS (SELECT 1 FROM Members WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT Members ON;
    INSERT INTO Members (Id, Email, Password_Hash, Registered_Via, Is_Email_Verified, Is_Active, Level, Role, Created_At, Updated_At)
    VALUES (1, 'test@example.com', 'hash123', 'email', 1, 1, 1, 0, GETDATE(), GETDATE());
    SET IDENTITY_INSERT Members OFF;
    PRINT '✅ 建立測試用戶 ID: 1';
END
ELSE
BEGIN
    PRINT '✅ 測試用戶已存在 ID: 1';
END

-- 6. 根據現有優惠券資料建立關聯
-- 檢查 ID=1 的優惠券是否存在且有效
IF EXISTS (SELECT 1 FROM Coupons WHERE Id = 1 AND Is_Active = 1)
BEGIN
    INSERT INTO Member_Coupons (Member_Id, Coupon_Id, Status, Assigned_At, Verification_Code, Updated_At)
    VALUES (1, 1, 'active', GETDATE(), 'TEST001', GETDATE());
    PRINT '✅ 分配優惠券 ID:1 給用戶 ID:1';
END
ELSE
BEGIN
    PRINT '⚠️ 優惠券 ID:1 不存在或未啟用';
END

-- 檢查 ID=2 的優惠券是否存在且有效
IF EXISTS (SELECT 1 FROM Coupons WHERE Id = 2 AND Is_Active = 1)
BEGIN
    INSERT INTO Member_Coupons (Member_Id, Coupon_Id, Status, Assigned_At, Verification_Code, Updated_At)
    VALUES (1, 2, 'active', GETDATE(), 'TEST002', GETDATE());
    PRINT '✅ 分配優惠券 ID:2 給用戶 ID:1';
END
ELSE
BEGIN
    PRINT '⚠️ 優惠券 ID:2 不存在或未啟用';
END

-- 7. 確保測試商品存在
IF NOT EXISTS (SELECT 1 FROM Products WHERE Id = 1)
BEGIN
    -- 先確保分類存在
    IF NOT EXISTS (SELECT 1 FROM Categories WHERE Id = 1)
    BEGIN
        SET IDENTITY_INSERT Categories ON;
        INSERT INTO Categories (Id, Name, Is_Active, Created_At, Updated_At)
        VALUES (1, '測試分類', 1, GETDATE(), GETDATE());
        SET IDENTITY_INSERT Categories OFF;
    END

    -- 確保賣家存在
    IF NOT EXISTS (SELECT 1 FROM Sellers WHERE Id = 1)
    BEGIN
        SET IDENTITY_INSERT Sellers ON;
        INSERT INTO Sellers (Id, Member_Id, Real_Name, Application_Status, Approved_At, Created_At, Updated_At)
        VALUES (1, 1, '測試賣家', 'approved', GETDATE(), GETDATE(), GETDATE());
        SET IDENTITY_INSERT Sellers OFF;
    END

    -- 建立測試商品
    SET IDENTITY_INSERT Products ON;
    INSERT INTO Products (Id, Name, Description, Price, Category_Id, Seller_Id, Is_Active, Created_At, Updated_At)
    VALUES (1, '測試商品', '用於 Swagger 測試的商品', 500, 1, 1, 1, GETDATE(), GETDATE());
    SET IDENTITY_INSERT Products OFF;
    PRINT '✅ 建立測試商品 ID:1 (價格: $500)';
END

-- 8. 確保商品屬性值存在
IF NOT EXISTS (SELECT 1 FROM Product_Attribute_Values WHERE Id = 1)
BEGIN
    -- 確保屬性存在
    IF NOT EXISTS (SELECT 1 FROM Attributes WHERE Id = 1)
    BEGIN
        SET IDENTITY_INSERT Attributes ON;
        INSERT INTO Attributes (Id, Name, Type, Created_At, Updated_At)
        VALUES (1, '款式', 'text', GETDATE(), GETDATE());
        SET IDENTITY_INSERT Attributes OFF;
    END

    -- 確保屬性值存在
    IF NOT EXISTS (SELECT 1 FROM Attribute_Values WHERE Id = 1)
    BEGIN
        SET IDENTITY_INSERT Attribute_Values ON;
        INSERT INTO Attribute_Values (Id, Attribute_Id, Value, Created_At, Updated_At)
        VALUES (1, 1, '標準款', GETDATE(), GETDATE());
        SET IDENTITY_INSERT Attribute_Values OFF;
    END

    -- 建立商品屬性值
    SET IDENTITY_INSERT Product_Attribute_Values ON;
    INSERT INTO Product_Attribute_Values (Id, Product_Id, Attribute_Value_Id, Stock, Sku, Created_At, Updated_At)
    VALUES (1, 1, 1, 100, 'TEST-001', GETDATE(), GETDATE());
    SET IDENTITY_INSERT Product_Attribute_Values OFF;
    PRINT '✅ 建立測試商品屬性值 ID:1 (庫存: 100)';
END

-- 9. 清理測試購物車
DELETE FROM Cart_Items WHERE Cart_Id IN (SELECT Id FROM Carts WHERE Member_Id = 1);
DELETE FROM Carts WHERE Member_Id = 1;
PRINT '✅ 清理測試購物車';

-- 10. 最終驗證
PRINT '';
PRINT '🔍 最終驗證結果：';
PRINT '==================';

DECLARE @memberCount INT = (SELECT COUNT(*) FROM Members WHERE Id = 1);
DECLARE @couponCount INT = (SELECT COUNT(*) FROM Coupons WHERE Id IN (1,2) AND Is_Active = 1);
DECLARE @memberCouponCount INT = (SELECT COUNT(*) FROM Member_Coupons WHERE Member_Id = 1);
DECLARE @productCount INT = (SELECT COUNT(*) FROM Products WHERE Id = 1 AND Is_Active = 1);
DECLARE @attrValueCount INT = (SELECT COUNT(*) FROM Product_Attribute_Values WHERE Id = 1);

PRINT '👤 測試用戶數量: ' + CAST(@memberCount AS VARCHAR(10));
PRINT '🎫 有效優惠券數量: ' + CAST(@couponCount AS VARCHAR(10));
PRINT '🔗 用戶優惠券關聯: ' + CAST(@memberCouponCount AS VARCHAR(10));
PRINT '📦 測試商品數量: ' + CAST(@productCount AS VARCHAR(10));
PRINT '🔧 商品屬性值數量: ' + CAST(@attrValueCount AS VARCHAR(10));

-- 11. 顯示可用的測試資料
PRINT '';
PRINT '📋 Swagger 測試資料：';
PRINT '===================';

-- 顯示可用優惠券
SELECT '可用優惠券:' AS [Type], Id, Title, Discount_Type, Discount_Amount, Min_Spend
FROM Coupons 
WHERE Id IN (SELECT Coupon_Id FROM Member_Coupons WHERE Member_Id = 1)
ORDER BY Id;

-- 12. 顯示最重要的驗證查詢
PRINT '';
PRINT '🔍 重要驗證查詢（確認優惠券關聯是否正確）：';
PRINT '';
PRINT '-- 檢查用戶是否有優惠券';
PRINT 'SELECT mc.*, c.Title, c.Discount_Type, c.Discount_Amount, c.Min_Spend';
PRINT 'FROM Member_Coupons mc';
PRINT 'JOIN Coupons c ON mc.Coupon_Id = c.Id';
PRINT 'WHERE mc.Member_Id = 1 AND mc.Status = ''active'';';
PRINT '';

-- 執行關鍵驗證查詢
SELECT 'Final verification - User coupons:' AS [Info];
SELECT mc.Member_Id, mc.Coupon_Id, mc.Status, c.Title, c.Discount_Type, c.Discount_Amount, c.Min_Spend,
       CASE 
           WHEN c.Start_At > GETDATE() THEN '未開始'
           WHEN c.Expired_At < GETDATE() THEN '已過期'
           WHEN c.Is_Active = 1 THEN '啟用中'
           ELSE '未啟用'
       END AS [Coupon_Status]
FROM Member_Coupons mc
JOIN Coupons c ON mc.Coupon_Id = c.Id
WHERE mc.Member_Id = 1 AND mc.Status = 'active';

PRINT '';
PRINT '🧪 測試建議：';
PRINT '1. GET /api/Carts/user/1 - 取得購物車';
PRINT '2. POST /api/Carts/user/1/items - 加入商品 (productId:1, attributeValueId:1, quantity:2)';
PRINT '3. GET /api/Coupons/UserAvailable/1 - 查看可用優惠券';
PRINT '4. POST /api/Carts/user/1/coupon - 套用優惠券';
PRINT '';
PRINT '✅ 修正完成！可以開始 Swagger 測試了！