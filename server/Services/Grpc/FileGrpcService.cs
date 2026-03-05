using AutoTestLab.Shared.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;
using server.Services.RAG;
using System.Security.Claims;
using System.Text;
using UglyToad.PdfPig;

namespace server.Services.Grpc
{
    [Authorize] // Тільки для авторизованих
    public class FileGrpcService : FileService.FileServiceBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FileGrpcService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebHostEnvironment _env;

        public FileGrpcService(
            ApplicationDbContext context,
            ILogger<FileGrpcService> logger,
            IServiceScopeFactory scopeFactory,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _env = env;
        }

        public override async Task<UploadFileResponse> UploadFile(IAsyncStreamReader<UploadFileRequest> requestStream, ServerCallContext context)
        {
            var userId = Guid.Parse(context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            var memoryStream = new MemoryStream();
            string fileName = "unknown.txt";

            // Читання потоку даних від клієнта
            await foreach (var chunk in requestStream.ReadAllAsync())
            {
                if (!string.IsNullOrEmpty(chunk.FileName))
                    fileName = chunk.FileName;

                if (chunk.Content.Length > 0)
                    await memoryStream.WriteAsync(chunk.Content.ToByteArray());
            }

            var uploadsFolder = Path.Combine(_env.ContentRootPath, "Uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            
            var sanitizedFileName = Path.GetFileName(fileName); 

            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                sanitizedFileName = "Invalid fail name";
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Зберігаємо запис про файл зі статусом Processing
            var fileEntity = new UsersFiles
            {
                UserId = userId,
                FileName = fileName,
                FilePath = filePath,
                UploadedAt = DateTime.UtcNow,
                Status = FileStatus.Processing
            };

            _context.UsersFiles.Add(fileEntity);
            await _context.SaveChangesAsync();

            try
            {
                await File.WriteAllBytesAsync(filePath, memoryStream.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file {FileName}", fileName);
                fileEntity.Status = FileStatus.Error;
                await _context.SaveChangesAsync();
                return new UploadFileResponse { FileId = fileEntity.Id, Status = "Error saving file" };
            }

            string textContent = string.Empty;
            var extension = Path.GetExtension(fileName).ToLower();

            try
            {
                if (extension == ".pdf")
                {
                    using var pdfDocument = PdfDocument.Open(memoryStream.ToArray());
                    var textBuilder = new StringBuilder();
                    foreach (var page in pdfDocument.GetPages())
                    {
                        textBuilder.AppendLine(page.Text);
                    }
                    textContent = textBuilder.ToString();
                }
                else
                {
                    memoryStream.Position = 0;
                    using var reader = new StreamReader(memoryStream);
                    textContent = await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            { 
                _logger.LogError(ex, "Failed to extract text from file {FileName}", fileName);
                fileEntity.Status = FileStatus.Error;
                await _context.SaveChangesAsync();
                return new UploadFileResponse { FileId = fileEntity.Id, Status = "Error parsing file format" };
            }

            _ = Task.Run(async () =>
            {
     
                using var scope = _scopeFactory.CreateScope();

                // Отримуємо сервіси всередині фонового потоку
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var ragService = scope.ServiceProvider.GetRequiredService<RagService>();

                try
                {
                    // Обробляємо файл
                    await ragService.ProcessFileAsync(fileEntity.Id, textContent);

                    // Важливо: заново дістаємо сутність в НОВОМУ контексті БД перед оновленням
                    var fileToUpdate = await db.UsersFiles.FindAsync(fileEntity.Id);
                    if (fileToUpdate != null)
                    {
                        fileToUpdate.Status = FileStatus.Ready;
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Якщо сталася помилка, теж варто оновити статус
                    var fileToUpdate = await db.UsersFiles.FindAsync(fileEntity.Id);
                    if (fileToUpdate != null)
                    {
                        fileToUpdate.Status = FileStatus.Error; 
                        await db.SaveChangesAsync();
                    }
                    _logger.LogError(ex, "RAG processing failed for file {FileId}", fileEntity.Id);
                }
            });

            // Відразу повертаємо клієнту, що файл прийнято в роботу
            return new UploadFileResponse { FileId = fileEntity.Id, Status = "Processing" };
        }

        public override async Task<FileListResponse> GetMyFiles(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
        {
            var userId = Guid.Parse(context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            var files = await _context.UsersFiles
                .Where(f => f.UserId == userId)
                .Select(f => new FileDto
                {
                    Id = f.Id,
                    Name = f.FileName,
                    Status = f.Status.ToString(),
                    UploadedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(f.UploadedAt.ToUniversalTime())
                })
                .ToListAsync();

            var response = new FileListResponse();
            response.Files.AddRange(files);
            return response;
        }
    }
}
