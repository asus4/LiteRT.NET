using System;
using System.IO;
using LiteRT.LM;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: SimpleLlm <model.litertlm> [prompt] [backend]");
    return 1;
}

string modelPath = args[0];
string prompt = args.Length > 1 ? args[1] : "Hello! Tell me a fun fact.";
string backend = args.Length > 2 ? args[2] : "cpu";

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine($"Model not found: {modelPath}");
    return 1;
}

// Quiet the native INFO logging; raise to 2 (INFO) to see engine details.
LlmEngine.SetMinLogLevel(3);

Console.WriteLine($"Loading model: {modelPath} (backend: {backend})");
using var engine = LlmEngine.Create(modelPath, backend);
using var session = engine.CreateSession();

Console.WriteLine($"Prompt: {prompt}");
Console.Write("Response: ");
session.GenerateContentStream(prompt, chunk => Console.Write(chunk));
Console.WriteLine();
Console.WriteLine("Done.");
return 0;
