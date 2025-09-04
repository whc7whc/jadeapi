namespace Team.API.DTO
{
    public class ReviewSellerDto
    {

        public int SellerId { get; set; }

    
        public string Status { get; set; } // "approved" 或 "rejected"

        public string? RejectionReason { get; set; } // 拒絕原因（可選）
    }
}
