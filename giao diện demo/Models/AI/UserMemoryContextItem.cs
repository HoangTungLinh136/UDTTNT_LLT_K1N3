namespace giao_dien_demo.Models.AI
{
    public sealed class UserMemoryContextItem
    {
        public string MemoryType { get; set; } = string.Empty;

        public string MemoryKey { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public double Confidence { get; set; }

        public string Source { get; set; } = string.Empty;
    }
}