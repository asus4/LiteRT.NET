using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LiteRT;
using LiteRT.Interop;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unity port of the MinimalInference console sample. Loads a small .tflite model from
/// StreamingAssets, runs one CPU inference pass with a deterministic ramp input, and logs
/// the output tensor to the Console.
/// </summary>
public sealed class MinimalInferenceBehaviour : MonoBehaviour
{
    [Tooltip("Model file name inside Assets/StreamingAssets.")]
    [SerializeField] private string modelFileName = "sqrt_mean_mul_ops.tflite";

    private async void Start()
    {
        try
        {
            await LoadAndRunAsync();
        }
        catch (OperationCanceledException)
        {
            // Object destroyed while awaiting — nothing to do.
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private async Awaitable LoadAndRunAsync()
    {
        // StreamingAssets is read via UnityWebRequest so this works on every platform:
        // on Android it lives inside the APK (a "jar:file://..." URL, not a real file path),
        // while on the Editor/desktop it is a plain path that needs a "file://" scheme.
        string raw = Path.Combine(Application.streamingAssetsPath, modelFileName);
        string uri = raw.Contains("://") ? raw : "file://" + raw;

        byte[] modelBytes;
        using (var request = UnityWebRequest.Get(uri))
        {
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LiteRT] Failed to read model from {uri}: {request.error}");
                return;
            }

            modelBytes = request.downloadHandler.data;
        }

        if (modelBytes == null || modelBytes.Length == 0)
        {
            Debug.LogError($"[LiteRT] Model is empty: {uri}");
            return;
        }

        RunInference(modelBytes);
    }

    private void RunInference(byte[] modelBytes)
    {
        Debug.Log($"[LiteRT] Loaded model ({modelBytes.Length} bytes), accelerator: Cpu");

        // Quiet CPU-only environment (no GPU/NPU plugin probing).
        using var env = new LiteRtEnvironment(LiteRtHwAccelerators.Cpu);
        using var model = LiteRtModel.CreateFromBuffer(modelBytes);
        using var compiled = new LiteRtCompiledModel(env, model, LiteRtHwAccelerators.Cpu);

        var signature = model.GetSignature(0);
        Debug.Log($"[LiteRT] Signature[0]: {signature.InputCount} input(s), {signature.OutputCount} output(s)");

        var inputs = new List<LiteRtTensorBuffer>(signature.InputCount);
        var outputs = new List<LiteRtTensorBuffer>(signature.OutputCount);
        try
        {
            for (int i = 0; i < signature.InputCount; i++)
            {
                var buffer = compiled.CreateInputBuffer(signature, i);
                inputs.Add(buffer);
                int count = buffer.PackedByteSize / sizeof(float);
                Debug.Log($"[LiteRT]   input '{signature.GetInputName(i)}': {buffer.ElementType}, {count} float element(s)");

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
                Debug.Log($"[LiteRT]   output '{signature.GetOutputName(i)}': [{PreviewFloats(values)}]");
            }

            Debug.Log("[LiteRT] Done.");
        }
        finally
        {
            foreach (var b in inputs) b.Dispose();
            foreach (var b in outputs) b.Dispose();
        }
    }

    private static string PreviewFloats(float[] values)
    {
        int n = Mathf.Min(values.Length, 16);
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(values[i].ToString("0.####"));
        }
        if (values.Length > n) sb.Append(", ...");
        return sb.ToString();
    }
}
