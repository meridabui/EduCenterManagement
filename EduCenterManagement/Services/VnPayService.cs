using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace EduCenterManagement.Services
{
    public class VnPaySettings
    {
        public const string SectionName = "VnPay";

        public string TmnCode { get; set; } = string.Empty;
        public string HashSecret { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } =
            "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    }

    public class VnPayPaymentRequest
    {
        public string TxnRef { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string IpAddress { get; set; } = "127.0.0.1";
        public DateTime ExpireAtUtc { get; set; }
    }

    public class VnPayReturnInfo
    {
        public bool IsValidSignature { get; set; }
        public string TxnRef { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ResponseCode { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public string TransactionNo { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public bool IsSuccess =>
            IsValidSignature && ResponseCode == "00" && TransactionStatus == "00";
    }

    public interface IVnPayService
    {
        // Tạo URL chuyển khách sang cổng VNPay (đã ký HMAC-SHA512).
        string CreatePaymentUrl(VnPayPaymentRequest request);

        // Đọc và kiểm tra chữ ký của tham số VNPay chuyển hướng về.
        VnPayReturnInfo ParseReturn(IQueryCollection query);
    }

    public class VnPayService : IVnPayService
    {
        private const string Version = "2.1.0";
        private readonly VnPaySettings _settings;

        public VnPayService(IOptions<VnPaySettings> options)
        {
            _settings = options.Value;
        }

        public string CreatePaymentUrl(VnPayPaymentRequest request)
        {
            EnsureConfigured();

            // VNPay yêu cầu giờ Việt Nam (GMT+7).
            var vnNow = DateTime.UtcNow.AddHours(7);
            var vnExpire = request.ExpireAtUtc.AddHours(7);

            var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"] = Version,
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = _settings.TmnCode,
                ["vnp_Amount"] = ToVnpAmount(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["vnp_CreateDate"] = vnNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                ["vnp_CurrCode"] = "VND",
                ["vnp_IpAddr"] = request.IpAddress,
                ["vnp_Locale"] = "vn",
                ["vnp_OrderInfo"] = request.OrderInfo,
                ["vnp_OrderType"] = "other",
                ["vnp_ReturnUrl"] = request.ReturnUrl,
                ["vnp_TxnRef"] = request.TxnRef,
                ["vnp_ExpireDate"] = vnExpire.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            };

            var query = BuildQuery(data);
            var hash = ComputeHash(query);

            return $"{_settings.PaymentUrl}?{query}&vnp_SecureHash={hash}";
        }

        public VnPayReturnInfo ParseReturn(IQueryCollection query)
        {
            var data = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var item in query)
            {
                if (item.Key.StartsWith("vnp_", StringComparison.Ordinal) &&
                    item.Key != "vnp_SecureHash" &&
                    item.Key != "vnp_SecureHashType" &&
                    !string.IsNullOrEmpty(item.Value))
                {
                    data[item.Key] = item.Value.ToString();
                }
            }

            string Get(string key) => data.TryGetValue(key, out var value) ? value : string.Empty;

            var received = query["vnp_SecureHash"].ToString();
            var isValid =
                !string.IsNullOrEmpty(received) &&
                !string.IsNullOrEmpty(_settings.HashSecret) &&
                CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(ComputeHash(BuildQuery(data))),
                    Encoding.ASCII.GetBytes(received.ToLowerInvariant()));

            long.TryParse(Get("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var rawAmount);

            var responseCode = Get("vnp_ResponseCode");

            return new VnPayReturnInfo
            {
                IsValidSignature = isValid,
                TxnRef = Get("vnp_TxnRef"),
                Amount = rawAmount / 100m,
                ResponseCode = responseCode,
                TransactionStatus = Get("vnp_TransactionStatus"),
                TransactionNo = Get("vnp_TransactionNo"),
                Message = isValid ? GetMessage(responseCode) : "Chữ ký giao dịch không hợp lệ."
            };
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_settings.TmnCode) ||
                string.IsNullOrWhiteSpace(_settings.HashSecret))
            {
                throw new InvalidOperationException(
                    "Chưa cấu hình VnPay:TmnCode và VnPay:HashSecret (dùng dotnet user-secrets).");
            }
        }

        // VNPay tính tiền theo đơn vị nhỏ nhất: nhân 100.
        private static long ToVnpAmount(decimal amount)
        {
            return (long)decimal.Round(amount * 100, 0);
        }

        private static string BuildQuery(SortedDictionary<string, string> data)
        {
            return string.Join("&", data
                .Where(item => !string.IsNullOrEmpty(item.Value))
                .Select(item => $"{WebUtility.UrlEncode(item.Key)}={WebUtility.UrlEncode(item.Value)}"));
        }

        private string ComputeHash(string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_settings.HashSecret));
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string GetMessage(string code)
        {
            return code switch
            {
                "00" => "Giao dịch thành công.",
                "07" => "Giao dịch bị nghi ngờ gian lận.",
                "09" => "Thẻ/tài khoản chưa đăng ký Internet Banking.",
                "10" => "Xác thực thông tin thẻ/tài khoản sai quá 3 lần.",
                "11" => "Đã hết hạn chờ thanh toán.",
                "12" => "Thẻ/tài khoản bị khóa.",
                "13" => "Sai mật khẩu xác thực giao dịch (OTP).",
                "24" => "Khách hàng đã hủy giao dịch.",
                "51" => "Tài khoản không đủ số dư.",
                "65" => "Tài khoản vượt hạn mức giao dịch trong ngày.",
                "75" => "Ngân hàng thanh toán đang bảo trì.",
                "79" => "Nhập sai mật khẩu thanh toán quá số lần quy định.",
                _ => $"Giao dịch không thành công (mã {code})."
            };
        }
    }
}