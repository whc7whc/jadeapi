using Team.Backend.Models.EfModel;
using System;
using System.Collections.Generic;

namespace Team.Backend.Models.ViewModels
{
    public class SellerFullViewModel
    {
        // from Seller table
        public int SellerId { get; set; } // 清楚指出是 Seller 的 Id
        public string RealName { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public string ApplicationStatus { get; set; } = string.Empty;
        public bool SellerIsActive { get; set; }
        public DateTime SellerAppliedAt { get; set; }
        //拒絕原因
        public string? RejectionReason { get; set; }

        // from Member table
        public string Email { get; set; } = string.Empty;

        // from SellerBankAccount table
        public string BankName { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool SellerBankAccountIsVerified { get; set; }

        // from SellerReturnInfo table
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ReturnAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;

        // 新增一個屬性來放文件資料（照片）
        public List<SellerDocumentViewModel> Documents { get; set; } = new List<SellerDocumentViewModel>();
    }

    // 新增一個 ViewModel 專門放文件資訊
    public class SellerDocumentViewModel
    {
        public string DocumentType { get; set; } = string.Empty; // 例如 IdCardFront, IdCardBack, BankPhoto
        public string FilePath { get; set; } = string.Empty;     // 照片的路徑或 URL
        public bool Verified { get; set; }                        // 是否已驗證
        public DateTime UploadedAt { get; set; }                  // 上傳時間
    }
}
