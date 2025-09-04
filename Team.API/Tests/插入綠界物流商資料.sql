-- 🚚 插入綠界物流對應的物流商資料（不含假資料）

-- 清空現有資料（如果需要重新開始）
-- DELETE FROM Carriers;

-- 插入對應綠界物流的物流商（真實的物流商資料）
INSERT INTO Carriers (Name, Contact, CreatedAt) VALUES
('黑貓宅急便', '客服專線: 0800-200-777', GETDATE()),
('7-11 超商取貨', '客服專線: 0800-008-711', GETDATE()),
('全家便利商店', '客服專線: 0800-030-588', GETDATE());

-- 確認插入結果
SELECT Id, Name, Contact, CreatedAt FROM Carriers ORDER BY Id;

-- 查看物流商對應的綠界代碼
SELECT 
    Id,
    Name,
    CASE 
        WHEN Name LIKE '%黑貓%' THEN 'HOME_TCAT'
        WHEN Name LIKE '%7-11%' OR Name LIKE '%7-ELEVEN%' THEN 'UNIMART'
        WHEN Name LIKE '%全家%' THEN 'FAMI'
        ELSE 'UNKNOWN'
    END AS ECPayCode,
    Contact
FROM Carriers
ORDER BY Id;