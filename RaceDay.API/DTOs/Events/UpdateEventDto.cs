using System.ComponentModel.DataAnnotations;

namespace RaceDay.API.DTOs.Events
{
    public class UpdateEventDto
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0.1, 1000)]
        public decimal DistanceKm { get; set; }

        [Required]
        [MaxLength(30)]
        public string EventType { get; set; } = string.Empty;
    }
}
