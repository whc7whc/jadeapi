-- ========== Swagger 測試資料 (Snake_Case 版本) ==========
-- 用於 Swagger UI 測試購物車和優惠券功能
-- 🔧 使用正確的 snake_case 資料庫結構

-- 設定時區和日期格式
SET DATEFORMAT ymd;

-- 1. 建立測試用戶 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Members] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Members] ON;
    INSERT INTO [Members] ([Id], [Email], [Password_Hash], [Registered_Via], [Is_Email_Verified], [Is_Active], [Level], [Role], [Created_At], [Updated_At])
    VALUES (1, 'test@example.com', 'hash123', 'email', 1, 1, 1, 0, GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Members] OFF;
    PRINT '✅ 建立測試用戶: test@example.com (ID: 1)';
END
ELSE
BEGIN
    PRINT '✅ 測試用戶已存在 (ID: 1)';
END

-- 2. 建立測試會員等級 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Membership_Levels] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Membership_Levels] ON;
    INSERT INTO [Membership_Levels] ([Id], [Level_Name], [Required_Amount], [Is_Active], [Created_At], [Updated_At])
    VALUES (1, '普通會員', 0, 1, GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Membership_Levels] OFF;
    PRINT '✅ 建立測試會員等級: 普通會員';
END

-- 3. 建立測試商品分類 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Categories] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Categories] ON;
    INSERT INTO [Categories] ([Id], [Name], [Is_Active], [Created_At], [Updated_At])
    VALUES (1, '測試分類', 1, GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Categories] OFF;
    PRINT '✅ 建立測試商品分類: 測試分類';
END

-- 4. 建立測試賣家 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Sellers] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Sellers] ON;
    INSERT INTO [Sellers] ([Id], [Member_Id], [Real_Name], [Application_Status], [Approved_At], [Created_At], [Updated_At])
    VALUES (1, 1, '測試賣家', 'approved', GETDATE(), GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Sellers] OFF;
    PRINT '✅ 建立測試賣家: 測試賣家';
END

-- 5. 建立測試商品 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Products] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Products] ON;
    INSERT INTO [Products] ([Id], [Name], [Description], [Price], [Category_Id], [Seller_Id], [Is_Active], [Created_At], [Updated_At])
    VALUES (1, '測試商品', '這是用於測試的商品', 500, 1, 1, 1, GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Products] OFF;
    PRINT '✅ 建立測試商品: 測試商品 (價格: $500)';
END

-- 6. 建立測試屬性 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Attributes] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Attributes] ON;
    INSERT INTO [Attributes] ([Id], [Name], [Type], [Created_At], [Updated_At])
    VALUES (1, '款式', 'text', GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Attributes] OFF;
    PRINT '✅ 建立測試屬性: 款式';
END

-- 7. 建立測試屬性值 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Attribute_Values] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Attribute_Values] ON;
    INSERT INTO [Attribute_Values] ([Id], [Attribute_Id], [Value], [Created_At], [Updated_At])
    VALUES (1, 1, '標準款', GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Attribute_Values] OFF;
    PRINT '✅ 建立測試屬性值: 標準款';
END

-- 8. 建立測試商品屬性值 (如果不存在)
IF NOT EXISTS (SELECT 1 FROM [Product_Attribute_Values] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Product_Attribute_Values] ON;
    INSERT INTO [Product_Attribute_Values] ([Id], [Product_Id], [Attribute_Value_Id], [Stock], [Sku], [Created_At], [Updated_At])
    VALUES (1, 1, 1, 100, 'TEST-001', GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Product_Attribute_Values] OFF;
    PRINT '✅ 建立測試商品屬性值: 庫存 100';
END

-- 9. 建立測試優惠券 (使用正確的欄位名稱)
-- 優惠券 1: 10% 折扣券
IF NOT EXISTS (SELECT 1 FROM [Coupons] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Coupons] ON;
    INSERT INTO [Coupons] ([Id], [Title], [Discount_Type], [Discount_Amount], [Min_Spend], [Start_At], [Expired_At], [Usage_Limit], [Used_Count], [Is_Active], [Created_At], [Updated_At])
    VALUES (1, '測試優惠券-10%折扣', '%數折扣', 10, 100, DATEADD(day, -1, GETDATE()), DATEADD(day, 30, GETDATE()), 100, 0, 1, GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Coupons] OFF;
    PRINT '✅ 建立測試優惠券 1: 10% 折扣券 (滿 $100)';
END

-- 優惠券 2: 滿減券
IF NOT EXISTS (SELECT 1 FROM [Coupons] WHERE [Id] = 2)
BEGIN
    SET IDENTITY_INSERT [Coupons] ON;
    INSERT INTO [Coupons] ([Id], [Title], [Discount_Type], [Discount_Amount], [Min_Spend], [Start_At], [Expired_At], [Usage_Limit], [Used_Count], [Is_Active], [Created_At], [Updated_At])
    VALUES (2, '測試優惠券-滿減50', '滿減', 50, 300, DATEADD(day, -1, GETDATE()), DATEADD(day, 30, GETDATE()), 50, 0, 1, GETDATE(), GETDATE());
    SET IDENTITY_INSERT [Coupons] OFF;
    PRINT '✅ 建立測試優惠券 2: 滿減 $50 (滿 $300)';
