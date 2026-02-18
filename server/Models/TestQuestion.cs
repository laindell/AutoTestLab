namespace server.Models
{
    public class TestQuestion
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public string QuestionText { get; set; } = null!;
        public string OptionsJson { get; set; } = null!; 
        public List<string> Options { get; set; } = new List<string>();

        public int CorrectOptionIndex { get; set; } 
    }
}