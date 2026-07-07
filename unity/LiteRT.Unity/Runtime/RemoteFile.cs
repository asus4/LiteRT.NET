using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace LiteRT.Unity
{
    /// <summary>Downloads and caches large model files. Not for production use.</summary>
    [Serializable]
    public class RemoteFile : IProgress<float>
    {
        public enum DownloadLocation
        {
            Persistent,
            Cache,
        }

        public string url = string.Empty;
        public DownloadLocation downloadLocation = DownloadLocation.Persistent;

        /// <summary>Normalized progress (0..1). Reported on the main thread while downloading.</summary>
        public event Action<float>? OnDownloadProgress;

        /// <summary>Byte progress: (receivedBytes, totalBytes). totalBytes is -1 when unknown.</summary>
        public event Action<long, long>? OnDownloadProgressBytes;

        // Leave headroom for filesystem overhead and the completed-file rename.
        private const long DiskSpaceMargin = 256L * 1024 * 1024;

        public string LocalPath
        {
            get
            {
                string dir = downloadLocation switch
                {
                    DownloadLocation.Persistent => Application.persistentDataPath,
                    DownloadLocation.Cache => Application.temporaryCachePath,
                    _ => throw new Exception($"Unknown download location {downloadLocation}"),
                };
                return Path.Combine(dir, GetFileName(url));
            }
        }

        public bool HasCache => File.Exists(LocalPath);

        public RemoteFile() { }

        public RemoteFile(string url, DownloadLocation location = DownloadLocation.Persistent)
        {
            this.url = url;
            downloadLocation = location;
        }

        public void Report(float value)
        {
            OnDownloadProgress?.Invoke(value);
        }

        /// <summary>Downloads (resuming a partial when possible) and returns the local path. Excluded from iCloud backup on iOS.</summary>
        public async Awaitable<string> EnsureLocal(CancellationToken cancellationToken)
        {
            string localPath = LocalPath;

            if (File.Exists(localPath))
            {
                Log($"Cache hit: {localPath}");
                ExcludeFromBackup(localPath);
                return localPath;
            }

            string tempPath = $"{localPath}.download";
            long totalBytes = await GetSize(cancellationToken);
            long existingBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;

            // A partial larger than the remote file means the content changed; start over.
            if (totalBytes > 0 && existingBytes > totalBytes)
            {
                File.Delete(tempPath);
                existingBytes = 0;
            }

            EnsureDiskSpace(localPath, totalBytes - existingBytes);

            if (totalBytes > 0 && existingBytes == totalBytes)
            {
                File.Move(tempPath, localPath);
                ExcludeFromBackup(localPath);
                return localPath;
            }

            bool resuming = existingBytes > 0;
            Log(resuming
                ? $"Resuming download at {existingBytes}/{totalBytes} bytes from: {url}"
                : $"Cache miss for {localPath}, downloading from: {url}");

            {
                // Keep the temp file on failure/abort so the next attempt can resume.
                using var handler = new DownloadHandlerFile(tempPath, append: resuming);
                handler.removeFileOnAbort = false;
                using var request = new UnityWebRequest(url, "GET", handler, null);
                if (resuming)
                {
                    request.SetRequestHeader("Range", $"bytes={existingBytes}-");
                }

                await SendAsync(request, existingBytes, totalBytes, cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    // 416: remote file changed — drop the partial and start over once.
                    if (resuming && request.responseCode == 416)
                    {
                        Log("Range not satisfiable; restarting download from scratch.");
                        File.Delete(tempPath);
                        return await EnsureLocal(cancellationToken);
                    }
                    throw new IOException($"Failed to download from {url}: {request.error}");
                }

                // 200 while resuming: the full file got appended onto the partial — corrupt; start over once.
                if (resuming && request.responseCode == 200)
                {
                    Log("Server ignored the Range request; restarting download from scratch.");
                    File.Delete(tempPath);
                    return await EnsureLocal(cancellationToken);
                }
            }

            File.Delete(localPath);
            File.Move(tempPath, localPath);
            ExcludeFromBackup(localPath);
            return localPath;
        }

        /// <summary>Returns the file size in bytes, or -1 if the server reports none.</summary>
        public async Awaitable<long> GetSize(CancellationToken cancellationToken)
        {
            string localPath = LocalPath;
            if (File.Exists(localPath))
            {
                return new FileInfo(localPath).Length;
            }

            // Range GET is more reliable than HEAD with UnityWebRequest.
            using var handler = new DownloadHandlerBuffer();
            using var request = new UnityWebRequest(url, "GET", handler, null);
            request.SetRequestHeader("Range", "bytes=0-0");
            await SendAsync(request, 0, -1, cancellationToken, reportProgress: false);
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new IOException($"Failed to query size of {url}: {request.error}");
            }

            string contentRange = request.GetResponseHeader("Content-Range");
            if (!string.IsNullOrEmpty(contentRange))
            {
                int slashIdx = contentRange.LastIndexOf('/');
                if (slashIdx >= 0 && slashIdx < contentRange.Length - 1
                    && long.TryParse(contentRange[(slashIdx + 1)..], out long total))
                {
                    return total;
                }
            }

            string contentLength = request.GetResponseHeader("Content-Length");
            if (!string.IsNullOrEmpty(contentLength) && long.TryParse(contentLength, out long length))
            {
                return length;
            }

            return -1;
        }

        private void EnsureDiskSpace(string localPath, long requiredBytes)
        {
            if (requiredBytes <= 0)
            {
                return;
            }
            string? root = Path.GetPathRoot(Path.GetFullPath(localPath));
            if (string.IsNullOrEmpty(root))
            {
                return;
            }
            long available;
            try
            {
                available = new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception)
            {
                return; // Unsupported platform/drive layout: skip the check.
            }
            if (available < requiredBytes + DiskSpaceMargin)
            {
                throw new IOException(
                    $"Not enough disk space to download {GetFileName(url)}: " +
                    $"needs ~{(requiredBytes + DiskSpaceMargin) / (1024 * 1024)} MB free, " +
                    $"only {available / (1024 * 1024)} MB available.");
            }
        }

        private async Awaitable SendAsync(
            UnityWebRequest request, long existingBytes, long totalBytes,
            CancellationToken cancellationToken, bool reportProgress = true)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync();
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    throw new OperationCanceledException(cancellationToken);
                }
                if (reportProgress)
                {
                    ReportBytes(existingBytes + (long)request.downloadedBytes, totalBytes);
                }
            }
            if (reportProgress && request.result == UnityWebRequest.Result.Success)
            {
                ReportBytes(totalBytes > 0 ? totalBytes : existingBytes + (long)request.downloadedBytes, totalBytes);
            }
        }

        private void ReportBytes(long receivedBytes, long totalBytes)
        {
            OnDownloadProgressBytes?.Invoke(receivedBytes, totalBytes);
            if (totalBytes > 0)
            {
                Report(Mathf.Clamp01((float)((double)receivedBytes / totalBytes)));
            }
        }

        private static void ExcludeFromBackup(string path)
        {
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
        }

        private static string GetFileName(string url)
        {
            var uri = new Uri(url);
            string fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException($"Could not derive a file name from URL: {url}");
            }
            return fileName;
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        private static void Log(string message)
        {
            UnityEngine.Debug.Log($"[RemoteFile] {message}");
        }
    }
}
