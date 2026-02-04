namespace server.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!; 
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "User";

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        public ICollection<GradeForTheTest> Grades { get; set; } = new List<GradeForTheTest>();
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>(); 
    }
}