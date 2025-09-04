-- 新增 IsActive 欄位到 Carriers 表
-- 執行時間：約 30 秒

-- Step 1: 新增 IsActive 欄位，預設為 1 (啟用)
ALTER TABLE Carriers 
ADD IsActive BIT NOT NULL DEFAULT 1;

-- Step 2: 確認所有現有物流商都設為啟用狀態
UPDATE Carriers 
SET IsActive = 1 
WHERE IsActive IS NULL;

-- Step 3: 新增索引以提升查詢效能
CREATE INDEX IX_Carriers_IsActive ON Carriers(IsActive);

-- Step 4: 驗證資料
SELECT Id, Name, Contact, IsActive, CreatedAt 
FROM Carriers 
ORDER BY Id;

-- 預期結果：所有物流商的 IsActive 都應該是 1
PRINT '✅ IsActive 欄位新增完成，所有現有物流商已設為啟用狀態';