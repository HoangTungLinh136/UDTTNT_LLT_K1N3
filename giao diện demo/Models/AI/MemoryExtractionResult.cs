namespace giao_dien_demo.Models.AI
{
    public sealed class MemoryExtractionResult
    {
        public bool ShouldStore { get; set; }

        public string? RejectionReason { get; set; }

        public MemoryCandidate? Candidate { get; set; }
    }
}
