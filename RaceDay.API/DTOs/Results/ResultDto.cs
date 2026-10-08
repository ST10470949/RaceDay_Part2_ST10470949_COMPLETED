namespace RaceDay.API.DTOs.Results
{
    public class ResultDto
    {
        public int ResultID { get; set; }
        public int EnrolmentID { get; set; }
        public string? ParticipantName { get; set; }
        public string? EventName { get; set; }
        public string? CategoryName { get; set; }
        public TimeSpan? FinishTime { get; set; }
        public int? Position { get; set; }
    }
}
