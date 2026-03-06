namespace server.Models
{
    public class TestGroupShare
    {
        public int TestId { get; set; }
        public Test Test { get; set; } = null!; 

        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        public DateTime SharedAt { get; set; }
    }
}
