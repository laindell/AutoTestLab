using System.ComponentModel.DataAnnotations;

namespace server.Models
{
    public class User
    {
        public Guid Id { get; set; }

        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string Username { get; set; } = null!;

        public string FirstName { get; set; } = string.Empty; 
        public string LastName { get; set; } = string.Empty;  

        public DateTime TimeRegister { get; set; } 

        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "User";

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        public ICollection<GradeForTheTest> Grades { get; set; } = new List<GradeForTheTest>();
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    }
}