-- 購物車 API 測試用 SQL 腳本 (根據實際 Snake_Case 資料表結構修正)
-- ?? 使用正確的表名和欄位名稱

-- ============ 檢查現有資料 ============

-- 1. 檢查會員資料 (Members 表 - 使用 Snake_Case 欄位)
SELECT TOP 5 Id, Email, Is_Active, Created_At FROM Members ORDER BY Id;

-- 2. 檢查商品資料 (Products 表 - 使用 Snake_Case 欄位)
SELECT TOP 5 Id, Name, Price, Is_Active, Is_Discount, Discount_Price FROM Products WHERE Is_Active = 1 ORDER BY Id;

-- 3. 檢查商品屬性值資料 (使用正確的表名和欄位名)
SELECT TOP 5 pav.Id, pav.Product_Id, pav.Stock, pav.Sku, av.Value, a.Name as AttributeName
FROM Product_Attribute_Values pav
JOIN Attribute_Values av ON pav.Attribute_Value_Id = av.Id
JOIN Attributes a ON av.Attribute_Id = a.Id
WHERE pav.Stock > 0
ORDER BY pav.Id;

-- 4. 檢查現有購物車 (Carts 表 - 使用 Snake_Case 欄位)
SELECT c.Id, c.Member_Id, c.Created_At, COUNT(ci.Id) as ItemCount
FROM Carts c
LEFT JOIN Cart_Items ci ON c.Id = ci.Cart_Id
GROUP BY c.Id, c.Member_Id, c.Created_At
ORDER BY c.Created_At DESC;

-- 5. 檢查購物車項目詳情 (Cart_Items 表 - 使用 Snake_Case)
SELECT ci.Id, ci.Cart_Id, ci.Product_Id, ci.Attribute_Value_Id, ci.Quantity, 
       ci.Price_At_Added, ci.Created_At, p.Name as ProductName
FROM Cart_Items ci
JOIN Products p ON ci.Product_Id = p.Id
ORDER BY ci.Created_At DESC;

-- ============ 建立測試資料區塊 ============

-- 建立測試會員 (使用正確的 Snake_Case 欄位名稱)
IF NOT EXISTS (SELECT 1 FROM Members WHERE Email = 'test@example.com')
BEGIN
    INSERT INTO Members (Email, Password_Hash, Registered_Via, Is_Email_Verified, Is_Active, Level, Role, Created_At, Updated_At) 
    VALUES ('test@example.com', 'hashedpassword123', 'manual', 1, 1, 1, 0, GETDATE(), GETDATE());
    PRINT '? 已建立測試會員: test@example.com';
END
ELSE
BEGIN
    PRINT '?? 測試會員已存在: test@example.com';
END

-- 建立測試商品 (使用正確的 Snake_Case 欄位名稱)
IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = '測試商品1')
BEGIN
    INSERT INTO Products (Name, Description, Price, Is_Discount, Discount_Price, Is_Active, Created_At, Updated_At) 
    VALUES 
    ('測試商品1', '這是測試用的商品1描述', 100, 0, NULL, 1, GETDATE(), GETDATE()),
    ('測試商品2', '這是測試用的商品2描述 - 有折扣', 200, 1, 180, 1, GETDATE(), GETDATE()),
    ('測試商品3', '這是測試用的商品3描述', 300, 0, NULL, 1, GETDATE(), GETDATE());
    PRINT '? 已建立測試商品 1-3';
END
ELSE
BEGIN
    PRINT '?? 測試商品已存在';
END

-- 建立測試屬性 (使用正確的 Attributes 表欄位)
IF NOT EXISTS (SELECT 1 FROM Attributes WHERE Name = '顏色')
BEGIN
    INSERT INTO Attributes (Name, Description, Is_Approved) 
    VALUES 
    ('顏色', '商品顏色屬性', 1),
    ('尺寸', '商品尺寸屬性', 1);
    PRINT '? 已建立測試屬性: 顏色, 尺寸';
END
ELSE
BEGIN
    PRINT '?? 測試屬性已存在';
END

-- 建立測試屬性值 (使用正確的 Attribute_Values 表和欄位名稱)
IF NOT EXISTS (SELECT 1 FROM Attribute_Values av 
               JOIN Attributes a ON av.Attribute_Id = a.Id 
               WHERE a.Name = '顏色' AND av.Value = '紅色')
