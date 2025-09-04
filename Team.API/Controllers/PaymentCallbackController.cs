using Microsoft.AspNetCore.Mvc;

namespace Team.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentCallbackController : ControllerBase
    {
        private readonly ILogger<PaymentCallbackController> _logger;

        public PaymentCallbackController(ILogger<PaymentCallbackController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// ECPay 付款完成後的返回頁面
        /// </summary>
        [HttpPost("ecpay/return")]
        public IActionResult EcpayReturn()
        {
            try
            {
                // 取得所有 POST 參數
                var allParams = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
                
                _logger.LogInformation("🎉 ECPay Return - 收到參數: {@Params}", allParams);

                // 檢查付款狀態
                if (allParams.TryGetValue("RtnCode", out var rtnCode) && rtnCode == "1")
                {
                    var merchantTradeNo = allParams.GetValueOrDefault("MerchantTradeNo", "");
                    var tradeAmt = allParams.GetValueOrDefault("TradeAmt", "");
                    var tradeDate = allParams.GetValueOrDefault("TradeDate", "");
                    
                    _logger.LogInformation("✅ 付款成功 - 訂單號: {OrderNo}, 金額: {Amount}, 時間: {Date}", 
                        merchantTradeNo, tradeAmt, tradeDate);

                    // 返回成功頁面的 HTML
                    var successHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <title>付款成功</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #f8f9fa; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 40px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .success {{ color: #28a745; font-size: 32px; margin-bottom: 20px; }}
        .info {{ background: #e7f5e7; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745; }}
        .btn {{ background: #007bff; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }}
        .params {{ background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; text-align: left; font-family: monospace; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='success'>🎉 付款成功！</div>
        <div class='info'>
            <h3>📋 交易詳情</h3>
            <p><strong>📝 訂單編號：</strong>{merchantTradeNo}</p>
            <p><strong>💰 付款金額：</strong>NT$ {tradeAmt}</p>
            <p><strong>📅 交易時間：</strong>{tradeDate}</p>
            <p><strong>🔢 回傳代碼：</strong>{rtnCode}</p>
        </div>
        
        <div class='params'>
            <strong>🔍 完整回傳參數：</strong><br>
            {string.Join("<br>", allParams.Select(p => $"{p.Key}: {p.Value}"))}
        </div>
        
        <a href='/api/payments/quick-test' class='btn'>🔄 再次測試</a>
        <a href='/' class='btn'>🏠 返回首頁</a>
    </div>
    
    <script>
        console.log('🎉 付款成功回呼收到:', {string.Join(", ", allParams.Select(p => $"'{p.Key}': '{p.Value}'"))});
    </script>
</body>
</html>";
                    return Content(successHtml, "text/html");
                }
                else
                {
                    _logger.LogWarning("❌ 付款失敗 - 錯誤代碼: {Code}, 所有參數: {@Params}", rtnCode, allParams);
                    var failHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <title>付款失敗</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #f8f9fa; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 40px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .error {{ color: #dc3545; font-size: 32px; margin-bottom: 20px; }}
        .info {{ background: #f8d7da; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #dc3545; }}
        .btn {{ background: #007bff; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }}
        .params {{ background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; text-align: left; font-family: monospace; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error'>❌ 付款失敗</div>
        <div class='info'>
            <h3>⚠️ 錯誤資訊</h3>
            <p><strong>錯誤代碼：</strong>{rtnCode}</p>
            <p><strong>訂單編號：</strong>{allParams.GetValueOrDefault("MerchantTradeNo", "未知")}</p>
        </div>
        
        <div class='params'>
            <strong>🔍 完整回傳參數：</strong><br>
            {string.Join("<br>", allParams.Select(p => $"{p.Key}: {p.Value}"))}
        </div>
        
        <a href='/api/payments/quick-test' class='btn'>🔄 重新測試</a>
    </div>
</body>
</html>";
                    return Content(failHtml, "text/html");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 處理 ECPay 返回時發生錯誤");
                return BadRequest("處理付款結果時發生錯誤");
            }
        }

        /// <summary>
        /// ECPay 付款通知（背景通知）
        /// </summary>
        [HttpPost("ecpay/notify")]
        public IActionResult EcpayNotify()
        {
            try
            {
                // 取得所有 POST 參數
                var allParams = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
                
                _logger.LogInformation("ECPay Notify - 收到通知: {@Params}", allParams);

                // 這裡應該要驗證 CheckMacValue，但為了測試先跳過
                
                // 檢查付款狀態
                if (allParams.TryGetValue("RtnCode", out var rtnCode) && rtnCode == "1")
                {
                    var merchantTradeNo = allParams.GetValueOrDefault("MerchantTradeNo", "");
                    var tradeAmt = allParams.GetValueOrDefault("TradeAmt", "");
                    
                    _logger.LogInformation("付款通知成功 - 訂單號: {OrderNo}, 金額: {Amount}", 
                        merchantTradeNo, tradeAmt);

                    // 在這裡更新你的資料庫，標記訂單為已付款
                    // await _orderService.UpdateOrderStatus(merchantTradeNo, "Paid");
                    
                    // 必須回傳 "1|OK" 給 ECPay
                    return Content("1|OK");
                }
                else
                {
                    _logger.LogWarning("付款通知失敗 - 錯誤代碼: {Code}", rtnCode);
                    return Content("0|Error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理 ECPay 通知時發生錯誤");
                return Content("0|Error");
            }
        }
    }
}
