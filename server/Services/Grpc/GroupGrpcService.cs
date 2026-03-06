using AutoTestLab.Shared.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;
using System.Security.Claims;
using System.Security.Cryptography;

namespace server.Services.Grpc
{
    [Authorize]
    public class GroupGrpcService : GroupService.GroupServiceBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroupGrpcService> _logger;

        public GroupGrpcService(ApplicationDbContext context, ILogger<GroupGrpcService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override async Task<GroupResponse> CreateGroup(CreateGroupRequest request, ServerCallContext context)
        {
            var userId = GetUserIdOrThrow(context);

            // generate a secure, unique join code
            var joinCode = await GenerateUniqueJoinCodeAsync();

            var group = new Group
            {
                Name = request.Name,
                OwnerId = userId,
                JoinCode = joinCode
            };


            var groupMember = new GroupMember
            {
                Group = group,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };

            _context.Groups.Add(group);
            _context.GroupMembers.Add(groupMember);

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} created group {GroupName}", userId, group.Name);


            return new GroupResponse
            {
                GroupId = group.Id,
                Name = group.Name,
                JoinCode = group.JoinCode
            };
        }

        public override async Task<GroupResponse> JoinGroup(JoinGroupRequest request, ServerCallContext context)
        {
            var userId = GetUserIdOrThrow(context);

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.JoinCode == request.JoinCode.ToUpper());
            if (group == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Групу не знайдено або код невірний."));
            }

            var existingMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == userId);
            if (existingMember)
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists, "Ви вже є учасником цієї групи."));
            }

            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };

            _context.GroupMembers.Add(groupMember);
            await _context.SaveChangesAsync();

            return new GroupResponse
            {
                GroupId = group.Id,
                Name = group.Name,
                JoinCode = group.JoinCode
            };
        }

        public override async Task<TestListResponse> GetGroupTests(GetGroupTestsRequest request, ServerCallContext context)
        {
            var userId = GetUserIdOrThrow(context);

            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == userId);
            if (!isMember)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Ви не є учасником цієї групи."));
            }

            // updated to query through a many-to-many relationship (assuming a TestGroupShares table)
            var tests = await _context.TestGroupShares
                .Where(tgs => tgs.GroupId == request.GroupId)
                .Select(tgs => tgs.Test) // Assuming navigation property to the Test
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

        public override async Task<Google.Protobuf.WellKnownTypes.Empty> ShareTest(ShareTestRequest request, ServerCallContext context)
        {
            var userId = GetUserIdOrThrow(context);

            var test = await _context.Tests.FindAsync(request.TestId);
            if (test == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Тест не знайдено."));
            }

            if (test.CreatorId != userId)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Тільки автор може поділитися цим тестом."));
            }

            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == userId);
            if (!isMember)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Ви не можете поділитися тестом у групу, до якої не належите."));
            }

            // prevent duplicate sharing to the same group
            var alreadyShared = await _context.TestGroupShares
                .AnyAsync(tgs => tgs.TestId == request.TestId && tgs.GroupId == request.GroupId);

            if (alreadyShared)
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists, "Тест вже поширено в цій групі."));
            }

            var testShare = new TestGroupShare
            {
                TestId = request.TestId,
                GroupId = request.GroupId,
                SharedAt = DateTime.UtcNow
            };

            _context.TestGroupShares.Add(testShare);
            await _context.SaveChangesAsync();

            return new Google.Protobuf.WellKnownTypes.Empty();
        }

        // Helper Methods 

        private Guid GetUserIdOrThrow(ServerCallContext context)
        {
            var claim = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(claim?.Value))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User authentication claim not found."));
            }

            if (!Guid.TryParse(claim.Value, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID format in token."));
            }

            return userId;
        }

        private async Task<string> GenerateUniqueJoinCodeAsync()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            int length = 12;
            const int maxRetries = 10; 

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var code = RandomNumberGenerator.GetString(chars, length);

                var exists = await _context.Groups.AnyAsync(g => g.JoinCode == code);
                if (!exists)
                {
                    return code; 
                }
            }

            _logger.LogError("Failed to generate a unique join code after {MaxRetries} attempts.", maxRetries);
            throw new RpcException(new Status(StatusCode.Internal, "Не вдалося згенерувати унікальний код для групи. Спробуйте пізніше."));
        }
    }
}