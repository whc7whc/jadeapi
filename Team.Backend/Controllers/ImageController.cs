using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.Backend.Models.EfModel;
using Team.Backend.Models.ViewModels;


namespace Team.Backend.Controllers
{
    public class ImageController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;

        public ImageController(AppDbContext context, Cloudinary cloudinary)
        {
            _context = context;
            _cloudinary = cloudinary;
        }

        [HttpGet]
        public IActionResult Upload()
        {
            return View(new UploadImageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Upload(UploadImageViewModel model)
        {
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                try
                {
                    using var stream = model.ImageFile.OpenReadStream();

                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(model.ImageFile.FileName, stream),
                        PublicId = $"jadeTainan_{DateTime.Now.Ticks}",
                        Folder = "uploads",
                        Transformation = new Transformation()
                            .Width(800).Height(600).Crop("limit")
                            .Quality("auto").FetchFormat("auto")
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        model.ImageUrl = uploadResult.SecureUrl.ToString();
                        model.Message = "圖片上傳成功！";
                        model.IsSuccess = true;

                        // 將ImageUrl 儲存到資料庫
                        //var productImage = new ProductImage
                        //{
                        //    ImagesUrl = model.ImageUrl,
                        //    ProductId = model.ProductId,
                        //    SkuId = model.SkuId,
                        //    SortOrder = model.SortOrder
                        //};
                        //await _context.ProductImages.AddAsync(productImage);
                        //await _context.SaveChangesAsync();
                    }
                    else
                    {
                        model.Message = $"上傳失敗：{uploadResult.Error?.Message}";
                        model.IsSuccess = false;
                    }
                }
                catch (Exception ex)
                {
                    model.Message = $"上傳過程發生錯誤：{ex.Message}";
                    model.IsSuccess = false;
                    if (ex.InnerException != null)
                    {
                        model.Message+=$"Inner Exception: {ex.InnerException.Message}";
                        // You can also get more details from the inner exception
                        // like its stack trace, type, etc.
                    }
                }
            }
            else
            {
                model.Message = "請選擇要上傳的圖片檔案";
                model.IsSuccess = false;
            }

            return View(model);
        }

    }
}
