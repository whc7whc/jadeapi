using System.ComponentModel.DataAnnotations;
using Team.API.Models.EfModel;

namespace Team.API.Models.DTOs
{
    // �t�ӵn�J�ШD DTO
    public class VendorLoginDto
    {
        [Required(ErrorMessage = "Email ���������")]
        [EmailAddress(ErrorMessage = "Email �榡�����T")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "�K�X���������")]
        [MinLength(6, ErrorMessage = "�K�X���צܤ�6�r��")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }

    // �t�ӵ��U�ШD DTO
    public class VendorRegisterDto
    {
        [Required(ErrorMessage = "Email ���������")]
        [EmailAddress(ErrorMessage = "Email �榡�����T")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "�K�X���������")]
        [MinLength(6, ErrorMessage = "�K�X���צܤ�6�r��")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "�u��m�W���������")]
        [StringLength(50, ErrorMessage = "�u��m�W���פ���W�L50�r��")]
        public string RealName { get; set; } = string.Empty;

        [Required(ErrorMessage = "�����Ҧr�����������")]
        [StringLength(10, ErrorMessage = "�����Ҧr�����ץ�����10�r��")]
        public string IdNumber { get; set; } = string.Empty;

        [Phone(ErrorMessage = "������X�榡�����T")]
        public string? Phone { get; set; }
    }

    // �t�Ӹ�T�^�� DTO
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

        // �Ȧ�b���T
        public VendorBankAccountDto? BankAccount { get; set; }

        // �h�f��T
        public VendorReturnInfoDto? ReturnInfo { get; set; }

        // �榡�����
        public string StatusLabel => ApplicationStatus switch
        {
            "pending" => "�f�֤�",
            "approved" => "�w�q�L",
            "rejected" => "�w�ڵ�",
            _ => "�������A"
        };

        public string FormattedAppliedAt => AppliedAt.ToString("yyyy-MM-dd HH:mm");
        public string FormattedApprovedAt => ApprovedAt?.ToString("yyyy-MM-dd HH:mm") ?? "�|���f��";
    }

    // �t�ӻȦ�b�� DTO
    public class VendorBankAccountDto
    {
        public string BankName { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    // �t�Ӱh�f��T DTO
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

    // �t�ӵn�J�^�� DTO
    public class VendorLoginResponseDto
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "�n�J���\";
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
                Message = "�n�J���\",
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

    // ��s�t�Ӹ�T�ШD DTO
    public class UpdateVendorInfoDto
    {
        [Required(ErrorMessage = "�u��m�W���������")]
        [StringLength(50, ErrorMessage = "�u��m�W���פ���W�L50�r��")]
        public string RealName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "������X�榡�����T")]
        public string? Phone { get; set; }

        // �Ȧ�b���T
        public string? BankName { get; set; }
        public string? BankCode { get; set; }
        public string? AccountName { get; set; }
        public string? AccountNumber { get; set; }

        // �h�f��T
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ReturnAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? ZipCode { get; set; }
    }

    // �X�i��k
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