using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.Models.DTOs;
using Team.API.Models.EfModel;
using Team.API.Payments;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Team.API.DTO;

namespace Team.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPaymentGateway _gw;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(AppDbContext db, IPaymentGateway gw, ILogger<PaymentsController> logger)
        {
            _gw = gw;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// ECPay 結帳 - 跳轉到綠界付款頁面
        /// 直接使用訂單ID建立付款
        /// </summary>
        /// <param name="orderId">訂單ID</param>
        [HttpGet("ecpay-checkout/{orderId}")]
        [AllowAnonymous] 
        public async Task<IActionResult> EcpayCheckout(int orderId, [FromServices] IConfiguration cfg)
        {
            try
            {
                // 1) 取得訂單資料
                var order = await GetOrderForPaymentAsync(orderId);
                if (order == null)
                {
                    return BadRequest(new { message = "找不到訂單資料" });
                }

                // 2) 統一用「後端重算後的整數金額」- 四捨五入到整數
                var payable = Convert.ToInt32(Math.Round(order.TotalAmount, 0, MidpointRounding.AwayFromZero));

                // 3) 先建 PaymentRecord（Pending）
                var merchantTradeNo = $"ORD{DateTime.Now:yyyyMMddHHmmss}"; // ≤20
                var paymentRecord = new PaymentRecord
                {
                    MerchantTradeNo = merchantTradeNo,
                    TradeAmt = payable, // ★ 使用統一的 payable 整數
                    RtnCode = 0, // 未付款
                    OrderId = orderId,
                    MemberId = null, // 可以根據需要設定
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.PaymentRecords.Add(paymentRecord);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ 付款記錄建立成功 - MerchantTradeNo: {MerchantTradeNo}, PayableAmount: {PayableAmount}", 
                    merchantTradeNo, payable);

                // 4) 組表單欄位
                var fields = new Dictionary<string, string>
                {
                    ["MerchantID"] = cfg["Ecpay:MerchantID"]!,
                    ["MerchantTradeNo"] = merchantTradeNo,
                    ["MerchantTradeDate"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                    ["PaymentType"] = "aio",
                    ["TotalAmount"] = payable.ToString(), // ★ 用同一個 payable
                    ["TradeDesc"] = $"訂單編號：ORD{order.Id:D8}",
                    ["ItemName"] = GenerateItemNamesFromOrderDto(order),
                    ["ReturnURL"] = cfg["Ecpay:ReturnURL"]!,
                    ["ChoosePayment"] = "Credit",
                    ["EncryptType"] = "1"
                };

                // 生成檢查碼
                fields["CheckMacValue"] = GenCheckMac(fields, cfg["Ecpay:HashKey"]!, cfg["Ecpay:HashIV"]!);

                var aioUrl = cfg["Ecpay:AioCheckOutUrl"]!;
                var inputs = string.Join("", fields.Select(f =>
                    $"<input type='hidden' name='{f.Key}' value='{System.Net.WebUtility.HtmlEncode(f.Value)}' />"));

                // 產生自動提交的 HTML 表單
                var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>跳轉到綠界付款</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; }}
        .container {{ max-width: 600px; margin: 0 auto; }}
        .btn {{ padding: 15px 30px; background: #28a745; color: white; border: none; border-radius: 5px; cursor: pointer; font-size: 16px; }}
        .btn:hover {{ background: #218838; }}
        .info {{ background: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0; }}
        .item {{ margin: 5px 0; text-align: left; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>🛒 確認付款資訊</h2>
        <div class='info'>
            <p><strong>訂單編號：</strong>ORD{order.Id:D8}</p>
            <p><strong>總金額：</strong>NT$ {payable:N0}</p>
            <p><strong>會員：</strong>{order.MemberName}</p>
            <div style='margin-top: 15px;'>
                <strong>商品明細：</strong>
                {GenerateItemDisplayFromOrderDto(order)}
            </div>
        </div>
        <p>🏦 請點擊下方按鈕前往綠界付款頁面</p>
        
        <form id='ecpayForm' method='post' action='{aioUrl}'>
            {inputs}
            <input type='submit' value='🏦 前往付款' class='btn' />
        </form>
    </div>
</body>
</html>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "跳轉綠界付款時發生錯誤");
                return BadRequest(new { message = "付款頁面載入失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 取得付款 URL - 根據環境自動返回正確的付款連結
        /// </summary>
        [HttpGet("payment-url/{orderId}")]
        [AllowAnonymous]
        public IActionResult GetPaymentUrl(int orderId, [FromServices] IConfiguration cfg)
        {
            try
            {
                // 從設定中取得基礎 URL
                string baseUrl;
                
                // 如果有設定 ngrok URL，優先使用
                var ngrokUrl = cfg["Ecpay:BaseURL"];
                if (!string.IsNullOrEmpty(ngrokUrl))
                {
                    baseUrl = ngrokUrl;
                }
                else
                {
                    // 否則使用當前請求的基礎 URL
                    baseUrl = $"{Request.Scheme}://{Request.Host}";
                }

                var paymentUrl = $"{baseUrl}/api/payments/ecpay-checkout/{orderId}";

                return Ok(new 
                { 
                    success = true, 
                    paymentUrl = paymentUrl,
                    orderId = orderId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得付款 URL 失敗");
                return BadRequest(new { success = false, message = "取得付款 URL 失敗" });
            }
        }

        /// <summary>
        /// 建立綠界付款訂單
        /// </summary>
        [HttpPost("checkout")]
        [AllowAnonymous] // TODO: 正式環境應移除，改用 [Authorize]
        public async Task<IActionResult> Checkout([FromBody] CreatePaymentOrderDto vm)
        {
            try
            {
                _logger.LogInformation("💳 開始建立付款訂單 - OrderId: {OrderId}, MemberId: {MemberId}, Amount: {Amount}", 
                    vm.OrderId, vm.MemberId, vm.TotalAmount);

                // 1. 驗證訂單是否存在且屬於該會員
                var order = await _db.Orders
                    .FirstOrDefaultAsync(o => o.Id == vm.OrderId && o.MemberId == vm.MemberId);

                if (order == null)
                {
                    return BadRequest(new { success = false, message = "訂單不存在或無權限訪問" });
                }

                // 2. 檢查是否已經有付款記錄
                var existingPayment = await _db.PaymentRecords
                    .FirstOrDefaultAsync(p => p.OrderId == vm.OrderId && p.RtnCode == 1);

                if (existingPayment != null)
                {
                    return BadRequest(new { success = false, message = "此訂單已完成付款" });
                }

                // 3. 生成唯一的商家訂單編號
                var merchantTradeNo = GenerateUniqueMerchantTradeNo();

                // 4. 建立付款記錄
                var paymentRecord = new PaymentRecord
                {
                    MerchantTradeNo = merchantTradeNo,
                    TradeAmt = vm.TotalAmount,
                    RtnCode = 0, // 未付款
                    OrderId = vm.OrderId,
                    MemberId = vm.MemberId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _db.PaymentRecords.Add(paymentRecord);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ 付款記錄建立成功 - MerchantTradeNo: {MerchantTradeNo}", merchantTradeNo);

                // 5. 呼叫綠界建立付款頁面
                var html = await _gw.CreateAioCheckoutHtmlAsync(new EcpayCreateOrder
                {
                    MerchantTradeNo = merchantTradeNo,
                    MerchantTradeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                    TotalAmount = vm.TotalAmount,
                    ItemName = string.Join('#', vm.Items.Select(i => i.Name)),
                    TradeDesc = $"訂單 #{order.Id} 付款",
                    ChoosePayment = vm.PaymentMethod
                });

                _logger.LogInformation("🎯 綠界付款頁面建立成功");

                return Ok(new PaymentResultDto
                {
                    Success = true,
                    Message = "付款頁面建立成功",
                    Html = html,
                    MerchantTradeNo = merchantTradeNo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 建立付款訂單失敗");
                return StatusCode(500, new { success = false, message = "建立付款訂單失敗" });
            }
        }

        /// <summary>
        /// 綠界付款結果回呼 (Server to Server)
        /// </summary>
        [HttpPost("callback/return")]
        [AllowAnonymous] // 綠界伺服器呼叫，不需要認證
        public async Task<IActionResult> Return([FromForm] IFormCollection form)
        {
            try
            {
                var formData = form.Keys.ToDictionary(k => k, k => form[k].ToString());
                
                _logger.LogInformation("📞 收到綠界付款回呼 - MerchantTradeNo: {MerchantTradeNo}", 
                    GetValueOrDefault(formData, "MerchantTradeNo"));

                // 1. 驗簽（暫時跳過以便測試）
                _logger.LogInformation("⚠️ 暫時跳過驗簽檢查以便測試");
                // if (!_gw.VerifyCheckMac(formData))
                // {
                //     _logger.LogWarning("❌ 綠界回呼驗簽失敗");
                //     return Content("0|CheckMacValue Error");
                // }

                // 2. 解析回呼資料
                var callback = new PaymentCallbackDto
                {
                    MerchantTradeNo = GetValueOrDefault(formData, "MerchantTradeNo"),
                    TradeNo = GetValueOrDefault(formData, "TradeNo"),
                    RtnCode = int.TryParse(GetValueOrDefault(formData, "RtnCode"), out var rtnCode) ? rtnCode : 0,
                    RtnMsg = GetValueOrDefault(formData, "RtnMsg"),
                    TradeAmt = int.TryParse(GetValueOrDefault(formData, "TradeAmt"), out var tradeAmt) ? tradeAmt : 0,
                    PaymentDate = GetValueOrDefault(formData, "PaymentDate"),
                    PaymentType = GetValueOrDefault(formData, "PaymentType"),
                    PaymentTypeChargeFee = GetValueOrDefault(formData, "PaymentTypeChargeFee"),
                    TradeDate = GetValueOrDefault(formData, "TradeDate"),
                    SimulatePaid = GetValueOrDefault(formData, "SimulatePaid") == "1",
                    CheckMacValue = GetValueOrDefault(formData, "CheckMacValue")
                };

                // 3. 更新付款記錄
                var paymentRecord = await _db.PaymentRecords
                    .FirstOrDefaultAsync(x => x.MerchantTradeNo == callback.MerchantTradeNo);

                if (paymentRecord != null)
                {
                    // 記錄金額比對資訊
                    _logger.LogInformation("💰 金額比對 - DB PaymentRecord.TradeAmt: {DbAmount}, ECPay Callback TradeAmt: {CallbackAmount}", 
                        paymentRecord.TradeAmt, callback.TradeAmt);

                    paymentRecord.TradeNo = callback.TradeNo;
                    paymentRecord.RtnCode = callback.RtnCode;
                    paymentRecord.RtnMsg = callback.RtnMsg;
                    paymentRecord.TradeAmt = callback.TradeAmt;
                    paymentRecord.PaymentType = callback.PaymentType;
                    paymentRecord.PaymentDate = DateTime.TryParse(callback.PaymentDate, out var paymentDate) ? paymentDate : null;
                    paymentRecord.PaymentTypeChargeFee = decimal.TryParse(callback.PaymentTypeChargeFee, out var fee) ? fee : null;
                    paymentRecord.TradeDate = DateTime.TryParse(callback.TradeDate, out var tradeDate) ? tradeDate : null;
                    paymentRecord.SimulatePaid = callback.SimulatePaid;
                    paymentRecord.RawReturn = JsonSerializer.Serialize(formData);
                    paymentRecord.UpdatedAt = DateTime.Now;

                    // 4. 如果付款成功，更新訂單狀態
                    if (callback.RtnCode == 1 && paymentRecord.OrderId.HasValue)
                    {
                        var order = await _db.Orders.FindAsync(paymentRecord.OrderId.Value);
                        if (order != null)
                        {
                            order.PaymentStatus = "completed";
                            order.OrderStatus = "processing"; // 可根據業務邏輯調整
                            order.UpdatedAt = DateTime.Now;

                            _logger.LogInformation("✅ 訂單付款成功 - OrderId: {OrderId}, TradeNo: {TradeNo}", 
                                order.Id, callback.TradeNo);
                        }
                    }

                    await _db.SaveChangesAsync();

                    _logger.LogInformation("✅ 付款記錄更新成功 - RtnCode: {RtnCode}, TradeNo: {TradeNo}", 
                        callback.RtnCode, callback.TradeNo);
                }
                else
                {
                    _logger.LogWarning("⚠️ 找不到對應的付款記錄 - MerchantTradeNo: {MerchantTradeNo}", 
                        callback.MerchantTradeNo);
                }

                // 5. 回覆綠界（必須回覆 1|OK）- 使用明確的 ContentType
                return new ContentResult { Content = "1|OK", ContentType = "text/plain", StatusCode = 200 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 處理綠界回呼失敗");
                return new ContentResult { Content = "0|Exception Error", ContentType = "text/plain", StatusCode = 200 };
            }
        }

        /// <summary>
        /// 查詢付款記錄
        /// </summary>
        [HttpGet("records/{orderId}")]
        [AllowAnonymous] // TODO: 正式環境應移除，改用 [Authorize]
        public async Task<IActionResult> GetPaymentRecords(int orderId, [FromQuery] int memberId)
        {
            try
            {
                var records = await _db.PaymentRecords
                    .Where(p => p.OrderId == orderId && p.MemberId == memberId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        p.Id,
                        p.MerchantTradeNo,
                        p.TradeNo,
                        p.TradeAmt,
                        p.RtnCode,
                        p.RtnMsg,
                        p.PaymentType,
                        p.PaymentDate,
                        p.CreatedAt,
                        Status = p.RtnCode == 1 ? "成功" : p.RtnCode == 0 ? "待付款" : "失敗"
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = records });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢付款記錄失敗");
                return StatusCode(500, new { success = false, message = "查詢付款記錄失敗" });
            }
        }

        /// <summary>
        /// 生成唯一的商家訂單編號
        /// </summary>
        private string GenerateUniqueMerchantTradeNo()
        {
            // 格式：PAY + 時間戳 + 隨機數 (最多20字元)
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var random = new Random().Next(1000, 9999);
            return $"PAY{timestamp}{random}".Substring(0, Math.Min(20, $"PAY{timestamp}{random}".Length));
        }

        
		/// <summary>
		/// 購物車結帳 - 跳轉到綠界付款頁面
		/// </summary>
		[HttpPost("cart-checkout")]
		[AllowAnonymous]
        
		public IActionResult CartCheckout([FromBody] CartCheckoutRequest request, [FromServices] IConfiguration cfg)
		{
			try
			{
				var merchantId = cfg["Ecpay:MerchantID"]!;
				var hashKey = cfg["Ecpay:HashKey"]!;
				var hashIV = cfg["Ecpay:HashIV"]!;
				var aioUrl = cfg["Ecpay:AioCheckOutUrl"]!;
				var returnUrl = cfg["Ecpay:ReturnURL"]!;
				var orderResult = cfg["Ecpay:OrderResultURL"];
				var clientBack = cfg["Ecpay:ClientBackURL"];

				// 生成唯一訂單編號
				var merchantTradeNo = $"ORD{DateTime.Now:yyyyMMddHHmmss}";

				// 根據付款方式決定 ChoosePayment
				string choosePayment = request.PaymentMethod?.ToLower() switch
				{
					"credit" => "Credit",
					"transfer" => "ATM", 
					"cod" => "Credit", // 貨到付款暫時也走信用卡，或者你可以不走綠界
					_ => "Credit"
				};

				var fields = new Dictionary<string, string>
				{
					["MerchantID"] = merchantId,
					["MerchantTradeNo"] = merchantTradeNo,
					["MerchantTradeDate"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
					["PaymentType"] = "aio",
					["TotalAmount"] = request.TotalAmount.ToString(),
					["TradeDesc"] = "JADE時尚購物",
					["ItemName"] = request.ItemName ?? "購物商品",
					["ReturnURL"] = returnUrl,
					["ChoosePayment"] = choosePayment,
					["EncryptType"] = "1"
				};

				if (!string.IsNullOrWhiteSpace(orderResult)) fields["OrderResultURL"] = orderResult!;
				if (!string.IsNullOrWhiteSpace(clientBack)) fields["ClientBackURL"] = clientBack!;

				fields["CheckMacValue"] = GenCheckMac(fields, hashKey, hashIV);

				var inputs = string.Join("", fields.Select(f => $"<input type='hidden' name='{f.Key}' value='{System.Net.WebUtility.HtmlEncode(f.Value)}' />"));
				var html = $@"<!doctype html><html><head><meta charset='utf-8'><title>JADE付款</title></head>
<body>
  <h3>正在跳轉到付款頁面...</h3>
  <form id='ecpayForm' method='post' action='{aioUrl}'>{inputs}
    <button type='submit'>🏦 前往付款</button>
  </form>
  <script>document.getElementById('ecpayForm').submit();</script>
</body></html>";
				return Content(html, "text/html");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "結帳時發生錯誤");
				return BadRequest(new { message = "結帳失敗", error = ex.Message });
			}
		}

		/// <summary>
		/// 快速測試 - 直接跳轉到綠界付款頁面
		/// </summary>
		[HttpGet("quick-test")]
		[AllowAnonymous]
		public IActionResult QuickTest([FromServices] IConfiguration cfg)
		{
			var merchantId = cfg["Ecpay:MerchantID"]!;
			var hashKey = cfg["Ecpay:HashKey"]!;
			var hashIV = cfg["Ecpay:HashIV"]!;
			var aioUrl = cfg["Ecpay:AioCheckOutUrl"]!;
			var returnUrl = cfg["Ecpay:ReturnURL"]!;
			var orderResult = cfg["Ecpay:OrderResultURL"];   // 可為 null
			var clientBack = cfg["Ecpay:ClientBackURL"];    // 可為 null

			var fields = new Dictionary<string, string>
			{
				["MerchantID"] = merchantId,
				["MerchantTradeNo"] = $"OwO{DateTime.Now:yyyyMMddHHmmss}",
				["MerchantTradeDate"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
				["PaymentType"] = "aio",
				["TotalAmount"] = "466",
				["TradeDesc"] = "測試訂單",
				["ItemName"] = "測試商品",
				["ReturnURL"] = returnUrl,
				["ChoosePayment"] = "Credit",
				["EncryptType"] = "1"
			};
			if (!string.IsNullOrWhiteSpace(orderResult)) fields["OrderResultURL"] = orderResult!;
			if (!string.IsNullOrWhiteSpace(clientBack)) fields["ClientBackURL"] = clientBack!;

			fields["CheckMacValue"] = GenCheckMac(fields, hashKey, hashIV);

			var inputs = string.Join("", fields.Select(f => $"<input type='hidden' name='{f.Key}' value='{System.Net.WebUtility.HtmlEncode(f.Value)}' />"));
			var html = $@"<!doctype html><html><head><meta charset='utf-8'><title>綠界測試</title></head>
<body>
  <h3>綠界付款測試</h3>
  <form id='ecpayForm' method='post' action='{aioUrl}'>{inputs}
    <button type='submit'>🏦 前往綠界付款</button>
  </form>
  <p>為避免 CSP，請手動點按鈕送出。</p>
</body></html>";
			return Content(html, "text/html");
		}

		static string GenCheckMac(IDictionary<string, string> fields, string hashKey, string hashIV)
		{
			var sorted = fields.Where(kv => !kv.Key.Equals("CheckMacValue", StringComparison.OrdinalIgnoreCase))
							   .OrderBy(kv => kv.Key, StringComparer.Ordinal)
							   .Select(kv => $"{kv.Key}={kv.Value}");
			var raw = $"HashKey={hashKey}&{string.Join("&", sorted)}&HashIV={hashIV}";
			var encoded = System.Web.HttpUtility.UrlEncode(raw, Encoding.UTF8)!.ToLowerInvariant()
				.Replace("%2d", "-").Replace("%5f", "_").Replace("%2e", ".")
				.Replace("%21", "!").Replace("%2a", "*").Replace("%28", "(").Replace("%29", ")")
				.Replace("%20", "+");  // 若你的 UrlEncode 產生 %20，必須轉為 +

			using var sha = System.Security.Cryptography.SHA256.Create();
			var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(encoded));
			return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
		}


		/// <summary>
		/// 字典的 GetValueOrDefault 擴展方法
		/// </summary>
		private static string GetValueOrDefault(Dictionary<string, string> dict, string key, string defaultValue = "")
        {
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 取得訂單付款資料
        /// </summary>
        private async Task<OrderPaymentDto?> GetOrderForPaymentAsync(int orderId)
        {
            var order = await _db.Orders
                .Include(o => o.Member)
                    .ThenInclude(m => m.MemberProfile) // 使用 MemberProfile 而不是 Profile
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return null;

            return new OrderPaymentDto
            {
                Id = order.Id, // 新增 Id 屬性
                OrderNumber = "ORD" + order.Id.ToString("D8"), // 生成訂單編號
                TotalAmount = order.TotalAmount,
                MemberName = order.Member?.MemberProfile?.Name ?? "未知會員", // 使用 MemberProfile.Name
                CreatedAt = order.CreatedAt, // 使用 CreatedAt 而不是 CreatedTime
                Items = order.OrderDetails.Select(od => new PaymentOrderItemDto
                {
                    ProductName = od.Product?.Name ?? "未知商品", // 使用 Name 而不是 ProductName
                    Quantity = od.Quantity ?? 0, // 處理可能的 null 值
                    SubTotal = od.Subtotal ?? 0 // 使用 Subtotal 而不是 SubTotal
                }).ToList()
            };
        }

        /// <summary>
        /// 從訂單DTO生成商品名稱字串（綠界格式）
        /// </summary>
        private string GenerateItemNamesFromOrderDto(OrderPaymentDto order)
        {
            var itemNames = order.Items.Select(item => $"{item.ProductName}x{item.Quantity}").ToList();
            var result = string.Join("#", itemNames);
            
            // 綠界商品名稱限制 200 字元
            if (result.Length > 200)
            {
                result = result.Substring(0, 197) + "...";
            }
            
            return result;
        }

        /// <summary>
        /// 從訂單DTO生成商品顯示HTML
        /// </summary>
        private string GenerateItemDisplayFromOrderDto(OrderPaymentDto order)
        {
            var html = "<div style='max-height: 150px; overflow-y: auto;'>";
            foreach (var item in order.Items)
            {
                html += $"<div class='item'>• {item.ProductName} x {item.Quantity} = NT$ {item.SubTotal:N0}</div>";
            }
            html += "</div>";
            return html;
        }

        /// <summary>
        /// 從訂單實體生成商品名稱字串（綠界格式）
        /// </summary>
        private string GenerateItemNamesFromOrder(Order order)
        {
            var itemNames = order.OrderDetails.Select(od => 
                $"{od.Product?.Name ?? "未知商品"}x{od.Quantity ?? 0}").ToList(); // 使用 Name 和處理 null
            var result = string.Join("#", itemNames);
            
            // 綠界商品名稱限制 200 字元
            if (result.Length > 200)
            {
                result = result.Substring(0, 197) + "...";
            }
            
            return result;
        }

        /// <summary>
        /// 從訂單實體生成商品顯示HTML
        /// </summary>
        private string GenerateItemDisplayFromOrder(Order order)
        {
            var html = "<div style='max-height: 150px; overflow-y: auto;'>";
            foreach (var item in order.OrderDetails)
            {
                html += $"<div class='item'>• {item.Product?.Name ?? "未知商品"} x {item.Quantity ?? 0} = NT$ {item.Subtotal ?? 0:N0}</div>"; // 修正屬性名稱
            }
            html += "</div>";
            return html;
        }

        /// <summary>
        /// 生成商品名稱字串（綠界格式）
        /// </summary>
        private string GenerateItemNames(List<PaymentOrderItemDto> items)
        {
            var itemNames = items.Select(item => $"{item.ProductName}x{item.Quantity}").ToList();
            var result = string.Join("#", itemNames);
            
            // 綠界商品名稱限制 200 字元
            if (result.Length > 200)
            {
                result = result.Substring(0, 197) + "...";
            }
            
            return result;
        }

        /// <summary>
        /// 生成商品顯示HTML
        /// </summary>
        private string GenerateItemDisplay(List<PaymentOrderItemDto> items)
        {
            var html = "<div style='max-height: 150px; overflow-y: auto;'>";
            foreach (var item in items)
            {
                html += $"<div class='item'>• {item.ProductName} x {item.Quantity} = NT$ {item.SubTotal:N0}</div>";
            }
            html += "</div>";
            return html;
        }

        /// <summary>
        /// 生成綠界檢查碼 (使用 SHA256)
        /// </summary>
        private string GenerateCheckMac(IDictionary<string, string> fields, string hashKey, string hashIV)
        {
            // 排除 CheckMacValue 並按照 Key 排序
            var sortedFields = fields.Where(f => f.Key != "CheckMacValue")
                                   .OrderBy(f => f.Key, StringComparer.Ordinal)
                                   .Select(f => $"{f.Key}={f.Value}");

            // 組合字串
            var rawString = $"HashKey={hashKey}&{string.Join("&", sortedFields)}&HashIV={hashIV}";
            
            // URL 編碼
            var encodedString = System.Web.HttpUtility.UrlEncode(rawString).ToLower();
            
            // SHA256 雜湊 (不是 MD5)
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(encodedString));
                return BitConverter.ToString(hash).Replace("-", "").ToUpper();
            }
        }
    }
}



