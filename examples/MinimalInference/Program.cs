using System;
using System.Collections.Generic;
using System.IO;
using LiteRT;
using LiteRT.Interop;

string modelPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "models", "sqrt_mean_mul_ops.tflite");

// Second arg picks the accelerator: "cpu" (default) or "gpu".
string acceleratorArg = args.Length > 1 ? args[1].ToLowerInvariant() : "cpu";
var accelerator = acceleratorArg == "gpu" ? LiteRtHwAccelerators.Gpu : LiteRtHwAccelerators.Cpu;

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine($"Model not found: {modelPath}");
    return 1;
}

Console.WriteLine($"Loading model: {modelPath} (accelerator: {accelerator})");

// Auto-register CPU plus whatever the compiled model needs. Limiting the set keeps a
// CPU-only run from probing (and warning about) GPU/NPU plugins it won't use.
using var env = new LiteRtEnvironment(LiteRtHwAccelerators.Cpu | accelerator);
using var model = LiteRtModel.CreateFromFile(modelPath);
using var compiled = new LiteRtCompiledModel(env, model, accelerator);

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
        Console.WriteLine($"  input '{signature.GetInputName(i)}': {signature.GetInputTensorType(i)}, {count} float element(s)");

        // Fill with a simple ramp so the output is deterministic.
        var data = new float[count];
        for (int j = 0; j < count; j++) data[j] = j + 1;
        buffer.Write(data);
    }

    for (int i = 0; i < signature.OutputCount; i++)
    {
        outputs.Add(compiled.CreateOutputBuffer(signature, i));
        Console.WriteLine($"  output '{signature.GetOutputName(i)}': {signature.GetOutputTensorType(i)}");
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
