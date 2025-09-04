# 結帳流程 API 測試指南

## 📋 概述

本文件說明了完整的結帳流程API使用方式，包含從購物車驗證到訂單完成的所有步驟。

## 🔧 API 端點列表

### 1. 結帳前驗證

#### 驗證購物車狀態
```http
POST /api/Checkout/validate/{memberId}
```

**回應範例：**
```json
{
  "success": true,
  "message": "結帳驗證通過",
  "data": {
    "isValid": true,
    "errors": [],
    "summary": {
      "itemCount": 2,
      "subtotalAmount": 1299.00,
      "shippingFee": 0,
      "discountAmount": 0,
      "pointsDeductAmount": 0,
      "totalAmount": 1299.00,
      "freeShipping": true,
      "availablePoints": 500,
      "maxPointsDeduction": 389.70
    }
  }
}
```

#### 取得結帳摘要
```http
GET /api/Checkout/summary/{memberId}?couponCode=SUMMER2024&usedPoints=100
```

### 2. 配送與付款選項

#### 取得配送方式
```http
GET /api/Checkout/delivery-methods/{memberId}
```

**回應範例：**
```json
{
  "success": true,
  "data": [
    {
      "method": "standard",
      "name": "標準配送",
      "fee": 60,
      "description": "3-5個工作天送達",
      "isAvailable": true,
      "estimatedDays": 4
    },
    {
      "method": "express",
      "name": "快速配送",
      "fee": 120,
      "description": "1-2個工作天送達",
      "isAvailable": true,
      "estimatedDays": 1
    }
  ]
}
```

#### 取得付款方式
```http
GET /api/Checkout/payment-methods/{memberId}
```

#### 計算運費
```http
GET /api/Checkout/shipping-fee/{memberId}?deliveryMethod=standard
```

### 3. 優惠券與點數

#### 驗證優惠券
```http
POST /api/Checkout/validate-coupon/{memberId}
Content-Type: application/json

"SUMMER2024"
```

#### 取得可用點數
```http
GET /api/Checkout/available-points/{memberId}
```

#### 計算最大點數抵扣
```http
GET /api/Checkout/max-points-deduction/{memberId}?subtotal=1299.00
```

### 4. 建立訂單

#### 完整結帳
```http
POST /api/Checkout/create-order
Content-Type: application/json

{
  "memberId": 1,
  "recipientName": "王小明",
  "phoneNumber": "0912345678",
  "city": "台北市",
  "district": "中正區",
  "addressDetail": "中山南路1號",
  "deliveryMethod": "standard",
  "paymentMethod": "credit_card",
  "couponCode": "SUMMER2024",
  "usedPoints": 100,
  "note": "請在平日送達"
}
```

**成功回應：**
```json
{
  "success": true,
  "message": "訂單建立成功",
  "data": {
    "orderId": 12345,
    "orderNumber": "00012345",
    "totalAmount": 1199.00,
    "orderStatus": "pending",
    "paymentStatus": "pending",
    "createdAt": "2024-01-15T10:30:00",
    "paymentInfo": null
  }
}
```

#### 快速結帳（立即購買）
```http
POST /api/Checkout/quick-checkout
Content-Type: application/json

{
  "memberId": 1,
  "productId": 123,
  "attributeValueId": 456,
  "quantity": 1,
  "deliveryInfo": {
    "memberId": 1,
    "recipientName": "王小明",
    "phoneNumber": "0912345678",
    "city": "台北市",
    "district": "中正區",
    "addressDetail": "中山南路1號",
    "deliveryMethod": "express",
    "paymentMethod": "linepay"
  }
}
```

### 5. 訂單確認

#### 取得訂單確認資訊
```http
GET /api/Checkout/order-confirmation?orderId=12345&memberId=1
```

**回應範例：**
```json
{
  "success": true,
  "data": {
    "orderId": 12345,
    "orderNumber": "00012345",
    "memberId": 1,
    "memberEmail": "user@example.com",
    "recipientName": "王小明",
    "phoneNumber": "0912345678",
    "deliveryAddress": "台北市中正區中山南路1號",
    "deliveryMethod": "standard",
    "subtotalAmount": 1299.00,
    "shippingFee": 0,
    "discountAmount": 100.00,
    "pointsDeductAmount": 100.00,
    "totalAmount": 1099.00,
    "paymentMethod": "credit_card",
    "paymentStatus": "pending",
    "orderStatus": "pending",
    "items": [
      {
        "orderDetailId": 1,
        "productId": 123,
        "productName": "iPhone 15 Pro",
        "productImage": "/images/products/iphone15pro.jpg",
        "attributeValueId": 456,
        "attributeName": "顏色",
        "attributeValue": "太空黑",
        "unitPrice": 39900.00,
        "quantity": 1,
        "subtotal": 39900.00
      }
    ],
    "couponCode": "SUMMER2024",
    "couponTitle": "夏日優惠券",
    "createdAt": "2024-01-15T10:30:00",
    "estimatedDeliveryDate": "2024-01-19T10:30:00"
  }
}
```

### 6. 付款處理

#### 處理付款
```http
POST /api/Checkout/process-payment/{orderId}
Content-Type: application/json

{
  "method": "credit_card",
  "cardNumber": "4111111111111111",
  "expiryMonth": "12",
  "expiryYear": "2025",
  "cvv": "123",
  "cardholderName": "王小明"
}
```

