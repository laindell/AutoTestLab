using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using server.Data;
using server.Models;
using server.Services.AI;
using System.Collections.Concurrent;

namespace server.Services.RAG
{
    public class RagService(ApplicationDbContext dbContext, IAIService aiService)
    {
        // concurrent file processing
        public async Task ProcessFileAsync(int fileId, string text)
        {
            var splitter = new TextSplitter(maxSize: 1000, overlap: 200);
            var chunks = splitter.SplitText(text); // get quality chunks

            var documentChunks = new ConcurrentBag<DocumentChunk>();

            // concurrent generation of embeddings
            await Parallel.ForEachAsync(chunks, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (chunkText, ct) =>
            {
                // get the vector through your AI service
                var embeddingArray = await aiService.GetEmbeddingAsync(chunkText);

                documentChunks.Add(new DocumentChunk
                {
                    FileId = fileId,
                    Content = chunkText, // save the chunk text itself
                    Embedding = new Vector(embeddingArray) // vector for search
                });
            });

            // batch saving to database for speed
            dbContext.DocumentChunks.AddRange(documentChunks);
            await dbContext.SaveChangesAsync();
        }

        // context search for selected files
        public async Task<string> GetContextAsync(List<int> selectedFileIds, string query, int limit = 5)
        {
            var queryEmbedding = new Vector(await aiService.GetEmbeddingAsync(query));

            // vector search with filtering by user selected files
            var relevantChunks = await dbContext.DocumentChunks
                .Where(c => selectedFileIds.Contains(c.FileId))
                .OrderBy(c => c.Embedding!.L2Distance(queryEmbedding))
                .Take(limit)
                .Select(c => c.Content)
                .ToListAsync();

            return string.Join("\n\n", relevantChunks);
        }
    }
}
