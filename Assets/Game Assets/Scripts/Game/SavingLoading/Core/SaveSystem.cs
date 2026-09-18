using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace FoodStreet.Saving
{
    /// <summary>
    /// The one entry point for persistence.
    ///
    /// Gameplay code never touches files. It edits <see cref="Data"/> and calls
    /// <see cref="MarkDirty"/>; the system coalesces those requests and writes at
    /// most once every <see cref="AutoSaveInterval"/> seconds, plus on pause,
    /// focus loss and quit.
    ///
    /// Snapshotting happens on the main thread (participants may touch Unity
    /// objects, and JSON of a live object graph must not race gameplay), while
    /// compression, encryption and the file write run on a worker thread.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Fastest cadence a background autosave will fire at.</summary>
        public static float AutoSaveInterval = 5f;

        public static GameSaveData Data { get; private set; }

        public static bool IsReady => Data != null;

        /// <summary>How the file on disk was received at boot. Useful for telemetry or a warning UI.</summary>
        public static SaveReadStatus LastLoadStatus { get; private set; } = SaveReadStatus.Missing;

        /// <summary>True when this session started from no usable save at all.</summary>
        public static bool StartedFresh { get; private set; }

        public static event Action<SaveReadStatus> OnLoaded;

        /// <summary>Raised on the main thread after a write reaches disk.</summary>
        public static event Action OnSaved;

        /// <summary>Raised on the main thread when a write could not be completed.</summary>
        public static event Action<Exception> OnSaveFailed;

        private static readonly List<ISaveParticipant> participants = new List<ISaveParticipant>();

        private static readonly object writeGate = new object();
        private static byte[] pendingBytes;
        private static long pendingSequence;
        private static bool writerRunning;
        private static long sequenceCounter;

        private static bool dirty;
        private static float lastSaveTime;

        // The worker thread cannot raise the public events itself - subscribers
        // are gameplay code and will touch Unity objects. It records what
        // happened here and Tick() re-raises it on the main thread.
        private static int completedWrites;
        private static int dispatchedWrites;
        private static Exception lastWriteException;

        #region Boot

        /// <summary>
        /// Loads the save file. Call this before any system reads
        /// <see cref="Data"/>. Safe to call more than once; later calls no-op.
        /// </summary>
        public static void Initialize()
        {
            if (IsReady)
                return;

            SaveStorage.Initialize(Application.persistentDataPath);

            SaveReadStatus status = SaveStorage.TryRead(out string json);
            GameSaveData loaded = null;

            if (status == SaveReadStatus.Ok)
            {
                loaded = SaveSerializer.Deserialize(json);
                if (loaded == null)
                {
                    // Verified bytes that will not parse: the schema is broken,
                    // not the file.
                    status = SaveReadStatus.Unreadable;
                }
            }

            if (loaded != null)
            {
                SaveMigrations.Upgrade(loaded);
            }
            else if (status == SaveReadStatus.Missing)
            {
                // No file of ours yet. Anything the previous save system left
                // behind is this player's real progress - take it over.
                loaded = SaveMigrations.ImportLegacy();
            }
            else
            {
                // Present but unusable. Keep the bytes around for support and
                // start clean rather than booting into half-restored state.
                Debug.LogError($"[SaveSystem] Save file rejected ({status}). Starting a new save.");
                SaveStorage.Quarantine();
                loaded = new GameSaveData();
            }

            loaded.EnsureComplete();

            Data = loaded;
            LastLoadStatus = status;
            StartedFresh = status != SaveReadStatus.Ok;
            lastSaveTime = Time.unscaledTime;

            SaveRunner.Ensure();

            OnLoaded?.Invoke(status);
        }

        #endregion

        #region Participants

        public static void Register(ISaveParticipant participant)
        {
            if (participant == null || participants.Contains(participant))
                return;

            participants.Add(participant);
            participants.Sort((a, b) => a.SaveOrder.CompareTo(b.SaveOrder));
        }

        public static void Unregister(ISaveParticipant participant)
        {
            if (participant == null)
                return;

            participants.Remove(participant);
        }

        /// <summary>
        /// Pushes the loaded file into every registered participant. Call once,
        /// after the scene has finished building itself.
        /// </summary>
        public static void RestoreParticipants()
        {
            if (!IsReady)
            {
                Debug.LogError("[SaveSystem] RestoreParticipants called before Initialize.");
                return;
            }

            for (int i = 0; i < participants.Count; i++)
            {
                try
                {
                    participants[i].RestoreState(Data);
                }
                catch (Exception exception)
                {
                    // One broken participant must not stop the rest of the game
                    // from loading.
                    Debug.LogError($"[SaveSystem] {participants[i].GetType().Name} failed to restore: {exception}");
                }
            }
        }

        #endregion

        #region Requesting a save

        /// <summary>
        /// Marks the save as out of date. Cheap - call it freely from gameplay.
        /// The actual write happens on the autosave cadence.
        /// </summary>
        public static void MarkDirty()
        {
            dirty = true;
        }

        public static bool IsDirty => dirty;

        /// <summary>Driven by <see cref="SaveRunner"/>; not for gameplay code.</summary>
        internal static void Tick()
        {
            DispatchWriteResults();

            if (!dirty || !IsReady)
                return;
            if (Time.unscaledTime - lastSaveTime < AutoSaveInterval)
                return;

            SaveNow();
        }

        private static void DispatchWriteResults()
        {
            Exception failure = Interlocked.Exchange(ref lastWriteException, null);
            if (failure != null)
                OnSaveFailed?.Invoke(failure);

            int completed = Volatile.Read(ref completedWrites);
            if (completed == dispatchedWrites)
                return;

            dispatchedWrites = completed;
            OnSaved?.Invoke();
        }

        /// <summary>
        /// Snapshots now and hands the write to a worker thread. Returns as soon
        /// as the snapshot is taken.
        /// </summary>
        public static void SaveNow()
        {
            byte[] file = BuildFile();
            if (file == null)
                return;

            long sequence = Interlocked.Increment(ref sequenceCounter);
            Enqueue(file, sequence);
        }

        /// <summary>
        /// Snapshots and writes on the calling thread. Used on pause, focus loss
        /// and quit, where the process may not survive long enough for a worker
        /// to finish.
        /// </summary>
        public static void Flush(bool force = false)
        {
            if (!IsReady)
                return;

            // Mobile fires OnApplicationPause and OnApplicationFocus back to
            // back. The first flush clears the dirty flag, so the second one
            // costs nothing.
            if (!force && !dirty && Volatile.Read(ref completedWrites) > 0)
                return;

            byte[] file = BuildFile();
            if (file == null)
                return;

            long sequence = Interlocked.Increment(ref sequenceCounter);
            try
            {
                if (SaveStorage.Write(file, sequence))
                {
                    Interlocked.Increment(ref completedWrites);
                    DispatchWriteResults();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Flush failed: {exception}");
                OnSaveFailed?.Invoke(exception);
            }
        }

        /// <summary>
        /// Main-thread half of a save: let participants copy their scene state
        /// into the model, then freeze the model to JSON and pack it.
        /// </summary>
        private static byte[] BuildFile()
        {
            if (!IsReady)
                return null;

            try
            {
                for (int i = 0; i < participants.Count; i++)
                {
                    try
                    {
                        participants[i].CaptureState(Data);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[SaveSystem] {participants[i].GetType().Name} failed to capture: {exception}");
                    }
                }

                Data.schemaVersion = SaveMigrations.CurrentSchemaVersion;
                Data.savedAtUtcTicks = DateTime.UtcNow.Ticks;

                string json = SaveSerializer.Serialize(Data);

                dirty = false;
                lastSaveTime = Time.unscaledTime;

                return SaveEnvelope.Pack(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Could not build save payload: {exception}");
                OnSaveFailed?.Invoke(exception);
                return null;
            }
        }

        #endregion

        #region Background writer

        private static void Enqueue(byte[] file, long sequence)
        {
            lock (writeGate)
            {
                // A queued write that has not started yet is already obsolete -
                // replace it instead of writing the same file twice.
                pendingBytes = file;
                pendingSequence = sequence;

                if (writerRunning)
                    return;

                writerRunning = true;
            }

            ThreadPool.QueueUserWorkItem(_ => DrainQueue());
        }

        private static void DrainQueue()
        {
            while (true)
            {
                byte[] file;
                long sequence;

                lock (writeGate)
                {
                    if (pendingBytes == null)
                    {
                        writerRunning = false;
                        return;
                    }

                    file = pendingBytes;
                    sequence = pendingSequence;
                    pendingBytes = null;
                }

                try
                {
                    if (SaveStorage.Write(file, sequence))
                        Interlocked.Increment(ref completedWrites);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[SaveSystem] Background write failed: {exception}");
                    Interlocked.Exchange(ref lastWriteException, exception);
                }
            }
        }

        #endregion

        #region Maintenance

        /// <summary>
        /// Throws away every file and resets the in-memory model. The next write
        /// starts a fresh save.
        /// </summary>
        public static void Wipe()
        {
            lock (writeGate)
            {
                pendingBytes = null;
            }

            if (!SaveStorage.IsInitialized)
                SaveStorage.Initialize(Application.persistentDataPath);

            SaveStorage.DeleteAll();
            SaveMigrations.DeleteLegacyData();

            Data = new GameSaveData();
            Data.EnsureComplete();
            LastLoadStatus = SaveReadStatus.Missing;
            StartedFresh = true;
            dirty = false;
        }

        /// <summary>Readable copy of the current state, for the editor tooling.</summary>
        public static string ExportJson()
        {
            return IsReady ? SaveSerializer.Serialize(Data, true) : "{}";
        }

        #endregion
    }
}
