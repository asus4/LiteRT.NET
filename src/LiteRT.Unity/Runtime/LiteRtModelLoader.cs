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
    /// <summary>
    /// Loads a <see cref="LiteRtModel"/> from a path, picking the cheapest correct strategy for
    /// where the file actually lives, and running the slow native parse off the main thread.
    ///
    /// <list type="bullet">
    /// <item>A real filesystem path (e.g. <c>Application.persistentDataPath</c>, or
    /// <c>StreamingAssets</c> on desktop) is opened directly with
    /// <see cref="LiteRtModel.CreateFromFile"/> — no download, no copy.</item>
    /// <item>A URL-style path (Android <c>StreamingAssets</c>, a <c>jar:file://…</c> URL inside the
    /// APK, or <c>http(s)://</c>) is fetched with <see cref="UnityWebRequest"/>, copied once into a
    /// persistent buffer this loader owns, and handed to LiteRT as an in-memory model.</item>
    /// </list>
    ///
    /// The loader owns the model and any backing buffer; disposing it releases both in the correct
    /// order. Reusing the same loader for another <see cref="LoadFromPathAsync"/> disposes the previous
    /// model first. Not designed for concurrent loads on a single instance.
    /// </summary>
    public sealed class LiteRtModelLoader : IDisposable
    {
        // Persistent copy of the model bytes for the URL branch; default (not created) for the
        // file branch, which lets the native runtime read the file directly.
        private NativeArray<byte> data;

        /// <summary>The most recently loaded model, or <c>null</c> before the first successful load.</summary>
        public LiteRtModel? Model { get; private set; }

        public void Dispose() => DisposeModel();

        /// <summary>
        /// Loads a model from <paramref name="path"/>. Paths containing <c>"://"</c> are treated as
        /// URLs and fetched via <see cref="UnityWebRequest"/>; everything else is treated as a real
        /// filesystem path and opened directly. The returned model is owned by this loader.
        /// </summary>
        public async ValueTask<LiteRtModel> LoadFromPathAsync(string path, CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) throw new ArgumentException("Path is empty.", nameof(path));

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
            try
            {
                using (var request = UnityWebRequest.Get(url))
                {
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

                    await Awaitable.BackgroundThreadAsync();
                    cancellationToken.ThrowIfCancellationRequested();

                    data = new NativeArray<byte>(src.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    src.CopyTo(data);
                }

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