BEGIN
    INSERT INTO Attribute_Values (Value, Attribute_Id) 
    SELECT '紅色', Id FROM Attributes WHERE Name = '顏色'
    UNION ALL
    SELECT '藍色', Id FROM Attributes WHERE Name = '顏色'
    UNION ALL
    SELECT '綠色', Id FROM Attributes WHERE Name = '顏色'
    UNION ALL
    SELECT 'S', Id FROM Attributes WHERE Name = '尺寸'
    UNION ALL
    SELECT 'M', Id FROM Attributes WHERE Name = '尺寸'
    UNION ALL
    SELECT 'L', Id FROM Attributes WHERE Name = '尺寸';
    PRINT '? 已建立測試屬性值';
END
ELSE
BEGIN
    PRINT '?? 測試屬性值已存在';
END

-- 建立商品屬性值組合 (使用正確的 Product_Attribute_Values 表和欄位名稱)
IF NOT EXISTS (SELECT 1 FROM Product_Attribute_Values pav 
               JOIN Products p ON pav.Product_Id = p.Id 
               WHERE p.Name LIKE '測試商品%')
BEGIN
    INSERT INTO Product_Attribute_Values (Product_Id, Attribute_Value_Id, Stock, Sku, Additional_Price, Created_At, Updated_At)
    SELECT p.Id, av.Id, 100, 
           CONCAT('SKU-', p.Id, '-', av.Id), 
           0, GETDATE(), GETDATE()
    FROM Products p
    CROSS JOIN Attribute_Values av
    WHERE p.Name LIKE '測試商品%';
    PRINT '? 已建立商品屬性值組合';
END
ELSE
BEGIN
    PRINT '?? 商品屬性值組合已存在';
END

-- ============ 資料驗證與顯示 ============

PRINT '';
PRINT '=== ?? 資料統計 ===';
DECLARE @MemberCount INT = (SELECT COUNT(*) FROM Members WHERE Is_Active = 1);
DECLARE @ProductCount INT = (SELECT COUNT(*) FROM Products WHERE Is_Active = 1);
DECLARE @AttributeCount INT = (SELECT COUNT(*) FROM Attributes WHERE Is_Approved = 1);
DECLARE @AttributeValueCount INT = (SELECT COUNT(*) FROM Attribute_Values);
DECLARE @ProductAttributeValueCount INT = (SELECT COUNT(*) FROM Product_Attribute_Values WHERE Stock > 0);

PRINT '會員數量: ' + CAST(@MemberCount AS VARCHAR(10));
PRINT '有效商品數量: ' + CAST(@ProductCount AS VARCHAR(10));
PRINT '屬性數量: ' + CAST(@AttributeCount AS VARCHAR(10));
PRINT '屬性值數量: ' + CAST(@AttributeValueCount AS VARCHAR(10));
PRINT '有庫存的商品屬性值組合: ' + CAST(@ProductAttributeValueCount AS VARCHAR(10));

PRINT '';
PRINT '=== ?? 購物車 API 測試用資料 ===';

-- 分別查詢每種資料類型，避免 UNION 造成的欄位名稱問題
PRINT '?? 測試會員:';
SELECT 
    Id,
    Email,
    CASE WHEN Is_Active = 1 THEN '? 啟用' ELSE '? 停用' END as Status
FROM Members 
WHERE Email = 'test@example.com';

PRINT '?? 測試商品:';
SELECT 
    Id,
    Name,
    Price,
    CASE WHEN Is_Discount = 1 THEN Discount_Price ELSE NULL END as DiscountPrice,
    CASE WHEN Is_Active = 1 THEN '? 上架' ELSE '? 下架' END as Status
FROM Products 
WHERE Name LIKE '測試商品%'
ORDER BY Id;

PRINT '??? 商品屬性值 (用於購物車測試):';
SELECT TOP 10
    pav.Id as AttributeValueId,
    p.Name as ProductName,
    a.Name as AttributeName,
    av.Value as AttributeValue,
    pav.Stock,
    CASE WHEN pav.Stock > 0 THEN '? 有庫存' ELSE '? 無庫存' END as StockStatus
