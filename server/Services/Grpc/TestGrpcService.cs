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

            // Намагаємось розпарсити рівень складності у ваш Enum (Easy, Medium, Hard)
            Enum.TryParse<TestDifficulty>(request.Difficulty, true, out var parsedDifficulty);

            // Перетворюємо DTO на сутності бази даних
            var dbQuestions = questionDtos.Select(dto => new Models.TestQuestion
            {
                QuestionText = dto.QuestionText,
                OptionsJson = JsonSerializer.Serialize(dto.Options), // Зберігаємо як JSON-рядок для БД
                Options = dto.Options, // Заповнюємо in-memory список (якщо потрібно)
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
                Questions = dbQuestions
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

        // отримання списку тестів користувача
        public override async Task<TestListResponse> GetMyTests(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
        {
            // ID користувача з токена
            var userIdString = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User ID not found in token."));
            }

            var userId = Guid.Parse(userIdString);

            // Шукає тести створені цим користувачем і відразу мапимо у DTO з App.proto
            var tests = await _context.Tests
                .Where(t => t.CreatorId == userId)
                .Select(t => new TestSummaryDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description ?? "" 
                })
                .ToListAsync();

            // складає відповідь
            var response = new TestListResponse();
            response.Tests.AddRange(tests);

            return response;
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