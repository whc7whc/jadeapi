using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Team.Backend.Models.EfModel;
using System.Linq;

// 聊天相關 API：不修改資料表，全部以現有欄位 (ChatRoom / ChatMessage) 實作

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
	private readonly AppDbContext _db;
	public ChatController(AppDbContext db) => _db = db;

	private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
	private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty; // 期望 'Member' / 'Seller' / 'Admin'

	#region 房間建立 / 取得
	/// <summary>
	/// 會員或廠商建立 (或取得既有) 的房間。Member 呼叫時提供 sellerId；Seller 呼叫時提供 memberId。
	/// 路由統一：/api/chat/room  (body 傳對方Id) 或簡化使用 /room/seller/{sellerId}
	/// </summary>
	[HttpPost("room/seller/{sellerId:int}")]
	public async Task<IActionResult> EnsureRoomWithSeller(int sellerId)
	{
		if (CurrentRole != "Member") return Forbid();
		var memberId = CurrentUserId;
		var room = await _db.ChatRooms.FirstOrDefaultAsync(r => r.MemberId == memberId && r.SellerId == sellerId && r.Status == "active");
		if (room == null)
		{
			room = new ChatRoom
			{
				MemberId = memberId,
				SellerId = sellerId,
				Status = "active",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow,
				LastMessageAt = null,
				RoomName = null
			};
			_db.ChatRooms.Add(room);
			await _db.SaveChangesAsync();
		}
		return Ok(new { roomId = room.Id });
	}

	[HttpPost("room/member/{memberId:int}")]
	public async Task<IActionResult> EnsureRoomWithMember(int memberId)
	{
		if (CurrentRole != "Seller") return Forbid();
		var sellerId = CurrentUserId;
		var room = await _db.ChatRooms.FirstOrDefaultAsync(r => r.MemberId == memberId && r.SellerId == sellerId && r.Status == "active");
		if (room == null)
		{
			room = new ChatRoom
			{
				MemberId = memberId,
				SellerId = sellerId,
				Status = "active",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
			_db.ChatRooms.Add(room);
			await _db.SaveChangesAsync();
		}
		return Ok(new { roomId = room.Id });
	}
	#endregion

	#region 房間列表 (分角色)
	[HttpGet("rooms/member")] // 會員端查看自己的房間列表
	public async Task<IActionResult> GetMemberRooms()
	{
		if (CurrentRole != "Member") return Forbid();
		var memberId = CurrentUserId;
		var rooms = await _db.ChatRooms
			.Where(r => r.MemberId == memberId)
			.OrderByDescending(r => r.LastMessageAt ?? r.UpdatedAt)
			.Select(r => new
			{
				r.Id,
				r.SellerId,
				LastMessageAt = r.LastMessageAt,
				UnreadCount = _db.ChatMessages.Count(m => m.ChatRoomId == r.Id && m.SenderType != "Member" && (m.IsReadMember == false || m.IsReadMember == null)),
				LastMessagePreview = _db.ChatMessages
					.Where(m => m.ChatRoomId == r.Id && !m.IsDeleted)
					.OrderByDescending(m => m.CreatedAt)
					.Select(m => m.MessageType == "text" ? m.Content : (m.MessageType + " message"))
					.FirstOrDefault()
			})
			.ToListAsync();
		return Ok(rooms);
	}

	[HttpGet("rooms/seller")] // 廠商端查看自己的房間列表
	public async Task<IActionResult> GetSellerRooms()
	{
		if (CurrentRole != "Seller") return Forbid();
		var sellerId = CurrentUserId;
		var rooms = await _db.ChatRooms
			.Where(r => r.SellerId == sellerId)
			.OrderByDescending(r => r.LastMessageAt ?? r.UpdatedAt)
			.Select(r => new
			{
				r.Id,
				r.MemberId,
				LastMessageAt = r.LastMessageAt,
				UnreadCount = _db.ChatMessages.Count(m => m.ChatRoomId == r.Id && m.SenderType != "Seller" && (m.IsReadSeller == false || m.IsReadSeller == null)),
				LastMessagePreview = _db.ChatMessages
					.Where(m => m.ChatRoomId == r.Id && !m.IsDeleted)
					.OrderByDescending(m => m.CreatedAt)
					.Select(m => m.MessageType == "text" ? m.Content : (m.MessageType + " message"))
					.FirstOrDefault()
			})
			.ToListAsync();
		return Ok(rooms);
	}
	#endregion

	#region 訊息歷史 (分頁)
	/// <summary>
	/// 取得訊息歷史。支援 beforeMessageId 分頁。預設 take=30。
	/// 若提供 beforeMessageId，會取該訊息之前的資料 (不含該訊息)。回傳正序。
	/// </summary>
	[HttpGet("rooms/{roomId:int}/messages")]
	public async Task<IActionResult> GetMessages(int roomId, int? beforeMessageId = null, int take = 30)
	{
		if (take > 100) take = 100;
		var uid = CurrentUserId;
		var room = await _db.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId);
		if (room == null) return NotFound();
		if (room.MemberId != uid && room.SellerId != uid && room.AdminId != uid) return Forbid();

		DateTime? threshold = null;
		int? thresholdId = null;
		if (beforeMessageId.HasValue)
		{
			var refMsg = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == beforeMessageId && m.ChatRoomId == roomId);
			if (refMsg != null)
			{
				threshold = refMsg.CreatedAt;
				thresholdId = refMsg.Id;
			}
		}

		var query = _db.ChatMessages
			.Where(m => m.ChatRoomId == roomId && !m.IsDeleted);
		if (threshold.HasValue)
		{
			query = query.Where(m => m.CreatedAt < threshold || (m.CreatedAt == threshold && m.Id < thresholdId));
		}

		var slice = await query
			.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
			.Take(take)
			.ToListAsync();

		// 反轉為時間正序
		slice.Reverse();

		var firstFetchedCreatedAt = slice.FirstOrDefault()?.CreatedAt;
		var moreExists = false;
		if (slice.Count == take)
		{
			// 繼續檢查是否還有更舊的
			moreExists = await _db.ChatMessages.AnyAsync(m => m.ChatRoomId == roomId && !m.IsDeleted && (
				(beforeMessageId == null && m.CreatedAt < slice.First().CreatedAt) ||
				(beforeMessageId != null && (m.CreatedAt < (firstFetchedCreatedAt ?? DateTime.MaxValue) || (m.CreatedAt == firstFetchedCreatedAt && m.Id < slice.First().Id)))
			));
		}

		return Ok(new
		{
			roomId,
			messages = slice.Select(m => new
			{
				m.Id,
				m.SenderType,
				m.SenderId,
				m.MessageType,
				m.Content,
				m.FileUrl,
				m.IsReadMember,
				m.IsReadSeller,
				m.IsReadAdmin,
				m.CreatedAt
			}),
			hasMore = moreExists
		});
	}
	#endregion

	#region 標記已讀 / 未讀摘要
	[HttpPost("room/{roomId:int}/read")]
	public async Task<IActionResult> MarkRoomRead(int roomId)
	{
		var uid = CurrentUserId;
		var room = await _db.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId);
		if (room == null) return NotFound();
		if (room.MemberId != uid && room.SellerId != uid && room.AdminId != uid) return Forbid();

		if (room.MemberId == uid)
		{
			await _db.ChatMessages
				.Where(m => m.ChatRoomId == roomId && (m.IsReadMember == false || m.IsReadMember == null))
				.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadMember, true));
		}
		else if (room.SellerId == uid)
		{
			await _db.ChatMessages
				.Where(m => m.ChatRoomId == roomId && (m.IsReadSeller == false || m.IsReadSeller == null))
				.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadSeller, true));
		}
		else if (room.AdminId == uid)
		{
			await _db.ChatMessages
				.Where(m => m.ChatRoomId == roomId && (m.IsReadAdmin == false || m.IsReadAdmin == null))
				.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadAdmin, true));
		}
		await _db.SaveChangesAsync();
		return Ok(new { roomId });
	}

	[HttpGet("unread/summary")] // 總未讀數 (依使用者角色計算)
	public async Task<IActionResult> GetUnreadSummary()
	{
		var uid = CurrentUserId;
		int total = 0;
		if (CurrentRole == "Member")
		{
			var roomIds = await _db.ChatRooms.Where(r => r.MemberId == uid).Select(r => r.Id).ToListAsync();
			total = await _db.ChatMessages.CountAsync(m => roomIds.Contains(m.ChatRoomId) && m.SenderType != "Member" && (m.IsReadMember == false || m.IsReadMember == null));
		}
		else if (CurrentRole == "Seller")
		{
			var roomIds = await _db.ChatRooms.Where(r => r.SellerId == uid).Select(r => r.Id).ToListAsync();
			total = await _db.ChatMessages.CountAsync(m => roomIds.Contains(m.ChatRoomId) && m.SenderType != "Seller" && (m.IsReadSeller == false || m.IsReadSeller == null));
		}
		else if (CurrentRole == "Admin")
		{
			var roomIds = await _db.ChatRooms.Where(r => r.AdminId == uid).Select(r => r.Id).ToListAsync();
			total = await _db.ChatMessages.CountAsync(m => roomIds.Contains(m.ChatRoomId) && m.SenderType != "Admin" && (m.IsReadAdmin == false || m.IsReadAdmin == null));
		}
		return Ok(new { totalUnread = total });
	}
	#endregion
}
