using AutoTestLab.Shared.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;
using server.Services.AI;
using server.Services.RAG;
using System.Security.Claims;
using System.Text.Json;

namespace server.Services.Grpc
{
    [Authorize]
    public class TestGrpcService : TestService.TestServiceBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RagService _ragService;
        private readonly IAIService _aiService;

        public TestGrpcService(ApplicationDbContext context, RagService ragService, IAIService aiService)
        {
            _context = context;
            _ragService = ragService;
            _aiService = aiService;
        }

        public override async Task<TestResponse> GenerateTest(GenerateTestRequest request, ServerCallContext context)
        {
            // Отримуємо контекст із файлу через RAG
            var contextText = await _ragService.GetContextAsync(new List<int> { request.FileId }, "Main concepts and definitions", limit: 10);

            if (string.IsNullOrEmpty(contextText))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "No content found for this file to generate test."));
            }

            // Генеруємо тест через OpenAI
            var jsonResponse = await _aiService.GenerateTestAsync(contextText, request.Language, request.Difficulty, request.QuestionCount);

            // Парсимо JSON, чистимо від markdown
            jsonResponse = jsonResponse.Replace("```json", "").Replace("```", "").Trim();

            var questionDtos = JsonSerializer.Deserialize<List<QuestionDto>>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (questionDtos == null || questionDtos.Count == 0)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to parse questions from AI response."));
            }

            var userIdString = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User ID not found in token."));
            }

            // Намагаємось розпарсити рівень складності
            Enum.TryParse<TestDifficulty>(request.Difficulty, true, out var parsedDifficulty);

            // Перетворюємо DTO на сутності бази даних
            var dbQuestions = questionDtos.Select(dto => new Models.TestQuestion
            {
                QuestionText = dto.QuestionText,
                OptionsJson = JsonSerializer.Serialize(dto.Options),
                Options = dto.Options,
                CorrectOptionIndex = dto.CorrectOptionIndex
            }).ToList();

            // Зберігаємо в БД
            var test = new Models.Test
            {
                Name = $"Test generated at {DateTime.Now:g}",
                Description = $"Difficulty: {request.Difficulty}",
                Difficulty = parsedDifficulty,
                CreatorId = Guid.Parse(userIdString),
                SourceFileId = request.FileId,
                Questions = dbQuestions,
                TestLanguage = request.Language // Зберігаємо мову тесту
            };

            _context.Tests.Add(test);
            await _context.SaveChangesAsync();

            // Формуємо відповідь для клієнта
            var response = new TestResponse { TestId = test.Id, Name = test.Name };

            foreach (var q in dbQuestions)
            {
                var qDto = new TestQuestionDto
                {
                    QuestionText = q.QuestionText,
                    CorrectOptionIndex = q.CorrectOptionIndex
                };
                qDto.Options.AddRange(q.Options);
                response.Questions.Add(qDto);
            }

            return response;
        }

        // Отримання списку тестів користувача
        public override async Task<TestListResponse> GetMyTests(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
        {
            var userIdString = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User ID not found in token."));
            }

            var userId = Guid.Parse(userIdString);

            var tests = await _context.Tests
                .Where(t => t.CreatorId == userId)
                .Select(t => new TestSummaryDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description ?? ""
                })
                .ToListAsync();

            var response = new TestListResponse();
            response.Tests.AddRange(tests);

            return response;
        }


        // Допоміжний метод перевірки прав доступу
        private async Task<bool> UserHasAccessToTestAsync(Guid userId, int testId)
        {
            // перевіряємо чи є користувач автором тесту
            var isCreator = await _context.Tests
                .AnyAsync(t => t.Id == testId && t.CreatorId == userId);

            if (isCreator) return true;

            //перевіряємо, чи розшарено тест у будь-яку групу де є цей користувач
            var hasGroupAccess = await _context.TestGroupShares
                .Where(tgs => tgs.TestId == testId)
                .Join(_context.GroupMembers,
                      tgs => tgs.GroupId,
                      gm => gm.GroupId,
                      (tgs, gm) => gm)
                .AnyAsync(gm => gm.UserId == userId);

            return hasGroupAccess;
        }

        // Отримання тесту для проходження
        public override async Task<TakeTestResponse> GetTestForTaking(GetTestRequest request, ServerCallContext context)
        {
            var userIdString = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User ID not found in token."));
            }
            var userId = Guid.Parse(userIdString);

            if (!await UserHasAccessToTestAsync(userId, request.TestId))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "You do not have permission to access this test."));
            }

            var test = await _context.Tests
                .Include(t => t.Questions)
                .FirstOrDefaultAsync(t => t.Id == request.TestId);

            if (test == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Test not found."));
            }

            var response = new TakeTestResponse
            {
                TestId = test.Id,
                Name = test.Name,
                Description = test.Description ?? "",
                TimeLimitSeconds = test.TimeLimitSeconds
            };

            foreach (var q in test.Questions)
            {
                var questionDto = new TakeTestQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText
                };

                // Якщо Options порожній (наприклад, дані лише в JSON)
                var options = q.Options != null && q.Options.Any()
                    ? q.Options
                    : JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? new List<string>();

                questionDto.Options.AddRange(options);

                response.Questions.Add(questionDto);
            }

            return response;
        }

        // відправка відповідей перевірка та збереження оцінки
        public override async Task<SubmitTestResponse> SubmitTestAnswers(SubmitTestRequest request, ServerCallContext context)
        {
            var userIdString = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User ID not found in token."));
            }
            var userId = Guid.Parse(userIdString);

            if (!await UserHasAccessToTestAsync(userId, request.TestId))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "You do not have permission to submit answers for this test."));
            }

            var test = await _context.Tests
                .Include(t => t.Questions)
                .FirstOrDefaultAsync(t => t.Id == request.TestId);

            if (test == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Test not found."));
            }

            int correctAnswersCount = 0;
            int totalQuestions = test.Questions.Count;

            if (totalQuestions == 0)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "Test has no questions."));
            }

            var questionsDict = test.Questions.ToDictionary(q => q.Id);

            foreach (var userAnswer in request.Answers)
            {
                if (questionsDict.TryGetValue(userAnswer.QuestionId, out var dbQuestion))
                {
                    if (dbQuestion.CorrectOptionIndex == userAnswer.SelectedOptionIndex)
                    {
                        correctAnswersCount++;
                    }
                }
            }

            double finalGrade = (double)correctAnswersCount / totalQuestions * 100.0;

            var gradeRecord = new GradeForTheTest
            {
                UserId = userId,
                TestId = test.Id,
                Grade = finalGrade,
                GradedAt = DateTime.UtcNow
            };

            // Зберігаємо оцінку
            _context.GradesForTheTests.Add(gradeRecord);
            await _context.SaveChangesAsync();

            return new SubmitTestResponse
            {
                Grade = finalGrade,
                CorrectAnswers = correctAnswersCount,
                TotalQuestions = totalQuestions
            };
        }
    }

    // Клас DTO для безпечної десеріалізації відповіді від ШІ
    public class QuestionDto
    {
        public string QuestionText { get; set; } = null!;
        public List<string> Options { get; set; } = new();
        public int CorrectOptionIndex { get; set; }
    }
}