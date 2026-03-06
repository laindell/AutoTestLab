namespace server.Models
{
    public class GradeForTheTest
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int TestId { get; set; }
        public double Grade { get; set; }
        public DateTime GradedAt { get; set; }
        public User User { get; set; } = null!;
        public Test Test { get; set; } = null!;
    }
}