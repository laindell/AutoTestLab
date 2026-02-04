using Pgvector; 
using System.ComponentModel.DataAnnotations.Schema;

namespace server.Models
{
    public class DocumentChunk
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public UsersFiles File { get; set; } = null!;
        public string Content { get; set; } = null!;
        [Column(TypeName = "vector(1536)")]
        public Vector? Embedding { get; set; }
    }
}