using server.Data;
using server.Models;
using server.Services.AI;
using System.Text.Json;

namespace server.Services.RAG
{
    public class TestGenerator(RagService ragService, IAIService aiService, ApplicationDbContext dbContext)
    {
        public async Task<Test> CreateTestAsync(Guid userId, List<int> fileIds, string topic, string difficulty, int questionsCount, int timeLimit, string testlanguage)
        {
            // get context specifically from selected documents
            var context = await ragService.GetContextAsync(fileIds, topic);

            // generate questions through AI
            var rawJsonTest = await aiService.GenerateTestAsync(context, testlanguage, difficulty, questionsCount);

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

            try
            {// Оскільки AI може повернути текст з Markdown (```json ... ```), чистимо його
                var cleanJson = rawJsonTest.Replace("```json", "").Replace("```", "").Trim();

                var questionsDtos = JsonSerializer.Deserialize<List<QuestionDto>>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (questionsDtos != null)
                {
                    foreach (var qDto in questionsDtos)
                    {
                        test.Questions.Add(
                        new TestQuestion
                            {
                                Text = qDto.QuestionText,
                                // Припускаємо, що Options зберігаються як JSON string або окрема таблиця. 
                                // Тут для прикладу з'єднуємо в рядок, але краще мати окрему сутність AnswerOption
                                OptionsJson = JsonSerializer.Serialize(qDto.Options),
                                CorrectOptionIndex = qDto.CorrectOptionIndex
                            }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                // Логування помилки парсингу, якщо AI повернув "битий" JSON
                Console.WriteLine($"Error parsing AI response: {ex.Message}");
            }

            dbContext.Tests.Add(test);
            await dbContext.SaveChangesAsync();
            return test;
        }
    }

    public class QuestionDto
    {
        public string QuestionText { get; set; } = null!;
        public List<string> Options { get; set; }   
        public int CorrectOptionIndex { get; set; }
    }
}