FROM Product_Attribute_Values pav
JOIN Products p ON pav.Product_Id = p.Id
JOIN Attribute_Values av ON pav.Attribute_Value_Id = av.Id
JOIN Attributes a ON av.Attribute_Id = a.Id
WHERE p.Name LIKE '測試商品%'
ORDER BY p.Id, a.Name, av.Value;

PRINT '';
PRINT '=== ?? Swagger 測試建議參數 ===';
PRINT '';
PRINT '基於上述查詢結果，建議使用以下參數：';
PRINT '';
PRINT '?? 基本測試參數:';
PRINT '? 用戶ID (userId): 1';
PRINT '? 商品ID (productId): 1';  
PRINT '? 屬性值ID (attributeValueId): 1';
PRINT '? 數量 (quantity): 2';
PRINT '';
PRINT '?? 測試 URL: https://localhost:7000/swagger';
PRINT '';
PRINT '?? 建議測試順序:';
PRINT '1. GET /api/Carts/user/1 (取得空購物車)';
PRINT '2. POST /api/Carts/user/1/items (加入商品到購物車)';
PRINT '   Request Body:';
PRINT '   {';
PRINT '     "productId": 1,';
PRINT '     "attributeValueId": 1,';
PRINT '     "quantity": 2';
PRINT '   }';
PRINT '3. GET /api/Carts/user/1 (確認商品已加入)';
PRINT '4. PUT /api/Carts/user/1/items/{itemId} (更新數量)';
PRINT '5. DELETE /api/Carts/user/1/items/{itemId} (移除商品)';
PRINT '6. DELETE /api/Carts/user/1 (清空購物車)';

-- ============ 表名對照表 ============
PRINT '';
PRINT '=== ?? 表名對照表 (Entity Model vs 實際資料表) ===';
PRINT 'Entity Model → 實際資料表';
PRINT 'Members → Members (相同)';
PRINT 'Products → Products (相同)';
PRINT 'Attributes → Attributes (相同)';
PRINT 'AttributeValues → Attribute_Values';
PRINT 'ProductAttributeValues → Product_Attribute_Values';
PRINT 'Carts → Carts (相同)';
PRINT 'CartItems → Cart_Items';

-- ============ 欄位名對照表 ============
PRINT '';
PRINT '=== ?? 欄位名對照表 (Entity Model vs 實際資料表) ===';
PRINT 'Entity Model → 實際資料表';
PRINT 'MemberId → Member_Id';
PRINT 'CreatedAt → Created_At';
PRINT 'UpdatedAt → Updated_At';
PRINT 'IsActive → Is_Active';
PRINT 'IsDiscount → Is_Discount';
PRINT 'DiscountPrice → Discount_Price';
PRINT 'AttributeId → Attribute_Id';
PRINT 'AttributeValueId → Attribute_Value_Id';
PRINT 'ProductId → Product_Id';
PRINT 'CartId → Cart_Id';
PRINT 'PriceAtAdded → Price_At_Added';

-- 清理測試資料 (使用正確的表名和欄位名稱)
/*
PRINT '';
PRINT '=== ?? 清理測試資料 ===';

-- 清理購物車相關資料
DELETE FROM Cart_Items WHERE Cart_Id IN (
    SELECT Id FROM Carts WHERE Member_Id IN (
        SELECT Id FROM Members WHERE Email = 'test@example.com'
    )
);
DELETE FROM Carts WHERE Member_Id IN (
    SELECT Id FROM Members WHERE Email = 'test@example.com'
);

-- 清理商品相關資料
DELETE FROM Product_Attribute_Values WHERE Product_Id IN (
    SELECT Id FROM Products WHERE Name LIKE '測試商品%'
);
DELETE FROM Products WHERE Name LIKE '測試商品%';

-- 清理屬性相關資料
DELETE FROM Attribute_Values WHERE Attribute_Id IN (
    SELECT Id FROM Attributes WHERE Name IN ('顏色', '尺寸')
);
DELETE FROM Attributes WHERE Name IN ('顏色', '尺寸');

-- 清理會員資料
DELETE FROM Members WHERE Email = 'test@example.com';

PRINT '? 測試資料已清理完成';
*/

PRINT '';
PRINT '=== ? 腳本執行完成 ===';
PRINT '現在您可以使用上述參數在 Swagger 中測試購物車 API 了！';
PRINT '如果遇到問題，請檢查上方的測試資料是否正確建立。';