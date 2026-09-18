using System;
using System.IO;
using UnityEngine;

namespace FoodStreet.Saving
{
    /// <summary>
    /// Everything that touches the filesystem.
    ///
    /// Writes never overwrite a good file in place: the new bytes land in a temp
    /// file, the previous save is rotated to .bak, and only then does the temp
    /// file take the live name. Whatever moment the process dies at, at least
    /// one complete file is on disk, and <see cref="TryRead"/> walks the
    /// candidates until one passes its MAC.
    /// </summary>
    internal static class SaveStorage
    {
        private const string LiveFileName = "foodstreet.sav";
        private const string TempFileName = "foodstreet.sav.tmp";
        private const string BackupFileName = "foodstreet.sav.bak";
        private const string QuarantineFileName = "foodstreet.corrupt.sav";

        private static readonly object ioGate = new object();

        private static string rootPath;
        private static long lastWrittenSequence;

        internal static bool IsInitialized => rootPath != null;

        internal static string RootPath => rootPath;
        internal static string LivePath => Path.Combine(rootPath, LiveFileName);
        internal static string TempPath => Path.Combine(rootPath, TempFileName);
        internal static string BackupPath => Path.Combine(rootPath, BackupFileName);

        /// <summary>
        /// Must run on the main thread: Unity refuses to hand out
        /// persistentDataPath from a worker, and every later write happens off
        /// the main thread.
        /// </summary>
        internal static void Initialize(string persistentDataPath)
        {
            lock (ioGate)
            {
                rootPath = persistentDataPath;
            }
        }

        internal static bool LiveFileExists()
        {
            lock (ioGate)
            {
                return rootPath != null && File.Exists(LivePath);
            }
        }

        /// <summary>
        /// Reads the newest file that passes verification. Stale writes are
        /// dropped by sequence number so a slow background write cannot land on
        /// top of a newer one.
        /// </summary>
        internal static SaveReadStatus TryRead(out string json)
        {
            json = null;
            if (rootPath == null)
                return SaveReadStatus.Missing;

            lock (ioGate)
            {
                // Order matters: the live file wins when it is valid. The temp
                // file is only newer in the crash window between the two moves,
                // and its MAC rejects it if the crash happened mid-write.
                SaveReadStatus best = SaveReadStatus.Missing;

                foreach (string path in new[] { LivePath, TempPath, BackupPath })
                {
                    SaveReadStatus status = ReadFile(path, out string candidate);
                    if (status == SaveReadStatus.Ok)
                    {
                        json = candidate;
                        return SaveReadStatus.Ok;
                    }

                    // Report the most informative failure we saw. "Missing" is
                    // the least interesting, so anything else outranks it.
                    if (status != SaveReadStatus.Missing && best == SaveReadStatus.Missing)
                        best = status;
                }

                return best;
            }
        }

        private static SaveReadStatus ReadFile(string path, out string json)
        {
            json = null;
            try
            {
                if (!File.Exists(path))
                    return SaveReadStatus.Missing;

                byte[] bytes = File.ReadAllBytes(path);
                return SaveEnvelope.TryUnpack(bytes, out json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not read '{path}': {exception.Message}");
                return SaveReadStatus.Unreadable;
            }
        }

        /// <summary>
        /// Writes a packed envelope. Safe to call from a background thread.
        /// Returns false when the write was skipped because a newer snapshot
        /// already made it to disk.
        /// </summary>
        internal static bool Write(byte[] file, long sequence)
        {
            if (rootPath == null)
                throw new InvalidOperationException("SaveStorage.Initialize has not run.");

            lock (ioGate)
            {
                if (sequence <= lastWrittenSequence)
                    return false;

                string livePath = LivePath;
                string tempPath = TempPath;
                string backupPath = BackupPath;

                // 1. Land the new bytes somewhere that is not the live file.
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(file, 0, file.Length);
                    stream.Flush(true);
                }

                // 2. Rotate the previous save out of the way. A crash here still
                //    leaves either the live file or the backup complete.
                if (File.Exists(livePath))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Move(livePath, backupPath);
                }

                // 3. Publish.
                File.Move(tempPath, livePath);

                lastWrittenSequence = sequence;
                return true;
            }
        }

        /// <summary>
        /// Moves an unusable save aside instead of deleting it, so a support
        /// request can still recover something, and the game starts clean.
        /// </summary>
        internal static void Quarantine()
        {
            if (rootPath == null)
                return;

            lock (ioGate)
            {
                foreach (string path in new[] { LivePath, TempPath, BackupPath })
                {
                    TryQuarantineFile(path);
                }
            }
        }

        private static void TryQuarantineFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                string target = Path.Combine(rootPath, QuarantineFileName);
                for (int suffix = 0; suffix < 16 && File.Exists(target); suffix++)
                {
                    target = Path.Combine(rootPath, $"{QuarantineFileName}.{suffix}");
                }

                if (File.Exists(target))
                    File.Delete(target);

                File.Move(path, target);
                Debug.LogWarning($"[SaveSystem] Unusable save moved to '{target}'.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not quarantine '{path}': {exception.Message}");
            }
        }

        internal static void DeleteAll()
        {
            if (rootPath == null)
                return;

            lock (ioGate)
            {
                foreach (string path in new[] { LivePath, TempPath, BackupPath })
                {
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[SaveSystem] Could not delete '{path}': {exception.Message}");
                    }
                }

                lastWrittenSequence = 0;
            }
        }
    }
}
