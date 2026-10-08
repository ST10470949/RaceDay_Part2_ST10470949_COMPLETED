using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs.Enrolments;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    /// <summary>
    /// This is where a Participant actually enters a race. An Organiser has no reason
    /// to be enrolling in their own events, so every action here is Participant-only.
    /// </summary>
    [Route("api/enrolments")]
    public class EnrolmentsController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public EnrolmentsController(RaceDayContext context)
        {
            _context = context;
        }

        /// <summary>Enrols the logged-in Participant into a category.</summary>
        /// <response code="201">Enrolment created.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Logged in, but not a Participant.</response>
        /// <response code="404">Category doesn't exist.</response>
        /// <response code="409">Already enrolled in this category, or it's full.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Enrol(CreateEnrolmentDto dto)
        {
            var check = RequireRole("Participant");
            if (check != null) return check;

            var category = await _context.Categories
                .Include(c => c.Event)
                .Include(c => c.Enrolments)
                .FirstOrDefaultAsync(c => c.CategoryID == dto.CategoryId);

            if (category == null)
            {
                return NotFound(new { message = "That category doesn't exist." });
            }

            var alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e => e.ParticipantID == CurrentUserId!.Value && e.CategoryID == dto.CategoryId);

            if (alreadyEnrolled)
            {
                return Conflict(new { message = "You're already enrolled in this category." });
            }

            if (category.Enrolments.Count >= category.MaxParticipants)
            {
                return Conflict(new { message = "This category is full." });
            }

            var enrolment = new Enrolment
            {
                ParticipantID = CurrentUserId!.Value,
                CategoryID = dto.CategoryId,
                EnrolmentDate = DateTime.UtcNow,
                Status = EnrolmentStatus.Confirmed
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new EnrolmentDto
            {
                EnrolmentID = enrolment.EnrolmentID,
                CategoryID = category.CategoryID,
                CategoryName = category.Name,
                EventID = category.EventID,
                EventName = category.Event!.Name,
                ParticipantID = enrolment.ParticipantID,
                EnrolmentDate = enrolment.EnrolmentDate,
                Status = enrolment.Status.ToString()
            });
        }

        /// <summary>Lists all of the logged-in Participant's own enrolments.</summary>
        /// <response code="200">Array of enrolments.</response>
        /// <response code="401">Not logged in.</response>
        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var check = RequireRole("Participant");
            if (check != null) return check;

            var enrolments = await _context.Enrolments
                .Include(e => e.Category).ThenInclude(c => c!.Event)
                .Where(e => e.ParticipantID == CurrentUserId!.Value)
                .Select(e => new EnrolmentDto
                {
                    EnrolmentID = e.EnrolmentID,
                    CategoryID = e.CategoryID,
                    CategoryName = e.Category!.Name,
                    EventID = e.Category.EventID,
                    EventName = e.Category.Event!.Name,
                    ParticipantID = e.ParticipantID,
                    EnrolmentDate = e.EnrolmentDate,
                    Status = e.Status.ToString()
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>Updates an enrolment's status (Pending/Confirmed/Cancelled). Only the
        /// Organiser who owns the event behind this enrolment can do this.</summary>
        /// <response code="200">Status updated.</response>
        /// <response code="400">Status value not recognised.</response>
        /// <response code="403">Not the owner of the event this enrolment belongs to.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpPut("{id:int}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEnrolmentStatus(int id, UpdateEnrolmentStatusDto dto)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!Enum.TryParse<EnrolmentStatus>(dto.Status, true, out var parsedStatus))
            {
                return BadRequest(new { message = "Status must be 'Pending', 'Confirmed' or 'Cancelled'." });
            }

            var enrolment = await _context.Enrolments
                .Include(e => e.Category).ThenInclude(c => c!.Event)
                .Include(e => e.Participant)
                .FirstOrDefaultAsync(e => e.EnrolmentID == id);

            if (enrolment == null)
            {
                return NotFound(new { message = "Enrolment not found." });
            }

            if (enrolment.Category!.Event!.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "You can only manage enrolments for your own events." });
            }

            enrolment.Status = parsedStatus;
            await _context.SaveChangesAsync();

            return Ok(new EnrolmentDto
            {
                EnrolmentID = enrolment.EnrolmentID,
                CategoryID = enrolment.CategoryID,
                CategoryName = enrolment.Category.Name,
                EventID = enrolment.Category.EventID,
                EventName = enrolment.Category.Event.Name,
                ParticipantID = enrolment.ParticipantID,
                ParticipantName = enrolment.Participant?.FullName,
                EnrolmentDate = enrolment.EnrolmentDate,
                Status = enrolment.Status.ToString()
            });
        }

        /// <summary>Cancels the logged-in Participant's own enrolment.</summary>
        /// <response code="204">Cancelled.</response>
        /// <response code="403">Trying to cancel someone else's enrolment.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelEnrolment(int id)
        {
            var check = RequireRole("Participant");
            if (check != null) return check;

            var enrolment = await _context.Enrolments.FindAsync(id);
            if (enrolment == null)
            {
                return NotFound(new { message = "Enrolment not found." });
            }

            if (enrolment.ParticipantID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only cancel your own enrolments." });
            }

            _context.Enrolments.Remove(enrolment);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
