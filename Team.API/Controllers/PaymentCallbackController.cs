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
        /// ECPay payment completion return page
        /// </summary>
        [HttpPost("ecpay/return")]
        public IActionResult EcpayReturn()
        {
            try
            {
                // Get all POST parameters
                var allParams = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
                
                _logger.LogInformation("ECPay Return - Received parameters: {@Params}", allParams);

                // Check payment status
                if (allParams.TryGetValue("RtnCode", out var rtnCode) && rtnCode == "1")
                {
                    var merchantTradeNo = allParams.GetValueOrDefault("MerchantTradeNo", "");
                    var tradeAmt = allParams.GetValueOrDefault("TradeAmt", "");
                    var tradeDate = allParams.GetValueOrDefault("TradeDate", "");
                    
                    _logger.LogInformation("Payment successful - Order: {OrderNo}, Amount: {Amount}, Date: {Date}", 
                        merchantTradeNo, tradeAmt, tradeDate);

                    // Return success page HTML
                    var successHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Payment Successful</title>
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
        <div class='success'>Payment Successful!</div>
        <div class='info'>
            <h3>Transaction Details</h3>
            <p><strong>Order Number:</strong>{merchantTradeNo}</p>
            <p><strong>Amount:</strong>NT$ {tradeAmt}</p>
            <p><strong>Transaction Date:</strong>{tradeDate}</p>
            <p><strong>Return Code:</strong>{rtnCode}</p>
        </div>
        
        <div class='params'>
            <strong>Complete Return Parameters:</strong><br>
            {string.Join("<br>", allParams.Select(p => $"{p.Key}: {p.Value}"))}
        </div>
        
        <a href='/api/payments/quick-test' class='btn'>Test Again</a>
        <a href='/' class='btn'>Home</a>
    </div>
    
    <script>
        console.log('Payment success callback received:', {string.Join(", ", allParams.Select(p => $"'{p.Key}': '{p.Value}'"))});
    </script>
</body>
</html>";
                    return Content(successHtml, "text/html");
                }
                else
                {
                    _logger.LogWarning("Payment failed - Error code: {Code}, All params: {@Params}", rtnCode, allParams);
                    var failHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Payment Failed</title>
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
        <div class='error'>Payment Failed</div>
        <div class='info'>
            <h3>Error Information</h3>
            <p><strong>Error Code:</strong>{rtnCode}</p>
            <p><strong>Order Number:</strong>{allParams.GetValueOrDefault("MerchantTradeNo", "Unknown")}</p>
        </div>
        
        <div class='params'>
            <strong>Complete Return Parameters:</strong><br>
            {string.Join("<br>", allParams.Select(p => $"{p.Key}: {p.Value}"))}
        </div>
        
        <a href='/api/payments/quick-test' class='btn'>Retry</a>
    </div>
</body>
</html>";
                    return Content(failHtml, "text/html");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ECPay return");
                return BadRequest("Error processing payment result");
            }
        }

        /// <summary>
        /// ECPay payment notification (background notification)
        /// </summary>
        [HttpPost("ecpay/notify")]
        public IActionResult EcpayNotify()
        {
            try
            {
                // Get all POST parameters
                var allParams = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
                
                _logger.LogInformation("ECPay Notify - Received notification: {@Params}", allParams);

                // Should verify CheckMacValue here, but skip for testing
                
                // Check payment status
                if (allParams.TryGetValue("RtnCode", out var rtnCode) && rtnCode == "1")
                {
                    var merchantTradeNo = allParams.GetValueOrDefault("MerchantTradeNo", "");
                    var tradeAmt = allParams.GetValueOrDefault("TradeAmt", "");
                    
                    _logger.LogInformation("Payment notification successful - Order: {OrderNo}, Amount: {Amount}", 
                        merchantTradeNo, tradeAmt);

                    // Update database here, mark order as paid
                    // await _orderService.UpdateOrderStatus(merchantTradeNo, "Paid");
                    
                    // Must return "1|OK" to ECPay
                    return Content("1|OK");
                }
                else
                {
                    _logger.LogWarning("Payment notification failed - Error code: {Code}", rtnCode);
                    return Content("0|Error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ECPay notification");
                return Content("0|Error");
            }
        }
    }
}
