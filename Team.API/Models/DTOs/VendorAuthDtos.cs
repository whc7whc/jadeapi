using System.ComponentModel.DataAnnotations;
using Team.API.Models.EfModel;

namespace Team.API.Models.DTOs
{
    // 廠商登入請求 DTO
    public class VendorLoginDto
    {
        [Required(ErrorMessage = "Email 為必填欄位")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密碼為必填欄位")]
        [MinLength(6, ErrorMessage = "密碼長度至少6字元")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }

    // 廠商註冊請求 DTO
    public class VendorRegisterDto
    {
        [Required(ErrorMessage = "Email 為必填欄位")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密碼為必填欄位")]
        [MinLength(6, ErrorMessage = "密碼長度至少6字元")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "真實姓名為必填欄位")]
        [StringLength(50, ErrorMessage = "真實姓名長度不能超過50字元")]
        public string RealName { get; set; } = string.Empty;

        [Required(ErrorMessage = "身分證字號為必填欄位")]
        [StringLength(10, ErrorMessage = "身分證字號長度必須為10字元")]
        public string IdNumber { get; set; } = string.Empty;

        [Phone(ErrorMessage = "手機號碼格式不正確")]
        public string? Phone { get; set; }
    }

    // 廠商資訊回應 DTO
    public class VendorInfoDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string RealName { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public string ApplicationStatus { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime AppliedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectedReason { get; set; }

        // 銀行帳戶資訊
        public VendorBankAccountDto? BankAccount { get; set; }

        // 退貨資訊
        public VendorReturnInfoDto? ReturnInfo { get; set; }

        // 格式化顯示
        public string StatusLabel => ApplicationStatus switch
        {
            "pending" => "審核中",
            "approved" => "已通過",
            "rejected" => "已拒絕",
            _ => "未知狀態"
        };

        public string FormattedAppliedAt => AppliedAt.ToString("yyyy-MM-dd HH:mm");
        public string FormattedApprovedAt => ApprovedAt?.ToString("yyyy-MM-dd HH:mm") ?? "尚未審核";
    }

    // 廠商銀行帳戶 DTO
    public class VendorBankAccountDto
    {
        public string BankName { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    // 廠商退貨資訊 DTO
    public class VendorReturnInfoDto
    {
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ReturnAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;

        public string FullAddress => $"{ZipCode} {City}{District}{ReturnAddress}";
    }

    // 廠商登入回應 DTO
    public class VendorLoginResponseDto
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "登入成功";
        public string Token { get; set; } = string.Empty;
        public int SellerId { get; set; }
        public VendorInfoDto VendorInfo { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new();

        public static VendorLoginResponseDto SuccessResult(string token, int sellerId, VendorInfoDto vendorInfo, DateTime expiresAt)
        {
            return new VendorLoginResponseDto
            {
                Success = true,
                Message = "登入成功",
                Token = token,
                SellerId = sellerId,
                VendorInfo = vendorInfo,
                ExpiresAt = expiresAt
            };
        }

        public static VendorLoginResponseDto ErrorResult(string message, Dictionary<string, string>? errors = null)
        {
            return new VendorLoginResponseDto
            {
                Success = false,
                Message = message,
                Errors = errors ?? new Dictionary<string, string>()
            };
        }
    }

    // 更新廠商資訊請求 DTO
    public class UpdateVendorInfoDto
    {
        [Required(ErrorMessage = "真實姓名為必填欄位")]
        [StringLength(50, ErrorMessage = "真實姓名長度不能超過50字元")]
        public string RealName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "手機號碼格式不正確")]
        public string? Phone { get; set; }

        // 銀行帳戶資訊
        public string? BankName { get; set; }
        public string? BankCode { get; set; }
        public string? AccountName { get; set; }
        public string? AccountNumber { get; set; }

        // 退貨資訊
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ReturnAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? ZipCode { get; set; }
    }

    // 擴展方法
    public static class VendorMappingExtensions
    {
        public static VendorInfoDto ToDto(this Seller seller)
        {
            return new VendorInfoDto
            {
                Id = seller.Id,
                Email = seller.Members?.Email ?? string.Empty,
                RealName = seller.RealName ?? string.Empty,
                IdNumber = seller.IdNumber ?? string.Empty,
                ApplicationStatus = seller.ApplicationStatus ?? "pending",
                IsActive = seller.IsActive,
                AppliedAt = seller.AppliedAt,
                ApprovedAt = seller.ApprovedAt,
                RejectedReason = seller.RejectedReason,
                BankAccount = seller.SellerBankAccounts?.FirstOrDefault()?.ToDto(),
                ReturnInfo = seller.SellerReturnInfos?.FirstOrDefault()?.ToDto()
            };
        }

        public static VendorBankAccountDto ToDto(this SellerBankAccount bankAccount)
        {
            return new VendorBankAccountDto
            {
                BankName = bankAccount.BankName ?? string.Empty,
                BankCode = bankAccount.BankCode ?? string.Empty,
                AccountName = bankAccount.AccountName ?? string.Empty,
                AccountNumber = bankAccount.AccountNumber ?? string.Empty,
                IsVerified = bankAccount.IsVerified
            };
        }

        public static VendorReturnInfoDto ToDto(this SellerReturnInfo returnInfo)
        {
            return new VendorReturnInfoDto
            {
                ContactName = returnInfo.ContactName ?? string.Empty,
                ContactPhone = returnInfo.ContactPhone ?? string.Empty,
                ReturnAddress = returnInfo.ReturnAddress ?? string.Empty,
                City = returnInfo.City ?? string.Empty,
                District = returnInfo.District ?? string.Empty,
                ZipCode = returnInfo.ZipCode ?? string.Empty
            };
        }
    }
}