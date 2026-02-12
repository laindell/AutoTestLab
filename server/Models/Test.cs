namespace server.Models
{
    public enum TestDifficulty { Easy, Medium, Hard }

    public class Test
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string TestLanguage { get; set; } = null!;

        public TestDifficulty Difficulty { get; set; } 
        public int TimeLimitSeconds { get; set; } = 600; 
        public Guid CreatorId { get; set; } 
        public User Creator { get; set; } = null!;
        public int? SourceFileId { get; set; } 
        public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
        public ICollection<GradeForTheTest> Grades { get; set; } = new List<GradeForTheTest>();
        public int? GroupId { get; set; }
    }
}