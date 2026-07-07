using System;
using System.IO;
using LiteRT.LM;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: SimpleChat <model.litertlm> [backend] [prompt]");
    Console.Error.WriteLine("  backend: cpu (default) or gpu");
    Console.Error.WriteLine("  prompt:  when given, runs a single turn instead of the interactive chat");
    return 1;
}

string modelPath = args[0];
string backend = args.Length > 1 ? args[1] : "cpu";
string? singlePrompt = args.Length > 2 ? args[2] : null;

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine($"Model not found: {modelPath}");
    return 1;
}

// Quiet the native INFO logging; raise to 2 (INFO) to see engine details.
LlmEngine.SetMinLogLevel(3);

Console.WriteLine($"Loading model: {modelPath} (backend: {backend})");
using var engine = LlmEngine.Create(modelPath, backend);
using var conversation = engine.CreateConversation(new LlmConversationOptions
{
    SystemInstruction = "You are a helpful assistant.",
    SamplerParams = new LiteRT.LM.Interop.LiteRtLmSamplerParams
    {
        Type = LiteRT.LM.Interop.LiteRtLmSamplerType.TopK,
        TopK = 40,
        TopP = 0.95f,
        Temperature = 1.0f,
        Seed = 12345,
    },
});

if (singlePrompt != null)
{
    Console.WriteLine($"> {singlePrompt}");
    conversation.SendMessageStream(singlePrompt, chunk => Console.Write(chunk));
    Console.WriteLine();
    return 0;
}

Console.WriteLine("Multi-turn chat. Empty line or Ctrl+C to exit.");
while (true)
{
    Console.Write("> ");
    string? line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
    {
        break;
    }

    conversation.SendMessageStream(line, chunk => Console.Write(chunk));
    Console.WriteLine();
}
return 0;
