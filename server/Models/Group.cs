namespace server.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string JoinCode { get; set; } = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
    public class GroupMember
    {
        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}