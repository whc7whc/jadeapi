-- 建立付款記錄表
CREATE TABLE PaymentRecords (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    MerchantTradeNo VARCHAR(20) UNIQUE NOT NULL,
    TradeNo VARCHAR(20) NULL,
    TradeAmt INT NOT NULL,
    RtnCode INT NOT NULL DEFAULT 0,
    RtnMsg NVARCHAR(200) NULL,
    PaymentType NVARCHAR(50) NULL,
    PaymentDate DATETIME2 NULL,
    PaymentTypeChargeFee DECIMAL(10,2) NULL,
    TradeDate DATETIME2 NULL,
    SimulatePaid BIT NULL,
    OrderId INT NULL,
    MemberId INT NULL,
    RawReturn NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- 建立索引
CREATE INDEX IX_PaymentRecords_MerchantTradeNo ON PaymentRecords(MerchantTradeNo);
CREATE INDEX IX_PaymentRecords_OrderId ON PaymentRecords(OrderId);
CREATE INDEX IX_PaymentRecords_MemberId ON PaymentRecords(MemberId);
CREATE INDEX IX_PaymentRecords_RtnCode ON PaymentRecords(RtnCode);
