using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Team.API.Payments
{
    public static class EcpayCheckMac
    {
        /// <summary>
        /// 根據綠界官方文件規範生成 CheckMacValue
        /// </summary>
        public static string Gen(IDictionary<string, string> fields, string hashKey, string hashIV)
        {
            // 1. 移除 CheckMacValue 參數並按字母順序排序
            var sorted = fields
                .Where(kv => !kv.Key.Equals("CheckMacValue", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}");

            // 2. 組合字串：HashKey + 參數 + HashIV
            var raw = $"HashKey={hashKey}&{string.Join("&", sorted)}&HashIV={hashIV}";

            // 3. URL 編碼
            var encoded = HttpUtility.UrlEncode(raw, Encoding.UTF8);

            // 4. 轉小寫
            encoded = encoded.ToLowerInvariant();

            // 5. SHA256 雜湊並轉大寫
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(encoded));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
        }

        /// <summary>
        /// 綠界專用的 URL Encode 轉換
        /// 依照綠界提供的轉換表進行字元替換
        /// </summary>
        public static string EcpayUrlEncode(string data)
        {
            string replacedString = data.Replace("%", "%25");

            replacedString = replacedString.Replace("~", "%7e");
            replacedString = replacedString.Replace("+", "%2b");
            replacedString = replacedString.Replace(" ", "+");
            replacedString = replacedString.Replace("@", "%40");
            replacedString = replacedString.Replace("#", "%23");
            replacedString = replacedString.Replace("$", "%24");
            replacedString = replacedString.Replace("&", "%26");
            replacedString = replacedString.Replace("=", "%3d");
            replacedString = replacedString.Replace(";", "%3b");
            replacedString = replacedString.Replace("?", "%3f");

            replacedString = replacedString.Replace("/", "%2f");
            replacedString = replacedString.Replace("\\", "%5c");
            replacedString = replacedString.Replace(">", "%3e");
            replacedString = replacedString.Replace("<", "%3c");
            replacedString = replacedString.Replace("`", "%60");
            replacedString = replacedString.Replace("[", "%5b");
            replacedString = replacedString.Replace("]", "%5d");
            replacedString = replacedString.Replace("{", "%7b");
            replacedString = replacedString.Replace("}", "%7d");
            replacedString = replacedString.Replace(":", "%3a");

            replacedString = replacedString.Replace("'", "%27");
            replacedString = replacedString.Replace("\"", "%22");
            replacedString = replacedString.Replace(",", "%2c");
            replacedString = replacedString.Replace("|", "%7c");

            return replacedString;
        }

        /// <summary>
        /// 使用綠界專用編碼規則生成檢查碼
        /// </summary>
        public static string GenWithEcpayEncoding(IDictionary<string, string> fields, string hashKey, string hashIV)
        {
            // 1. 過濾掉CheckMacValue並按字母順序排序
            var sorted = fields
                .Where(kv => !kv.Key.Equals("CheckMacValue", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}");
            
            // 2. 組合原始字串
            var raw = $"HashKey={hashKey}&{string.Join("&", sorted)}&HashIV={hashIV}";

            // 3. 使用綠界專用URL編碼
            var encoded = EcpayUrlEncode(raw);
            
            // 4. 轉小寫
            encoded = encoded.ToLowerInvariant();

            // 5. SHA256雜湊並轉大寫
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(encoded));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
        }
    }
}