#### 確認付款完成
```http
POST /api/Checkout/confirm-payment?orderId=12345&transactionId=TXN123456789
```

## 🔄 完整結帳流程示例

### 步驟 1: 驗證購物車
```javascript
// 1. 先驗證購物車狀態
const validateResponse = await fetch('/api/Checkout/validate/1', {
  method: 'POST'
});
const validation = await validateResponse.json();

if (!validation.success || !validation.data.isValid) {
  console.error('購物車驗證失敗:', validation.data.errors);
  return;
}
```

### 步驟 2: 取得配送與付款選項
```javascript
// 2. 取得配送方式
const deliveryMethods = await fetch('/api/Checkout/delivery-methods/1')
  .then(res => res.json());

// 3. 取得付款方式
const paymentMethods = await fetch('/api/Checkout/payment-methods/1')
  .then(res => res.json());
```

### 步驟 3: 計算最終金額
```javascript
// 4. 如果有優惠券，先驗證
let couponCode = null;
if (userCouponCode) {
  const couponResponse = await fetch(`/api/Checkout/validate-coupon/1`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(userCouponCode)
  });
  const couponResult = await couponResponse.json();
  if (couponResult.success) {
    couponCode = userCouponCode;
  }
}

// 5. 取得最終結帳摘要
const summary = await fetch(
  `/api/Checkout/summary/1?couponCode=${couponCode}&usedPoints=100`
).then(res => res.json());
```

### 步驟 4: 建立訂單
```javascript
// 6. 建立訂單
const orderRequest = {
  memberId: 1,
  recipientName: "王小明",
  phoneNumber: "0912345678",
  city: "台北市",
  district: "中正區", 
  addressDetail: "中山南路1號",
  deliveryMethod: "standard",
  paymentMethod: "credit_card",
  couponCode: couponCode,
  usedPoints: 100
};

const orderResponse = await fetch('/api/Checkout/create-order', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(orderRequest)
});

const orderResult = await orderResponse.json();
if (!orderResult.success) {
  console.error('建立訂單失敗:', orderResult.message);
  return;
}

const orderId = orderResult.data.orderId;
```

### 步驟 5: 處理付款
```javascript
// 7. 處理付款
const paymentData = {
  method: "credit_card",
  cardNumber: "4111111111111111",
  expiryMonth: "12", 
  expiryYear: "2025",
  cvv: "123",
  cardholderName: "王小明"
};

const paymentResponse = await fetch(`/api/Checkout/process-payment/${orderId}`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(paymentData)
});

const paymentResult = await paymentResponse.json();
if (paymentResult.success) {
  // 8. 確認付款
  await fetch(`/api/Checkout/confirm-payment?orderId=${orderId}&transactionId=${paymentResult.data.transactionId}`, {
    method: 'POST'
  });
  
  // 9. 顯示訂單確認頁面
  window.location.href = `/order-confirmation?orderId=${orderId}`;
}
```

## 📝 錯誤處理

### 常見錯誤代碼

| 錯誤類型 | 錯誤訊息 | 處理方式 |
|---------|---------|---------|
| MEMBER_NOT_FOUND | 會員不存在 | 重新登入 |
| EMPTY_CART | 購物車為空 | 重新添加商品 |
| PRODUCT_UNAVAILABLE | 商品已下架 | 移除該商品 |
| INSUFFICIENT_STOCK | 庫存不足 | 調整數量或移除 |
| INVALID_COUPON | 優惠券無效 | 移除優惠券 |
| INSUFFICIENT_POINTS | 點數不足 | 調整使用點數 |

### 錯誤處理範例
```javascript
async function handleCheckoutError(error) {
  switch (error.type) {
    case 'INSUFFICIENT_STOCK':
      alert(`商品 ${error.data.productName} 庫存不足，僅剩 ${error.data.availableStock} 件`);
      // 更新購物車數量
      break;
    case 'INVALID_COUPON':
      alert('優惠券已過期或無法使用');
      // 移除優惠券
      break;
    case 'INSUFFICIENT_POINTS':
      alert('點數不足，請調整使用點數');
      // 調整點數使用量
      break;
    default:
      alert('系統錯誤，請稍後再試');
  }
}
```

## 🎯 最佳實踐

1. **結帳前必須驗證**：每次進入結帳頁面都要驗證購物車狀態
2. **即時計算金額**：當用戶選擇不同配送方式、優惠券或點數時，即時更新總金額
3. **庫存檢查**：在建立訂單前再次檢查庫存狀態
4. **錯誤處理**：提供友善的錯誤訊息和處理建議
5. **交易安全**：付款資訊需要加密傳輸
6. **訂單追蹤**：提供訂單狀態查詢功能

## 📊 狀態說明

### 訂單狀態 (OrderStatus)
- `pending`: 待處理
- `confirmed`: 已確認
- `processing`: 處理中
- `shipped`: 已出貨
- `delivered`: 已送達
- `completed`: 已完成
- `cancelled`: 已取消
- `returned`: 已退貨

### 付款狀態 (PaymentStatus)
- `pending`: 待付款
- `processing`: 處理中
- `completed`: 已完成
- `failed`: 失敗
- `cancelled`: 已取消
- `refunded`: 已退款

## 🔗 相關 API

- [購物車 API](/api/Carts) - 管理購物車商品
- [優惠券 API](/api/Coupons) - 查詢可用優惠券
- [會員地址 API](/api/MemberAddresses) - 管理收件地址
- [訂單 API](/api/Orders) - 查詢訂單狀態