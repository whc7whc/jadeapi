using Team.API.Models.EfModel;
using Team.API.DTO;

namespace Team.API.Extensions
{
    public static class PostExtensions
    {
        /// <summary>
        /// 將 Post 實體轉換為 PostResponseDto
        /// </summary>
        public static PostResponseDto ToDto(this Post post)
        {
            return new PostResponseDto
            {
                Id = post.Id,
                Title = post.Title ?? string.Empty,
                Content = post.Content ?? string.Empty,
                MembersId = post.MembersId,
                Image = post.Image,
                Status = NormalizeStatus(post.Status), // 🔥 統一狀態格式
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                PublishedAt = post.PublishedAt,
                ReviewedBy = post.ReviewedBy,
                ReviewedAt = post.ReviewedAt,
                RejectedReason = post.RejectedReason,

                // 🔥 會員資訊
                MemberName = GetMemberName(post),
                MemberAvatar = GetMemberAvatar(post),

                // 🔥 互動數據 - 這裡可以從實際的互動資料表取得
                LikesCount = GetLikesCount(post.Id),
                ViewsCount = GetViewsCount(post.Id),
                IsLiked = false // TODO: 根據當前用戶判斷是否已按讚
            };
        }

        /// <summary>
        /// 將 Post 實體轉換為 PostSummaryDto（用於列表顯示）
        /// </summary>
        public static PostSummaryDto ToSummaryDto(this Post post)
        {
            return new PostSummaryDto
            {
                Id = post.Id,
                Title = post.Title ?? string.Empty,
                Image = post.Image,
                Status = NormalizeStatus(post.Status),
                CreatedAt = post.CreatedAt,
                PublishedAt = post.PublishedAt,
                MemberName = GetMemberName(post),
                LikesCount = GetLikesCount(post.Id),
                ViewsCount = GetViewsCount(post.Id)
            };
        }

        /// <summary>
        /// 統一狀態格式為小寫
        /// </summary>
        private static string NormalizeStatus(string? status)
        {
            return status?.ToLower() switch
            {
                "draft" => "draft",
                "pending" => "pending",
                "published" => "published",
                "rejected" => "rejected",
                _ => "draft"
            };
        }

        /// <summary>
        /// 取得會員名稱
        /// </summary>
        private static string GetMemberName(Post post)
        {
            // 優先使用個人資料中的名稱
            if (!string.IsNullOrEmpty(post.Members?.MemberProfile?.Name))
            {
                return post.Members.MemberProfile.Name;
            }

            // 其次使用會員帳號
            if (!string.IsNullOrEmpty(post.Members?.MemberProfile?.MemberAccount))
            {
                return post.Members.MemberProfile.MemberAccount;
            }

            // 最後使用預設格式
            return $"會員 {post.MembersId}";
        }

        /// <summary>
        /// 取得會員頭像
        /// </summary>
        private static string? GetMemberAvatar(Post post)
        {
            return post.Members?.MemberProfile?.ProfileImg;
        }

        /// <summary>
        /// 取得按讚數量
        /// TODO: 實作真實的按讚統計
        /// </summary>
        private static int GetLikesCount(int postId)
        {
            // 這裡應該查詢 PostLikes 表來取得真實的按讚數
            // var likesCount = _context.PostLikes.Count(pl => pl.PostId == postId);
            // return likesCount;

            // 暫時回傳模擬數據
            return Random.Shared.Next(0, 100);
        }

        /// <summary>
        /// 取得瀏覽次數
        /// TODO: 實作真實的瀏覽統計
        /// </summary>
        private static int GetViewsCount(int postId)
        {
            // 這裡應該查詢瀏覽記錄表來取得真實的瀏覽次數
            // 或者在 Posts 表中新增 ViewsCount 欄位

            // 暫時回傳模擬數據
            return Random.Shared.Next(10, 500);
        }

        /// <summary>
        /// 檢查用戶是否已按讚
        /// TODO: 實作真實的按讚檢查
        /// </summary>
        private static bool IsLikedByUser(int postId, int? userId)
        {
            if (!userId.HasValue) return false;

            // 這裡應該查詢 PostLikes 表
            // return _context.PostLikes.Any(pl => pl.PostId == postId && pl.MembersId == userId);

            return false;
        }

        /// <summary>
        /// 批量轉換為 DTO
        /// </summary>
        public static IEnumerable<PostResponseDto> ToDtos(this IEnumerable<Post> posts)
        {
            return posts.Select(p => p.ToDto());
        }

        /// <summary>
        /// 批量轉換為摘要 DTO
        /// </summary>
        public static IEnumerable<PostSummaryDto> ToSummaryDtos(this IEnumerable<Post> posts)
        {
            return posts.Select(p => p.ToSummaryDto());
        }
    }
}