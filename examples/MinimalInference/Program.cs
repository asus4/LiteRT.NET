using System;
using System.Collections.Generic;
using System.IO;
using LiteRT;
using LiteRT.Interop;

string modelPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "models", "sqrt_mean_mul_ops.tflite");

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine($"Model not found: {modelPath}");
    return 1;
}

Console.WriteLine($"Loading model: {modelPath}");

using var env = new LiteRtEnvironment();
using var model = LiteRtModel.CreateFromFile(modelPath);
using var compiled = new LiteRtCompiledModel(env, model, LiteRtHwAccelerators.Cpu);

var signature = model.GetSignature(0);
Console.WriteLine($"Signature[0]: {signature.InputCount} input(s), {signature.OutputCount} output(s)");

var inputs = new List<LiteRtTensorBuffer>();
var outputs = new List<LiteRtTensorBuffer>();
try
{
    for (int i = 0; i < signature.InputCount; i++)
    {
        var buffer = compiled.CreateInputBuffer(signature, i);
        inputs.Add(buffer);
        int count = buffer.PackedByteSize / sizeof(float);
        Console.WriteLine($"  input '{signature.GetInputName(i)}': {buffer.ElementType}, {count} float element(s)");

        // Fill with a simple ramp so the output is deterministic.
        var data = new float[count];
        for (int j = 0; j < count; j++) data[j] = j + 1;
        buffer.Write(data);
    }

    for (int i = 0; i < signature.OutputCount; i++)
    {
        outputs.Add(compiled.CreateOutputBuffer(signature, i));
    }

    compiled.Run(signature, inputs, outputs);

    for (int i = 0; i < outputs.Count; i++)
    {
        var values = outputs[i].ReadFloats();
        Console.WriteLine($"  output '{signature.GetOutputName(i)}': [{string.Join(", ", PreviewFloats(values))}]");
    }
}
finally
{
    foreach (var b in inputs) b.Dispose();
    foreach (var b in outputs) b.Dispose();
}

Console.WriteLine("Done.");
return 0;

static IEnumerable<string> PreviewFloats(float[] values)
{
    int n = Math.Min(values.Length, 16);
    for (int i = 0; i < n; i++) yield return values[i].ToString("0.####");
    if (values.Length > n) yield return "...";
}
