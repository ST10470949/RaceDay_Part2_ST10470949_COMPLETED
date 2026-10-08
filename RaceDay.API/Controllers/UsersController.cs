using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs.Users;

namespace RaceDay.API.Controllers
{
    /// <summary>
    /// Lets a logged-in user view and update their own profile. Both roles use this.
    /// </summary>
    [Route("api/users")]
    [Route("api/profile")]
    public class UsersController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public UsersController(RaceDayContext context)
        {
            _context = context;
        }

        /// <summary>Returns the logged-in user's own profile.</summary>
        /// <response code="200">Profile returned.</response>
        /// <response code="401">Not logged in.</response>
        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyProfile()
        {
            var check = RequireLogin();
            if (check != null) return check;

            var user = await _context.Users.FindAsync(CurrentUserId!.Value);
            if (user == null)
            {
                return Unauthorized(new { message = "Your session doesn't match a real account any more." });
            }

            return Ok(new UserProfileDto
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            });
        }

        /// <summary>Updates the logged-in user's own name and email. Nobody can update someone else's profile.</summary>
        /// <response code="200">Profile updated.</response>
        /// <response code="400">Validation error.</response>
        /// <response code="401">Not logged in.</response>
        [HttpPut("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateMyProfile(UpdateProfileDto dto)
        {
            var check = RequireLogin();
            if (check != null) return check;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FindAsync(CurrentUserId!.Value);
            if (user == null)
            {
                return Unauthorized(new { message = "Your session doesn't match a real account any more." });
            }

            var emailTaken = await _context.Users
                .AnyAsync(u => u.UserID != user.UserID && u.Email.ToLower() == dto.Email.ToLower());

            if (emailTaken)
            {
                return BadRequest(new { message = "That email is already used by another account." });
            }

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            await _context.SaveChangesAsync();

            return Ok(new UserProfileDto
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            });
        }
    }
}
