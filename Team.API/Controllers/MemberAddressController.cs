// Controllers/MemberAddressController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Team.API.DTO;
using Team.API.Models.EfModel;

namespace Team.API.Controllers
{
    [Route("api/members/{memberId}/addresses")]
    [ApiController]
    public class MemberAddressController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MemberAddressController(AppDbContext context)
        {
            _context = context;
        }

        // 1. 取得會員所有地址
        [HttpGet]
        public async Task<IActionResult> GetMemberAddresses(int memberId)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            var addresses = await _context.MemberAddresses
                .Where(a => a.MembersId == memberId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new MemberAddressResponseDto
                {
                    Id = a.Id,
                    MembersId = a.MembersId,
                    RecipientName = a.RecipientName,
                    PhoneNumber = a.PhoneNumber,
                    City = a.City,
                    District = a.District,
                    ZipCode = a.ZipCode,
                    StreetAddress = a.StreetAddress,
                    IsDefault = a.IsDefault,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync();

            return Ok(addresses);
        }

        // 2. 取得單一地址
        [HttpGet("{addressId}")]
        public async Task<IActionResult> GetMemberAddress(int memberId, int addressId)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            var address = await _context.MemberAddresses
                .Where(a => a.MembersId == memberId && a.Id == addressId)
                .Select(a => new MemberAddressResponseDto
                {
                    Id = a.Id,
                    MembersId = a.MembersId,
                    RecipientName = a.RecipientName,
                    PhoneNumber = a.PhoneNumber,
                    City = a.City,
                    District = a.District,
                    ZipCode = a.ZipCode,
                    StreetAddress = a.StreetAddress,
                    IsDefault = a.IsDefault,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (address == null)
                return NotFound("找不到該地址");

            return Ok(address);
        }

        // 3. 新增地址
        [HttpPost]
        public async Task<IActionResult> CreateMemberAddress(int memberId, [FromBody] CreateMemberAddressDto dto)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            // 檢查地址數量限制 (例如最多10個)
            var addressCount = await _context.MemberAddresses.CountAsync(a => a.MembersId == memberId);
            if (addressCount >= 10)
                return BadRequest("每位會員最多只能建立10個地址");

            // 如果設定為預設地址，先將其他地址的預設狀態取消
            if (dto.IsDefault)
            {
                await ClearDefaultAddress(memberId);
            }
            // 如果是第一個地址，自動設為預設
            else if (addressCount == 0)
            {
                dto.IsDefault = true;
            }

            var address = new MemberAddress
            {
                MembersId = memberId,
                RecipientName = dto.RecipientName,
                PhoneNumber = dto.PhoneNumber,
                City = dto.City,
                District = dto.District,
                ZipCode = dto.ZipCode,
                StreetAddress = dto.StreetAddress,
                IsDefault = dto.IsDefault,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.MemberAddresses.Add(address);
            await _context.SaveChangesAsync();

            var responseDto = new MemberAddressResponseDto
            {
                Id = address.Id,
                MembersId = address.MembersId,
                RecipientName = address.RecipientName,
                PhoneNumber = address.PhoneNumber,
                City = address.City,
                District = address.District,
                ZipCode = address.ZipCode,
                StreetAddress = address.StreetAddress,
                IsDefault = address.IsDefault,
                CreatedAt = address.CreatedAt,
                UpdatedAt = address.UpdatedAt
            };

            return CreatedAtAction(nameof(GetMemberAddress),
                new { memberId = memberId, addressId = address.Id }, responseDto);
        }

        // 4. 更新地址
        [HttpPut("{addressId}")]
        public async Task<IActionResult> UpdateMemberAddress(int memberId, int addressId, [FromBody] UpdateMemberAddressDto dto)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            var address = await _context.MemberAddresses
                .FirstOrDefaultAsync(a => a.MembersId == memberId && a.Id == addressId);

            if (address == null)
                return NotFound("找不到該地址");

            // 如果設定為預設地址，先將其他地址的預設狀態取消
            if (dto.IsDefault && !address.IsDefault)
            {
                await ClearDefaultAddress(memberId);
            }

            address.RecipientName = dto.RecipientName;
            address.PhoneNumber = dto.PhoneNumber;
            address.City = dto.City;
            address.District = dto.District;
            address.ZipCode = dto.ZipCode;
            address.StreetAddress = dto.StreetAddress;
            address.IsDefault = dto.IsDefault;
            address.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok("地址更新成功");
        }

        // 5. 刪除地址
        [HttpDelete("{addressId}")]
        public async Task<IActionResult> DeleteMemberAddress(int memberId, int addressId)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            var address = await _context.MemberAddresses
                .FirstOrDefaultAsync(a => a.MembersId == memberId && a.Id == addressId);

            if (address == null)
                return NotFound("找不到該地址");

            //// 檢查是否有關聯的訂單
            //var hasOrders = await _context.Orders.AnyAsync(o => o.MemberAddressId == addressId);
            //if (hasOrders)
            //    return BadRequest("此地址已有訂單記錄，無法刪除");

            var wasDefault = address.IsDefault;

            _context.MemberAddresses.Remove(address);
            await _context.SaveChangesAsync();

            // 如果刪除的是預設地址，將最新的地址設為預設
            if (wasDefault)
            {
                var latestAddress = await _context.MemberAddresses
                    .Where(a => a.MembersId == memberId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestAddress != null)
                {
                    latestAddress.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok("地址刪除成功");
        }

        // 6. 設定預設地址
        [HttpPatch("set-default")]
        public async Task<IActionResult> SetDefaultAddress(int memberId, [FromBody] SetDefaultAddressDto dto)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            var address = await _context.MemberAddresses
                .FirstOrDefaultAsync(a => a.MembersId == memberId && a.Id == dto.AddressId);

            if (address == null)
                return NotFound("找不到該地址");

            if (address.IsDefault)
                return Ok("該地址已是預設地址");

            // 清除所有預設狀態
            await ClearDefaultAddress(memberId);

            // 設定新的預設地址
            address.IsDefault = true;
            address.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok("預設地址設定成功");
        }

        // 7. 取得預設地址
        [HttpGet("default")]
        public async Task<IActionResult> GetDefaultAddress(int memberId)
        {
            // 檢查會員是否存在
            var member = await _context.Members.FindAsync(memberId);
            if (member == null)
                return NotFound("找不到會員");

            var defaultAddress = await _context.MemberAddresses
                .Where(a => a.MembersId == memberId && a.IsDefault)
                .Select(a => new MemberAddressResponseDto
                {
                    Id = a.Id,
                    MembersId = a.MembersId,
                    RecipientName = a.RecipientName,
                    PhoneNumber = a.PhoneNumber,
                    City = a.City,
                    District = a.District,
                    ZipCode = a.ZipCode,
                    StreetAddress = a.StreetAddress,
                    IsDefault = a.IsDefault,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (defaultAddress == null)
                return NotFound("找不到預設地址");

            return Ok(defaultAddress);
        }

        // --- 輔助方法 ---
        private async Task ClearDefaultAddress(int memberId)
        {
            var defaultAddresses = await _context.MemberAddresses
                .Where(a => a.MembersId == memberId && a.IsDefault)
                .ToListAsync();

            foreach (var addr in defaultAddresses)
            {
                addr.IsDefault = false;
                addr.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }
    }
}