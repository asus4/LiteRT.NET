using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LiteRT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;

namespace LiteRT.Unity
{
    /// <summary>Loads a model from a path or URL; owns the model and backing buffer. Not safe for concurrent loads.</summary>
    public sealed class LiteRtModelLoader : IDisposable
    {
        // Only created for the URL branch; the file branch lets the native runtime read directly.
        private NativeArray<byte> data;

        public LiteRtModel? Model { get; private set; }

        public void Dispose() => DisposeModel();

        public async ValueTask<LiteRtModel> LoadFromPathAsync(string path, CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) throw new ArgumentException("Path is empty.", nameof(path));

            DisposeModel();
            if (path.Contains("://"))
            {
                Model = await LoadFromUrlAsync(path, cancellationToken);
            }
            else
            {
                Model = await LoadFromFileAsync(path, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Model!;
        }

        private async ValueTask<LiteRtModel> LoadFromFileAsync(string path, CancellationToken cancellationToken)
        {
            await Awaitable.BackgroundThreadAsync();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return LiteRtModel.CreateFromFile(path);
            }
            finally
            {
                await Awaitable.MainThreadAsync();
            }
        }

        private async ValueTask<LiteRtModel> LoadFromUrlAsync(string url, CancellationToken cancellationToken)
        {
            // UnityWebRequest is main-thread only; copy the bytes so they outlive the request.
            using (var request = UnityWebRequest.Get(url))
            {
                await request.SendWebRequest();
                cancellationToken.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new IOException($"Failed to load model from '{url}': {request.error}");
                }

                var src = request.downloadHandler.nativeData;
                if (src.Length == 0)
                {
                    throw new InvalidDataException($"Model is empty: '{url}'");
                }

                data = new NativeArray<byte>(src.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                src.CopyTo(data);
            }

            await Awaitable.BackgroundThreadAsync();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                unsafe
                {
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data);
                    return LiteRtModel.CreateFromBuffer((IntPtr)ptr, data.Length);
                }
            }
            finally
            {
                await Awaitable.MainThreadAsync();
            }
        }

        private void DisposeModel()
        {
            Model?.Dispose();
            Model = null;
            if (data.IsCreated)
            {
                data.Dispose();
            }
        }
    }
}
