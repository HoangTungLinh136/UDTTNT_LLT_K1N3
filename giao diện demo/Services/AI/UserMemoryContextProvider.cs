using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI
{
    public sealed class UserMemoryContextProvider :
        IUserMemoryContextProvider
    {
        private readonly IUserMemoryService _memoryService;

        public UserMemoryContextProvider(
            IUserMemoryService memoryService
        )
        {
            _memoryService = memoryService;
        }

        public async Task<IReadOnlyList<UserMemoryContextItem>> GetForUserAsync(
            string userId,
            CancellationToken cancellationToken = default
        )
        {
            var memories =
                await _memoryService.GetActiveMemoriesForUserAsync(
                    userId,
                    limit: 20,
                    cancellationToken
                );

            return memories
                .Select(
                    x => new UserMemoryContextItem
                    {
                        MemoryType = x.MemoryType,
                        MemoryKey = x.MemoryKey,
                        Content = x.Content,
                        Confidence = x.Confidence,
                        Source = x.Source
                    }
                )
                .ToList();
        }
    }
}