END

-- 10. 關鍵步驟：將優惠券分配給測試用戶 (使用正確的表名和欄位名)
-- 刪除舊的關聯，重新建立
DELETE FROM [Member_Coupons] WHERE [Member_Id] = 1 AND [Coupon_Id] IN (1, 2);

-- 分配優惠券 1 給用戶 1
INSERT INTO [Member_Coupons] ([Member_Id], [Coupon_Id], [Status], [Assigned_At], [Verification_Code], [Updated_At])
VALUES (1, 1, 'active', GETDATE(), 'TEST001', GETDATE());
PRINT '✅ 分配優惠券 1 給測試用戶';

-- 分配優惠券 2 給用戶 1
INSERT INTO [Member_Coupons] ([Member_Id], [Coupon_Id], [Status], [Assigned_At], [Verification_Code], [Updated_At])
VALUES (1, 2, 'active', GETDATE(), 'TEST002', GETDATE());
PRINT '✅ 分配優惠券 2 給測試用戶';

-- 11. 清理舊的測試購物車 (重新開始測試)
DELETE FROM [Cart_Items] WHERE [Cart_Id] IN (SELECT [Id] FROM [Carts] WHERE [Member_Id] = 1);
DELETE FROM [Carts] WHERE [Member_Id] = 1;
PRINT '✅ 清理舊的測試購物車';

-- 12. 驗證資料 (使用正確的表名)
PRINT '';
PRINT '📋 測試資料驗證：';
PRINT '  ▸ 用戶: ' + CAST((SELECT COUNT(*) FROM [Members] WHERE [Id] = 1) AS VARCHAR(10)) + ' 筆';
PRINT '  ▸ 商品: ' + CAST((SELECT COUNT(*) FROM [Products] WHERE [Id] = 1) AS VARCHAR(10)) + ' 筆';
PRINT '  ▸ 優惠券: ' + CAST((SELECT COUNT(*) FROM [Coupons] WHERE [Id] IN (1,2)) AS VARCHAR(10)) + ' 筆';
PRINT '  ▸ 用戶優惠券: ' + CAST((SELECT COUNT(*) FROM [Member_Coupons] WHERE [Member_Id] = 1) AS VARCHAR(10)) + ' 筆';

-- 13. 檢查實際的優惠券資料
PRINT '';
PRINT '🔍 實際優惠券資料：';
SELECT 'Coupons in database:' AS [Info];
SELECT Id, Title, Discount_Type, Discount_Amount, Min_Spend, Is_Active, Start_At, Expired_At
FROM Coupons 
WHERE Id IN (1, 2)
ORDER BY Id;

-- 14. 檢查用戶優惠券關聯
PRINT '';
PRINT '🔗 用戶優惠券關聯：';
SELECT 'Member_Coupons relationships:' AS [Info];
SELECT mc.Member_Id, mc.Coupon_Id, mc.Status, c.Title, c.Discount_Type, c.Discount_Amount, c.Min_Spend
FROM Member_Coupons mc
JOIN Coupons c ON mc.Coupon_Id = c.Id
WHERE mc.Member_Id = 1;

-- 15. 顯示完成訊息
PRINT '';
PRINT '🎉 Swagger 測試資料建立完成！';
PRINT '';
PRINT '📋 測試帳號資訊：';
PRINT '   👤 用戶ID: 1 (test@example.com)';
PRINT '   📦 商品ID: 1 (測試商品 - $500)';
PRINT '   🔧 屬性值ID: 1 (標準款)';
PRINT '   🎫 優惠券ID: 1, 2';
PRINT '';
PRINT '🧪 可以開始在 Swagger 中測試了！';
PRINT '   Swagger URL: https://localhost:7106/swagger/index.html';
PRINT '';

-- 16. 顯示測試建議
PRINT '💡 測試建議：';
PRINT '   1. 先加入 2 個商品 (總額 $1000) → 可用 10% 折扣券';
PRINT '   2. 先加入 1 個商品 (總額 $500) → 金額不足，無法使用優惠券';
PRINT '   3. 先加入 1 個商品，再加入 1 個 → 總額 $1000，可用兩種優惠券';
PRINT '';

-- 17. 顯示下一步驗證查詢
PRINT '🔍 驗證查詢（可在 SSMS 中執行）：';
PRINT '';
PRINT '-- 檢查用戶是否有優惠券';
PRINT 'SELECT mc.*, c.Title, c.Discount_Type, c.Discount_Amount, c.Min_Spend';
PRINT 'FROM Member_Coupons mc';
PRINT 'JOIN Coupons c ON mc.Coupon_Id = c.Id';
PRINT 'WHERE mc.Member_Id = 1;';
PRINT '';