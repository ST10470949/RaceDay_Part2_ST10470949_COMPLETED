using System.ComponentModel.DataAnnotations;

namespace RaceDay.API.DTOs.Enrolments
{
    public class UpdateEnrolmentStatusDto
    {
        // Expected values: "Pending", "Confirmed", "Cancelled".
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}
