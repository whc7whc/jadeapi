using System.ComponentModel.DataAnnotations;
using Team.API.Models.EfModel;

namespace Team.API.Models.DTOs
{
    // Vendor login request DTO
    public class VendorLoginDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email format is incorrect")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }

    // Vendor registration request DTO
    public class VendorRegisterDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email format is incorrect")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Real name is required")]
        [StringLength(50, ErrorMessage = "Real name cannot exceed 50 characters")]
        public string RealName { get; set; } = string.Empty;

        [Required(ErrorMessage = "ID number is required")]
        [StringLength(10, ErrorMessage = "ID number must be exactly 10 characters")]
        public string IdNumber { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Phone number format is incorrect")]
        public string? Phone { get; set; }
    }

    // Vendor information response DTO
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

        // Bank account information
        public VendorBankAccountDto? BankAccount { get; set; }

        // Return information
        public VendorReturnInfoDto? ReturnInfo { get; set; }

        // Formatted properties
        public string StatusLabel => ApplicationStatus switch
        {
            "pending" => "Pending review",
            "approved" => "Approved",
            "rejected" => "Rejected",
            _ => "Unknown status"
        };

        public string FormattedAppliedAt => AppliedAt.ToString("yyyy-MM-dd HH:mm");
        public string FormattedApprovedAt => ApprovedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Under review";
    }

    // Vendor bank account DTO
    public class VendorBankAccountDto
    {
        public string BankName { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    // Vendor return information DTO
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

    // Vendor login response DTO
    public class VendorLoginResponseDto
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Login successful";
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
                Message = "Login successful",
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

    // Update vendor information request DTO
    public class UpdateVendorInfoDto
    {
        [Required(ErrorMessage = "Real name is required")]
        [StringLength(50, ErrorMessage = "Real name cannot exceed 50 characters")]
        public string RealName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Phone number format is incorrect")]
        public string? Phone { get; set; }

        // Bank account information
        public string? BankName { get; set; }
        public string? BankCode { get; set; }
        public string? AccountName { get; set; }
        public string? AccountNumber { get; set; }

        // Return information
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ReturnAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? ZipCode { get; set; }
    }

    // Mapping extensions
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