using Microsoft.Extensions.Options;

namespace Team.API.Payments
{
    public class EcpaySandboxGateway : IPaymentGateway
    {
        private readonly EcpayOptions _opt;
        private readonly HttpClient _http;
        private readonly ILogger<EcpaySandboxGateway> _logger;

        public EcpaySandboxGateway(IOptions<EcpayOptions> opt, HttpClient http, ILogger<EcpaySandboxGateway> logger)
        {
            _opt = opt.Value;
            _http = http;
            _logger = logger;
        }

        public async Task<string> CreateAioCheckoutHtmlAsync(EcpayCreateOrder o)
        {
            var fields = new Dictionary<string, string>
            {
                ["MerchantID"] = _opt.MerchantID,
                ["MerchantTradeNo"] = o.MerchantTradeNo,
                ["MerchantTradeDate"] = o.MerchantTradeDate,
                ["PaymentType"] = "aio",
                ["TotalAmount"] = o.TotalAmount.ToString(),
                ["TradeDesc"] = o.TradeDesc,
                ["ItemName"] = o.ItemName,
                ["ReturnURL"] = _opt.ReturnURL,
                ["ChoosePayment"] = o.ChoosePayment,
                ["EncryptType"] = "1"
            };

            if (!string.IsNullOrWhiteSpace(_opt.OrderResultURL))
                fields["OrderResultURL"] = _opt.OrderResultURL;

            // 生成檢查碼 - 使用標準方法
            fields["CheckMacValue"] = EcpayCheckMac.Gen(fields, _opt.HashKey, _opt.HashIV);

            _logger.LogInformation("🔐 綠界付款請求參數: {@Fields}", fields);

            var content = new FormUrlEncodedContent(fields);
            var resp = await _http.PostAsync(_opt.AioCheckOutUrl, content);
            resp.EnsureSuccessStatusCode();
            
            var html = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("✅ 綠界回應成功，HTML 長度: {Length}", html.Length);
            
            return html; // 綠界會回一段 HTML
        }

        public bool VerifyCheckMac(IDictionary<string, string> f)
        {
            // 記錄原始參數
            _logger.LogInformation("🔍 收到的所有參數: {@Params}", f);
            
            var expectedCheckMac = EcpayCheckMac.Gen(f, _opt.HashKey, _opt.HashIV);
            var receivedCheckMac = f.TryGetValue("CheckMacValue", out var checkMac) ? checkMac : "";
            
            var isValid = string.Equals(receivedCheckMac, expectedCheckMac, StringComparison.OrdinalIgnoreCase);
            
            _logger.LogInformation("🔐 驗簽詳細資訊:");
            _logger.LogInformation("   - HashKey: {HashKey}", _opt.HashKey);
            _logger.LogInformation("   - HashIV: {HashIV}", _opt.HashIV);
            _logger.LogInformation("   - 預期 CheckMac: {Expected}", expectedCheckMac);
            _logger.LogInformation("   - 收到 CheckMac: {Received}", receivedCheckMac);
            _logger.LogInformation("   - 驗證結果: {IsValid}", isValid ? "✅ 成功" : "❌ 失敗");
            
            return isValid;
        }
    }
}
