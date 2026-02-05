using server.Data;
using server.Models;
using server.Services.AI;

namespace server.Services.RAG
{
    public class TestGenerator(RagService ragService, IAIService aiService, ApplicationDbContext dbContext)
    {
        public async Task<Test> CreateTestAsync(Guid userId, List<int> fileIds, string topic, string difficulty, int questionsCount, int timeLimit)
        {
            // get context specifically from selected documents
            var context = await ragService.GetContextAsync(fileIds, topic);

            // generate questions through AI
            var rawJsonTest = await aiService.GenerateTestAsync(context, difficulty, questionsCount);

            // creating a test object based on the model
            var test = new Test
            {
                Name = $"Тест на тему: {topic}",
                Description = $"Згенеровано автоматично. Складність: {difficulty}",
                Difficulty = Enum.Parse<TestDifficulty>(difficulty),
                TimeLimitSeconds = timeLimit,
                CreatorId = userId,
                //TODO: додати тре логіку парсингу JSON від ШІ в ICollection<TestQuestion>
            };

            dbContext.Tests.Add(test);
            await dbContext.SaveChangesAsync();
            return test;
        }
    }
}
