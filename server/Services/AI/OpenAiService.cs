using OpenAI.Chat;
using OpenAI.Embeddings;

namespace server.Services.AI
{
    public class OpenAiService : IAIService
    {
        private readonly ChatClient _chatClient;
        private readonly EmbeddingClient _embeddingClient;

        public OpenAiService(IConfiguration configuration)
        {
            var apiKey = configuration["OpenAI:ApiKey"]
                         ?? throw new ArgumentNullException("OpenAI API Key is missing"); //TODO: додати логування помилок + згенерувати ключ 

            // сlient initialization 
            _chatClient = new ChatClient("gpt-4o", apiKey);
            _embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            // obtaining a vector representation of text for RAG
            OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(text);
            return embedding.ToFloats().ToArray();
        }

        public async Task<string> GenerateTestAsync(string context, string difficulty, int questionCount)
        {
            // forming a prompt for generating a test based on the found context
            string prompt = $"""
            На основі наступного тексту документації, згенеруй тест.
            Складність: {difficulty}.
            Кількість питань: {questionCount}.
            Контекст: {context}
            """
            +
            $$"""
            Відповідь надай СУВОРО у форматі JSON:
            [
              {
                    "QuestionText": "Текст питання",
                "Options": ["Варіант 1", "Варіант 2", "Варіант 3", "Варіант 4"],
                "CorrectOptionIndex": 0
              }
            ]
            """;

            ChatCompletion completion = await _chatClient.CompleteChatAsync(prompt);
            return completion.Content[0].Text;
        }

    }
}