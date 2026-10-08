using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs.Categories;
using RaceDay.API.DTOs.Enrolments;
using RaceDay.API.DTOs.Events;
using RaceDay.API.DTOs.Results;
using RaceDay.API.DTOs.Weather;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    /// <summary>
    /// Events are owned by an Organiser. Anyone can browse events and categories,
    /// but only the Organiser who created an event can edit it, add categories to it,
    /// or see who has enrolled and what their results were.
    /// </summary>
    [Route("api/events")]
    public class EventsController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public EventsController(RaceDayContext context)
        {
            _context = context;
        }

        /// <summary>Lists all events. Supports optional filtering by location and date.</summary>
        /// <response code="200">Array of events.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEvents([FromQuery] string? location, [FromQuery] DateTime? date)
        {
            var query = _context.Events.Include(e => e.Organiser).AsQueryable();

            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(e => e.Location.ToLower().Contains(location.ToLower()));
            }

            if (date.HasValue)
            {
                query = query.Where(e => e.EventDate.Date == date.Value.Date);
            }

            var events = await query
                .OrderBy(e => e.EventDate)
                .Select(e => new EventDto
                {
                    EventID = e.EventID,
                    Name = e.Name,
                    EventDate = e.EventDate,
                    Location = e.Location,
                    Description = e.Description,
                    DistanceKm = e.DistanceKm,
                    EventType = e.EventType,
                    OrganiserID = e.OrganiserID,
                    OrganiserName = e.Organiser!.FullName
                })
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>Lists only the events owned by the logged-in Organiser.</summary>
        /// <response code="200">Array of the Organiser's own events.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Logged in, but not an Organiser.</response>
        [HttpGet("mine")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyEvents()
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            var events = await _context.Events
                .Include(e => e.Organiser)
                .Where(e => e.OrganiserID == CurrentUserId!.Value)
                .OrderBy(e => e.EventDate)
                .Select(e => new EventDto
                {
                    EventID = e.EventID,
                    Name = e.Name,
                    EventDate = e.EventDate,
                    Location = e.Location,
                    Description = e.Description,
                    DistanceKm = e.DistanceKm,
                    EventType = e.EventType,
                    OrganiserID = e.OrganiserID,
                    OrganiserName = e.Organiser!.FullName
                })
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>Returns one event with its categories.</summary>
        /// <response code="200">Event details.</response>
        /// <response code="404">No event with that id.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvent(int id)
        {
            var ev = await _context.Events
                .Include(e => e.Organiser)
                .Include(e => e.Categories)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            return Ok(new
            {
                ev.EventID,
                ev.Name,
                ev.EventDate,
                ev.Location,
                ev.Description,
                ev.DistanceKm,
                ev.EventType,
                ev.OrganiserID,
                OrganiserName = ev.Organiser?.FullName,
                Categories = ev.Categories.Select(c => new CategoryDto
                {
                    CategoryID = c.CategoryID,
                    EventID = c.EventID,
                    Name = c.Name,
                    EntryFee = c.EntryFee,
                    MaxParticipants = c.MaxParticipants
                })
            });
        }

        /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
        /// <response code="201">Event created.</response>
        /// <response code="400">Validation error.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Logged in, but not an Organiser.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateEvent(CreateEventDto dto)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newEvent = new Event
            {
                OrganiserID = CurrentUserId!.Value,
                Name = dto.Name,
                EventDate = dto.EventDate,
                Location = dto.Location,
                Description = dto.Description,
                DistanceKm = dto.DistanceKm,
                EventType = dto.EventType
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEvent), new { id = newEvent.EventID }, new EventDto
            {
                EventID = newEvent.EventID,
                Name = newEvent.Name,
                EventDate = newEvent.EventDate,
                Location = newEvent.Location,
                Description = newEvent.Description,
                DistanceKm = newEvent.DistanceKm,
                EventType = newEvent.EventType,
                OrganiserID = newEvent.OrganiserID
            });
        }

        /// <summary>Updates an event. Only the Organiser who owns it can do this.</summary>
        /// <response code="200">Event updated.</response>
        /// <response code="403">Not the owner (or not an Organiser at all).</response>
        /// <response code="404">Event not found.</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(int id, UpdateEventDto dto)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (ev.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit your own events." });
            }

            ev.Name = dto.Name;
            ev.EventDate = dto.EventDate;
            ev.Location = dto.Location;
            ev.Description = dto.Description;
            ev.DistanceKm = dto.DistanceKm;
            ev.EventType = dto.EventType;
            await _context.SaveChangesAsync();

            return Ok(new EventDto
            {
                EventID = ev.EventID,
                Name = ev.Name,
                EventDate = ev.EventDate,
                Location = ev.Location,
                Description = ev.Description,
                DistanceKm = ev.DistanceKm,
                EventType = ev.EventType,
                OrganiserID = ev.OrganiserID
            });
        }

        /// <summary>Deletes an event. Only the owning Organiser can do this.</summary>
        /// <response code="204">Deleted.</response>
        /// <response code="403">Not the owner.</response>
        /// <response code="404">Event not found.</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (ev.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete your own events." });
            }

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>Lists categories for a given event. Open to everyone.</summary>
        /// <response code="200">Array of categories.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id:int}/categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategories(int id)
        {
            var eventExists = await _context.Events.AnyAsync(e => e.EventID == id);
            if (!eventExists)
            {
                return NotFound(new { message = "Event not found." });
            }

            var categories = await _context.Categories
                .Where(c => c.EventID == id)
                .Select(c => new CategoryDto
                {
                    CategoryID = c.CategoryID,
                    EventID = c.EventID,
                    Name = c.Name,
                    EntryFee = c.EntryFee,
                    MaxParticipants = c.MaxParticipants
                })
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>Adds a category (e.g. 10km, 21km) to an event. Organiser-owner only.</summary>
        /// <response code="201">Category created.</response>
        /// <response code="403">Not the event owner.</response>
        /// <response code="404">Event not found.</response>
        [HttpPost("{id:int}/categories")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddCategory(int id, CreateCategoryDto dto)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (ev.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only add categories to your own events." });
            }

            var category = new Category
            {
                EventID = id,
                Name = dto.Name,
                EntryFee = dto.EntryFee,
                MaxParticipants = dto.MaxParticipants
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new CategoryDto
            {
                CategoryID = category.CategoryID,
                EventID = category.EventID,
                Name = category.Name,
                EntryFee = category.EntryFee,
                MaxParticipants = category.MaxParticipants
            });
        }

        /// <summary>Lists everyone enrolled across every category of this event. Organiser-owner only.</summary>
        /// <response code="200">Array of enrolments.</response>
        /// <response code="403">Not the event owner.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id:int}/enrolments")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventEnrolments(int id)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (ev.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view enrolments for your own events." });
            }

            var enrolments = await _context.Enrolments
                .Include(e => e.Category)
                .Include(e => e.Participant)
                .Where(e => e.Category!.EventID == id)
                .Select(e => new EnrolmentDto
                {
                    EnrolmentID = e.EnrolmentID,
                    CategoryID = e.CategoryID,
                    CategoryName = e.Category!.Name,
                    EventID = id,
                    EventName = ev.Name,
                    ParticipantID = e.ParticipantID,
                    ParticipantName = e.Participant!.FullName,
                    EnrolmentDate = e.EnrolmentDate,
                    Status = e.Status.ToString()
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>Lists all captured results for this event. Organiser-owner only.</summary>
        /// <response code="200">Array of results.</response>
        /// <response code="403">Not the event owner.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id:int}/results")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventResults(int id)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (ev.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view results for your own events." });
            }

            var results = await _context.Results
                .Include(r => r.Enrolment).ThenInclude(e => e!.Category)
                .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
                .Where(r => r.Enrolment!.Category!.EventID == id)
                .Select(r => new ResultDto
                {
                    ResultID = r.ResultID,
                    EnrolmentID = r.EnrolmentID,
                    ParticipantName = r.Enrolment!.Participant!.FullName,
                    EventName = ev.Name,
                    CategoryName = r.Enrolment.Category!.Name,
                    FinishTime = r.FinishTime,
                    Position = r.Position
                })
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>Returns weather/route info for race-day prep. Open to everyone.</summary>
        /// <response code="200">Weather info.</response>
        /// <response code="404">No weather info recorded yet, or event doesn't exist.</response>
        [HttpGet("{id:int}/weather")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetWeather(int id)
        {
            var info = await _context.RouteWeatherInfos.FirstOrDefaultAsync(w => w.EventID == id);
            if (info == null)
            {
                return NotFound(new { message = "No weather/route info for this event yet." });
            }

            return Ok(new WeatherDto
            {
                EventID = info.EventID,
                WeatherForecast = info.WeatherForecast,
                RouteMapUrl = info.RouteMapURL,
                UpdatedAt = info.UpdatedAt
            });
        }

        /// <summary>Adds or updates weather/route info for an event. Organiser-owner only.</summary>
        /// <response code="200">Updated existing weather info.</response>
        /// <response code="201">Created new weather info.</response>
        /// <response code="403">Not the event owner.</response>
        [HttpPost("{id:int}/weather")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetWeather(int id, CreateWeatherDto dto)
        {
            var check = RequireRole("Organiser");
            if (check != null) return check;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound(new { message = "Event not found." });
            }

            if (ev.OrganiserID != CurrentUserId!.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only update weather info for your own events." });
            }

            var info = await _context.RouteWeatherInfos.FirstOrDefaultAsync(w => w.EventID == id);
            var isNew = info == null;

            if (info == null)
            {
                info = new RouteWeatherInfo { EventID = id };
                _context.RouteWeatherInfos.Add(info);
            }

            info.WeatherForecast = dto.WeatherForecast;
            info.RouteMapURL = dto.RouteMapUrl;
            info.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var result = new WeatherDto
            {
                EventID = info.EventID,
                WeatherForecast = info.WeatherForecast,
                RouteMapUrl = info.RouteMapURL,
                UpdatedAt = info.UpdatedAt
            };

            return isNew ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
        }
    }
}
