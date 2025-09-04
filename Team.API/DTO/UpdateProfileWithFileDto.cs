namespace Team.API.DTO
{
    public class UpdateProfileWithFileDto
    {
        public string Name { get; set; }
        public string Gender { get; set; }
        public DateTime BirthDate { get; set; }
        public IFormFile? ProfileImgFile { get; set; }  // <-- 支援上傳檔案
    }
}
