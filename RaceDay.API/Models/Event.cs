using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models
{
    public class Event
    {
        [Key]
        public int EventID { get; set; }

        [Required]
        public int OrganiserID { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "date")]
        public DateTime EventDate { get; set; }

        [Required]
        [MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Required per the Part 2 brief's Event model (distance in kilometres,
        // e.g. 5, 10, 21.1, 42.2) and the type of event (e.g. Run, Walk, Cycle).
        [Column(TypeName = "decimal(6,2)")]
        public decimal DistanceKm { get; set; }

        [Required]
        [MaxLength(30)]
        public string EventType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(OrganiserID))]
        public User? Organiser { get; set; }

        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public RouteWeatherInfo? WeatherInfo { get; set; }
    }
}
