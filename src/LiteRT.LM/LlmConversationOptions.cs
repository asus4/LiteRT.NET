using LiteRT.LM.Interop;

namespace LiteRT.LM
{
    public sealed class LlmConversationOptions
    {
        public string? SystemInstruction { get; set; }

        /// <summary>Raw JSON array, e.g. <c>[{"role":"user","content":[...]}]</c>.</summary>
        public string? InitialMessagesJson { get; set; }

        public LiteRtLmSamplerParams? SamplerParams { get; set; }

        /// <summary>Per turn; 0 uses the engine default.</summary>
        public int MaxOutputTokens { get; set; }
    }
}
