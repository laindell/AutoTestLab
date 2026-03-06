namespace server.Models
{
    public enum FileStatus { Uploaded, Processing, Ready, Failed, Error }

    public class UsersFiles
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!; 
        public User User { get; set; } = null!;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public FileStatus Status { get; set; } = FileStatus.Uploaded;
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}