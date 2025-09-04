using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Team.API.DTO;
using Team.API.Models.EfModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplySellerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly Cloudinary _cloudinary;

        public ApplySellerController(AppDbContext context, IEmailService emailService, Cloudinary cloudinary)
        {
            _context = context;
            _emailService = emailService;
            _cloudinary = cloudinary;
        }

        [HttpPost("{memberId}/apply")]
        public async Task<IActionResult> ApplySeller(int memberId, [FromForm] ApplySellerDto dto)
        {
            var member = await _context.Members.FindAsync(memberId);
            if (member == null || !member.IsActive || !member.IsEmailVerified)
                return BadRequest("會員驗證失敗");

            var existingSeller = await _context.Sellers
                .Include(s => s.SellerBankAccounts)
                .Include(s => s.SellerReturnInfos)
                .Include(s => s.SellerDocuments)
                .FirstOrDefaultAsync(s => s.MembersId == memberId);

            using var transaction = await _context.Database.BeginTransactionAsync();

            if (existingSeller != null)
            {
                if (existingSeller.ApplicationStatus == "pending" || existingSeller.ApplicationStatus == "approved")
                    return BadRequest("您已提交過賣家申請");

                if (existingSeller.ApplicationStatus == "rejected")
                {
                    existingSeller.RealName = dto.RealName;
                    existingSeller.IdNumber = dto.IdNumber;
                    existingSeller.ApplicationStatus = "resubmitted";
                    existingSeller.AppliedAt = DateTime.Now;
                    existingSeller.UpdatedAt = DateTime.Now;
                    existingSeller.IsActive = false;
                    existingSeller.ApprovedAt = null;
                    existingSeller.RejectedReason = null;

                    _context.SellerBankAccounts.RemoveRange(existingSeller.SellerBankAccounts);
                    _context.SellerBankAccounts.Add(new SellerBankAccount
                    {
                        SellersId = existingSeller.Id,
                        BankName = dto.BankName,
                        BankCode = dto.BankCode,
                        AccountName = dto.AccountName,
                        AccountNumber = dto.AccountNumber,
                        IsDefault = true,
                        IsVerified = false,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });

                    _context.SellerReturnInfos.RemoveRange(existingSeller.SellerReturnInfos);
                    _context.SellerReturnInfos.Add(new SellerReturnInfo
                    {
                        SellersId = existingSeller.Id,
                        ContactName = dto.ContactName,
                        ContactPhone = dto.ContactPhone,
                        ReturnAddress = dto.ReturnAddress,
                        City = dto.City,
                        District = dto.District,
                        ZipCode = dto.ZipCode,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });

                    _context.SellerDocuments.RemoveRange(existingSeller.SellerDocuments);
                    await _context.SaveChangesAsync();

                    await UploadDocument("frontDoc", dto.frontDoc, existingSeller.Id);
                    await UploadDocument("backDoc", dto.backDoc, existingSeller.Id);
                    await UploadDocument("BankPhoto", dto.BankPhoto, existingSeller.Id);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok("您的賣家申請已重新送出，請耐心等待審核");
                }
            }

            // 新申請
            var seller = new Seller
            {
                MembersId = memberId,
                RealName = dto.RealName,
                IdNumber = dto.IdNumber,
                ApplicationStatus = "pending",
                AppliedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsActive = false
            };
            _context.Sellers.Add(seller);
            await _context.SaveChangesAsync();

            _context.SellerBankAccounts.Add(new SellerBankAccount
            {
                SellersId = seller.Id,
                BankName = dto.BankName,
                BankCode = dto.BankCode,
                AccountName = dto.AccountName,
                AccountNumber = dto.AccountNumber,
                IsDefault = true,
                IsVerified = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });

            _context.SellerReturnInfos.Add(new SellerReturnInfo
            {
                SellersId = seller.Id,
                ContactName = dto.ContactName,
                ContactPhone = dto.ContactPhone,
                ReturnAddress = dto.ReturnAddress,
                City = dto.City,
                District = dto.District,
                ZipCode = dto.ZipCode,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });

            await UploadDocument("frontDoc", dto.frontDoc, seller.Id);
            await UploadDocument("backDoc", dto.backDoc, seller.Id);
            await UploadDocument("BankPhoto", dto.BankPhoto, seller.Id);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok("您的賣家申請已送出，請耐心等待審核");
        }

        private async Task UploadDocument(string type, IFormFile file, int sellerId)
        {
            if (file == null || file.Length == 0) return;

            var allowedExts = new[] { ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExts.Contains(ext))
                throw new Exception($"{type} 的檔案格式不支援");

            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "seller_documents",
                Transformation = new Transformation().Quality("auto")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception($"{type} 上傳失敗");

            var document = new SellerDocument
            {
                SellersId = sellerId,
                DocumentType = type,
                FilePath = uploadResult.SecureUrl.ToString(),
                UploadedAt = DateTime.Now,
                Verified = false
            };

            _context.SellerDocuments.Add(document);
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetSellerApplications(string? status = null)
        {
            var query = _context.Sellers.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.ApplicationStatus == status);

            var list = await query
                .Select(s => new
                {
                    s.Id,
                    s.RealName,
                    s.IdNumber,
                    s.ApplicationStatus,
                    s.AppliedAt,
                    s.IsActive,
                    MemberEmail = s.Members.Email
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("{memberId}/seller-status")]
        public async Task<IActionResult> GetSellerStatus(int memberId)
        {
            var seller = await _context.Sellers
                .FirstOrDefaultAsync(s => s.MembersId == memberId);

            if (seller == null)
                return NotFound("此會員尚未提交賣家申請");

            return Ok(new
            {
                Status = seller.ApplicationStatus,
                IsActive = seller.IsActive,
                AppliedAt = seller.AppliedAt,
                ApprovedAt = seller.ApprovedAt,
                RejectedReason = seller.RejectedReason,
                RealName = seller.RealName,
                IdNumber = seller.IdNumber
            });
        }

        [HttpPost("review")]
        public async Task<IActionResult> ReviewSeller([FromBody] ReviewSellerDto dto)
        {
            var seller = await _context.Sellers.Include(s => s.Members)
                                               .FirstOrDefaultAsync(s => s.Id == dto.SellerId);
            if (seller == null)
                return NotFound("找不到賣家申請");

            var validStatuses = new[] { "approved", "rejected", "pending", "resubmitted" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest("狀態必須是 approved、rejected、pending 或 resubmitted");

            seller.ApplicationStatus = dto.Status;
            seller.IsActive = dto.Status == "approved";

            if (dto.Status == "approved")
            {
                seller.ApprovedAt = DateTime.Now;
                seller.RejectedReason = null;

                if (seller.Members != null)
                {
                    seller.Members.Role = true;
                }
            }
            else if (dto.Status == "rejected")
            {
                seller.RejectedReason = dto.RejectionReason;
            }

            await _context.SaveChangesAsync();

            var subject = dto.Status == "approved" ? "賣家申請已通過" : "賣家申請未通過";
            var body = dto.Status == "approved"
                ? $"您好 {seller.RealName}，您的賣家申請已通過。"
                : $"您好 {seller.RealName}，您的賣家申請未通過。原因：{dto.RejectionReason}";

            // 若已實作 EmailService，可在此寄信
            // await _emailService.SendEmailAsync(seller.Members.Email, subject, body);

            return Ok(new { success = true });
        }

        [HttpGet("{memberId}/seller-info")]
        public async Task<IActionResult> GetSellerFullInfo(int memberId)
        {
            var seller = await _context.Sellers
                .Include(s => s.SellerBankAccounts)
                .Include(s => s.SellerReturnInfos)
                .FirstOrDefaultAsync(s => s.MembersId == memberId);

            if (seller == null)
                return NotFound("此會員尚未成為賣家");

            var bank = seller.SellerBankAccounts.FirstOrDefault();
            var address = seller.SellerReturnInfos.FirstOrDefault();

            return Ok(new
            {
                seller.RealName,
                seller.IdNumber,
                seller.ApplicationStatus,
                seller.AppliedAt,
                seller.UpdatedAt,
                BankName = bank?.BankName,
                BankCode = bank?.BankCode,
                AccountNumber = bank?.AccountNumber,
                AccountName = bank?.AccountName,
                ContactName = address?.ContactName,
                ContactPhone = address?.ContactPhone,
                City = address?.City,
                District = address?.District,
                ZipCode = address?.ZipCode,
                ReturnAddress = address?.ReturnAddress,
                Status = seller.ApplicationStatus
            });
        }
        [HttpPut("{memberId}/bank-info")]
        public async Task<IActionResult> UpdateBankInfo(int memberId, [FromForm] UpdateBankInfoDto dto)
        {
            var seller = await _context.Sellers
                .Include(s => s.SellerBankAccounts)
                .FirstOrDefaultAsync(s => s.MembersId == memberId);

            if (seller == null)
                return NotFound("賣家不存在");

            var bank = seller.SellerBankAccounts.FirstOrDefault();
            if (bank == null)
            {
                bank = new SellerBankAccount
                {
                    SellersId = seller.Id,
                    CreatedAt = DateTime.Now
                };
                _context.SellerBankAccounts.Add(bank);
            }

            bank.BankName = dto.BankName;
            bank.BankCode = dto.BankCode;
            bank.AccountNumber = dto.AccountNumber;
            bank.AccountName = dto.AccountName;
            bank.UpdatedAt = DateTime.Now;

            // 上傳存摺照片（可選）
            if (dto.BankPhoto != null)
            {
                using var stream = dto.BankPhoto.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(dto.BankPhoto.FileName, stream),
                    Folder = "bank_photos",
                    Transformation = new Transformation().Quality("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // 儲存在某個欄位（假設你有 FilePath 欄位，或另外建立 Document）
                    var document = new SellerDocument
                    {
                        SellersId = seller.Id,
                        DocumentType = "BankPhoto",
                        FilePath = uploadResult.SecureUrl.ToString(),
                        UploadedAt = DateTime.Now,
                        Verified = false
                    };
                    _context.SellerDocuments.Add(document);
                }
            }

            await _context.SaveChangesAsync();
            return Ok("銀行資訊更新成功");
        }



        [HttpPut("{memberId}/address-info")]
        public async Task<IActionResult> UpdateAddressInfo(int memberId, [FromBody] UpdateAddressInfoDto dto)
        {
            var seller = await _context.Sellers
                .Include(s => s.SellerReturnInfos)
                .FirstOrDefaultAsync(s => s.MembersId == memberId);

            if (seller == null)
                return NotFound("賣家不存在");

            var address = seller.SellerReturnInfos.FirstOrDefault();
            if (address == null)
            {
                address = new SellerReturnInfo
                {
                    SellersId = seller.Id,
                    CreatedAt = DateTime.Now
                };
                _context.SellerReturnInfos.Add(address);
            }

            address.ContactName = dto.ContactName;
            address.ContactPhone = dto.ContactPhone;
            address.City = dto.City;
            address.District = dto.District;
            address.ZipCode = dto.ZipCode;
            address.ReturnAddress = dto.ReturnAddress;
            address.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok("地址資訊更新成功");
        }
        [HttpGet("{memberId}/seller-id")]
        public async Task<IActionResult> GetSellerId(int memberId)
        {
            var seller = await _context.Sellers
                .Where(s => s.MembersId == memberId)
                .Select(s => new { s.Id }) // 只取 SellerId
                .FirstOrDefaultAsync();

            if (seller == null)
                return NotFound("找不到對應的賣家");

            return Ok(seller);
        }













    }
}
