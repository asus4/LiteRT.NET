using LiteRT.LM.Interop;

namespace LiteRT.LM
{
    /// <summary>Options for creating an <see cref="LlmConversation"/>.</summary>
    public sealed class LlmConversationOptions
    {
        /// <summary>Plain-text system instruction (e.g. "You are a helpful assistant.").</summary>
        public string? SystemInstruction { get; set; }

        /// <summary>
        /// Raw JSON array of initial messages to seed the conversation with
        /// (e.g. <c>[{"role":"user","content":[...]}]</c>). Advanced use.
        /// </summary>
        public string? InitialMessagesJson { get; set; }

        /// <summary>Sampler parameters; null uses the engine defaults.</summary>
        public LiteRtLmSamplerParams? SamplerParams { get; set; }

        /// <summary>Maximum output tokens per turn; 0 uses the engine default.</summary>
        public int MaxOutputTokens { get; set; }
    }
}
