using System;
using System.Collections.Generic;
using System.IO;
using LiteRT;
using LiteRT.Interop;
using LiteRT.Unity;
using UnityEngine;

/// <summary>Unity port of the MinimalInference console sample.</summary>
public sealed class MinimalInferenceBehaviour : MonoBehaviour
{
    [Tooltip("Model file name inside Assets/StreamingAssets.")]
    [SerializeField]
    string modelFileName = "sqrt_mean_mul_ops.tflite";

    LiteRtModelLoader loader;

    async void Start()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, modelFileName);

            loader = new LiteRtModelLoader();
            LiteRtModel model = await loader.LoadFromPathAsync(path, destroyCancellationToken);

            Debug.Log($"[LiteRT] Loaded model from {path}, accelerator: Cpu");
            RunInference(model);
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

    void OnDestroy()
    {
        loader?.Dispose();
        loader = null;
    }

    void RunInference(LiteRtModel model)
    {
        using var env = new LiteRtEnvironment(LiteRtHwAccelerators.Cpu);
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
                Debug.Log($"[LiteRT]   output '{signature.GetOutputName(i)}': [{string.Join(", ", values)}]");
            }

            Debug.Log("[LiteRT] Done.");
        }
        finally
        {
            foreach (var b in inputs) b.Dispose();
            foreach (var b in outputs) b.Dispose();
        }
    }
}
