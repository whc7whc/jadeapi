using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Team.Backend.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Team.Backend.Controllers
{

    public class SellerManageController : BaseController

    {
        private readonly AppDbContext _context;
        private readonly IUserEmailSender _emailSender;
        private readonly ILogger<SellerManageController> _logger;

        public SellerManageController(AppDbContext context, IUserEmailSender emailSender, ILogger<SellerManageController> logger)
            : base(context, logger)
        {
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }
        // 檢視所有賣家資料
        public async Task<IActionResult> SellerInfo(string? search, int page = 1)
        {
            int pageSize = 10;

            var query = _context.Sellers
                .Include(s => s.Members)
                .Include(s => s.SellerBankAccounts)
                .Include(s => s.SellerReturnInfos)
                .AsQueryable();

            // 搜尋處理（支援姓名、Email、身份證、申請狀態）
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowered = search.ToLower().Trim();

                if (lowered == "待審核") lowered = "pending";
                else if (lowered == "已通過") lowered = "approved";
                else if (lowered == "已拒絕") lowered = "rejected";
                else if (lowered == "重新申請") lowered = "resubmitted";

                query = query.Where(s =>
                    s.RealName.ToLower().Contains(lowered) ||
                    s.IdNumber.ToLower().Contains(lowered) ||
                    s.ApplicationStatus.ToLower().Contains(lowered) ||
                    s.Members.Email.ToLower().Contains(lowered)
                );
            }

            int totalItems = await query.CountAsync();
            var sellers = await query
                .OrderByDescending(s => s.AppliedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModels = sellers.Select(seller =>
            {
                var bankAccount = seller.SellerBankAccounts.FirstOrDefault();
                var returnInfo = seller.SellerReturnInfos.FirstOrDefault();

                return new SellerFullViewModel
                {
                    SellerId = seller.Id,
                    RealName = seller.RealName,
                    IdNumber = seller.IdNumber,
                    ApplicationStatus = seller.ApplicationStatus,
                    SellerAppliedAt = seller.AppliedAt,
                    SellerIsActive = seller.IsActive,
                    Email = seller.Members?.Email ?? string.Empty,
                    BankName = bankAccount?.BankName ?? string.Empty,
                    BankCode = bankAccount?.BankCode ?? string.Empty,
                    AccountName = bankAccount?.AccountName ?? string.Empty,
                    AccountNumber = bankAccount?.AccountNumber ?? string.Empty,
                    SellerBankAccountIsVerified = bankAccount?.IsVerified ?? false,
                    ContactName = returnInfo?.ContactName ?? string.Empty,
                    ContactPhone = returnInfo?.ContactPhone ?? string.Empty,
                    ReturnAddress = returnInfo?.ReturnAddress ?? string.Empty,
                    City = returnInfo?.City ?? string.Empty,
                    District = returnInfo?.District ?? string.Empty,
                    ZipCode = returnInfo?.ZipCode ?? string.Empty,
                };
            }).ToList();

            // 傳遞分頁資訊給 View
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(viewModels);
        }

        //賣家詳細資料
        public async Task<IActionResult> SellerDetail(int id)
        {
            if (id <= 0)
                return BadRequest("不合法的賣家 ID");

            var seller = await _context.Sellers
                .Include(s => s.Members)
                .Include(s => s.SellerBankAccounts)
                .Include(s => s.SellerReturnInfos)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (seller == null)
                return NotFound();

            var bankAccount = seller.SellerBankAccounts?.FirstOrDefault();
            var returnInfo = seller.SellerReturnInfos?.FirstOrDefault();

            // 撈文件照片
            var documents = await _context.SellerDocuments
                .Where(d => d.SellersId == id)
                .Select(d => new SellerDocumentViewModel
                {
                    DocumentType = d.DocumentType,
                    FilePath = d.FilePath,
                    Verified = d.Verified,
                    UploadedAt = d.UploadedAt
                })
                .ToListAsync();

            var vm = new SellerFullViewModel
            {

                SellerAppliedAt = seller.AppliedAt,
                SellerId = seller.Id,
                RealName = seller.RealName ?? string.Empty,
                IdNumber = seller.IdNumber ?? string.Empty,
                ApplicationStatus = seller.ApplicationStatus ?? string.Empty,
                SellerIsActive = seller.IsActive,
                Email = seller.Members?.Email ?? string.Empty,
                BankName = bankAccount?.BankName ?? string.Empty,
                BankCode = bankAccount?.BankCode ?? string.Empty,
                AccountName = bankAccount?.AccountName ?? string.Empty,
                AccountNumber = bankAccount?.AccountNumber ?? string.Empty,
                SellerBankAccountIsVerified = bankAccount?.IsVerified ?? false,
                ContactName = returnInfo?.ContactName ?? string.Empty,
                ContactPhone = returnInfo?.ContactPhone ?? string.Empty,
                ReturnAddress = returnInfo?.ReturnAddress ?? string.Empty,
                City = returnInfo?.City ?? string.Empty,
                District = returnInfo?.District ?? string.Empty,
                ZipCode = returnInfo?.ZipCode ?? string.Empty,

                Documents = documents // 這裡塞進文件清單
            };

            return PartialView("_SellerDetailPartial", vm);
        }

        // ✅ 儲存狀態變更（也可包含拒絕原因）
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> UpdateSellerStatus(int sellerId, string status, string? rejectionReason)
        {
            var seller = await _context.Sellers
                .Include(s => s.Members)
                .FirstOrDefaultAsync(s => s.Id == sellerId);

            if (seller == null)
                return Json(new { success = false, message = "找不到賣家。" });

            // 一律存英文
            seller.ApplicationStatus = status;
            seller.IsActive = status == "approved";

            // ✅ 通過時，設定會員角色
            if (status == "approved" && seller.Members != null)
            {
                seller.Members.Role = true; // 假設 Role 是 bool 類型
            }

            // ✅ 記錄拒絕原因
            if (status == "rejected")
            {
                seller.RejectedReason = rejectionReason;
            }
            else
            {
                seller.RejectedReason = null;
            }

            await _context.SaveChangesAsync();

            // 顯示用的中文
            string statusDisplay = status switch
            {
                "pending" => "待審核",
                "approved" => "已通過",
                "rejected" => "已拒絕",
                "resubmitted" => "重新申請",
                _ => status
            };

            //寄送通知
            string subject;
            string body;

            if (status == "approved")
            {
                subject = "賣家申請已通過";
                body = $"<p>您好 {seller.RealName}，</p><p>您的賣家申請已通過，歡迎使用平台功能！</p>";
            }
            else if (status == "rejected")
            {
                subject = "賣家申請未通過";
                body = $"<p>您好 {seller.RealName}，</p><p>很抱歉，您的賣家申請未通過。</p><p><strong>原因：</strong> {rejectionReason}</p>";
            }
            else if (status == "resubmitted")
            {
                subject = "賣家重新提交申請";
                body = $"<p>您好 {seller.RealName}，</p><p>您已重新提交賣家申請，請耐心等待管理員審核。</p>";
            }
            else
            {
                subject = "賣家申請狀態已更新";
                body = $"<p>您好 {seller.RealName}，</p><p>您的申請狀態已更新為：{statusDisplay}</p>";
            }

            await _emailSender.SendEmailAsync(seller.Members.Email, subject, body);
            return Json(new { success = true });
        }


        // ✅ 通過申請（快捷）
        [HttpPost]
        public async Task<IActionResult> ApproveApplication(int sellerId)
        {
            return await UpdateSellerStatus(sellerId, "approved", null);
        }

        [HttpPost]
        public async Task<IActionResult> RejectApplication(int sellerId, string rejectionReason)
        {
            return await UpdateSellerStatus(sellerId, "rejected", rejectionReason);
        }

        //重新申請
        [HttpPost]
        public async Task<IActionResult> ResubmitApplication(int sellerId)
        {
            var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.Id == sellerId);

            if (seller == null)
                return Json(new { success = false, message = "找不到賣家。" });

            if (seller.ApplicationStatus != "rejected")
                return Json(new { success = false, message = "只有被拒絕的申請才能重新申請。" });

            seller.ApplicationStatus = "resubmitted";
            seller.AppliedAt = DateTime.UtcNow;
            seller.IsActive = false;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

    }
}
