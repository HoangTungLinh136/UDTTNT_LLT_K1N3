using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI
{
    public sealed class UserMemoryService : IUserMemoryService
    {
        private readonly ApplicationDbContext _db;

        public UserMemoryService(
            ApplicationDbContext db
        )
        {
            _db = db;
        }


        public async Task<UserMemory?> SaveCandidateAsync(
            string userId,
            string? employeeId,
            string? conversationId,
            MemoryCandidate candidate,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "Authenticated UserId is required.",
                    nameof(userId)
                );
            }

            if (candidate is null)
            {
                return null;
            }

            if (
                candidate.Confidence < 0.75 ||
                string.IsNullOrWhiteSpace(candidate.MemoryType) ||
                string.IsNullOrWhiteSpace(candidate.MemoryKey) ||
                string.IsNullOrWhiteSpace(candidate.Content)
            )
            {
                return null;
            }

            if (candidate.Content.Length > 4000)
            {
                return null;
            }

            var normalizedUserId = userId.Trim();
            var normalizedType = candidate.MemoryType.Trim();
            var normalizedKey = candidate.MemoryKey.Trim();

            var existing = await _db.UserMemories
                .FirstOrDefaultAsync(
                    x =>
                        x.UserId == normalizedUserId &&
                        x.MemoryType == normalizedType &&
                        x.MemoryKey == normalizedKey &&
                        x.IsActive,
                    cancellationToken
                );

            if (existing is not null)
            {
                existing.Content = candidate.Content.Trim();
                existing.Confidence = candidate.Confidence;
                existing.Source = candidate.Source;
                existing.SourceConversationId = conversationId;
                existing.EmployeeId = employeeId;
                existing.UpdatedAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync(
                    cancellationToken
                );

                return existing;
            }

            var memory = new UserMemory
            {
                UserId = normalizedUserId,
                EmployeeId = employeeId,
                MemoryType = normalizedType,
                MemoryKey = normalizedKey,
                Content = candidate.Content.Trim(),
                Confidence = candidate.Confidence,
                Source = candidate.Source,
                SourceConversationId = conversationId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.UserMemories.Add(memory);

            await _db.SaveChangesAsync(
                cancellationToken
            );

            return memory;
        }


        public async Task<IReadOnlyList<UserMemory>> GetActiveMemoriesForUserAsync(
            string userId,
            int limit = 20,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "Authenticated UserId is required.",
                    nameof(userId)
                );
            }

            var normalizedUserId = userId.Trim();

            var safeLimit = Math.Clamp(
                limit,
                1,
                50
            );

            return await _db.UserMemories
                .AsNoTracking()
                .Where(
                    x =>
                        x.UserId == normalizedUserId &&
                        x.IsActive
                )
                .OrderByDescending(
                    x => x.Confidence
                )
                .ThenByDescending(
                    x => x.UpdatedAtUtc
                )
                .Take(safeLimit)
                .ToListAsync(
                    cancellationToken
                );
        }


        public async Task<IReadOnlyList<UserMemory>> GetActiveMemoriesForUserByTypeAsync(
            string userId,
            string memoryType,
            int limit = 20,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "Authenticated UserId is required.",
                    nameof(userId)
                );
            }

            if (string.IsNullOrWhiteSpace(memoryType))
            {
                return Array.Empty<UserMemory>();
            }

            var normalizedUserId = userId.Trim();
            var normalizedType = memoryType.Trim();

            var safeLimit = Math.Clamp(
                limit,
                1,
                50
            );

            return await _db.UserMemories
                .AsNoTracking()
                .Where(
                    x =>
                        x.UserId == normalizedUserId &&
                        x.MemoryType == normalizedType &&
                        x.IsActive
                )
                .OrderByDescending(
                    x => x.Confidence
                )
                .ThenByDescending(
                    x => x.UpdatedAtUtc
                )
                .Take(safeLimit)
                .ToListAsync(
                    cancellationToken
                );
        }


        public async Task<bool> DeactivateMemoryAsync(
            string userId,
            long memoryId,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "Authenticated UserId is required.",
                    nameof(userId)
                );
            }

            var normalizedUserId = userId.Trim();

            var memory = await _db.UserMemories
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == memoryId &&
                        x.UserId == normalizedUserId &&
                        x.IsActive,
                    cancellationToken
                );

            if (memory is null)
            {
                return false;
            }

            memory.IsActive = false;
            memory.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(
                cancellationToken
            );

            return true;
        }
    }
}