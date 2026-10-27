using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI
{
    public interface IMemoryExtractor
    {
        MemoryExtractionResult Extract(string userMessage);
    }
}
