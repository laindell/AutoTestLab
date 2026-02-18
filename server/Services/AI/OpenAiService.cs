using OpenAI.Chat;
using OpenAI.Embeddings;

namespace server.Services.AI
{
    public class OpenAiService : IAIService
    {
        private readonly ChatClient _chatClient;
        private readonly EmbeddingClient _embeddingClient;
        private readonly ILogger<OpenAiService> _logger;

        public OpenAiService(IConfiguration configuration, ILogger<OpenAiService> logger)
        {
            _logger = logger;

            var apiKey = configuration["OpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "placeholder")
            {
                var errorMsg = "OpenAI API Key is missing. Please set 'OpenAI__ApiKey' environment variable.";
                _logger.LogCritical(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            try
            {
            // сlient initialization 
            _chatClient = new ChatClient("gpt-4o", apiKey);
            _embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
        }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize OpenAI clients.");
                throw;
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
            OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(text);
            return embedding.ToFloats().ToArray();
        }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding.");
                throw;
            }
        }

        public async Task<string> GenerateTestAsync(string context, string testlanguage, string difficulty, int questionCount)
        {
            var prompt = $"""
            Based on the following documentation text, generate a test.
            Difficulty: {difficulty}.
            Number of questions: {questionCount}.
            Context: {context}
            The question text must be in {testlanguage}.
            """
            +
           $$"""
            Please provide your answer STRICTLY in JSON format:
            [
                {
                    "QuestionText": "Question text",
                    "Options": ["Option 1", "Option 2", "Option 3", "Option 4"],
                    "CorrectOptionIndex": 0
                }
            ]
            """;

            try
            {
            ChatCompletion completion = await _chatClient.CompleteChatAsync(prompt);
            return completion.Content[0].Text;
        }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating test.");
                throw;
            }
        }
    }
}