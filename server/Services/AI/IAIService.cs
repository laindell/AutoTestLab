namespace server.Services.AI
{
    public interface IAIService
    {
        Task<float[]> GetEmbeddingAsync(string text);
        Task<string> GenerateTestAsync(string context, string testlanguage, string difficulty, int questionCount);
    }
}
