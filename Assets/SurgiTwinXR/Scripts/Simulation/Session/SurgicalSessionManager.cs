using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SurgiTwinXR.Cameras;
using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.Patient;
using SurgiTwinXR.Utilities;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Owns a training session: session ID, clock, event log and trajectory recording.
    /// Exports one JSON file per session to Application.persistentDataPath/SurgiTwinLogs
    /// (on Quest: /sdcard/Android/data/&lt;package&gt;/files/SurgiTwinLogs).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SurgicalSessionManager : MonoBehaviour
    {
        public const string SchemaId = "surgitwin.phase1.session/1";

        public struct SessionEvent
        {
            public float time;
            public string type;
            public string subject;
            public string detail;
        }

        public static SurgicalSessionManager Instance { get; private set; }

        [SerializeField] bool autoStartSession = true;
        [SerializeField] bool exportOnPauseOrQuit = true;
        [SerializeField] string logFolderName = "SurgiTwinLogs";
        [SerializeField] int maxEvents = 4096;

        [Header("Systems")]
        [SerializeField] PatientStateController patient;
        [SerializeField] SurgicalTaskController task;
        [SerializeField] InstrumentTrajectoryRecorder recorder;
        [SerializeField] SurgicalWorkspaceController workspace;
        [SerializeField] SurgicalCameraManager cameras;

        readonly List<SessionEvent> events = new List<SessionEvent>(256);
        readonly List<InstrumentController> subscribed = new List<InstrumentController>(8);
        string sessionId;
        DateTime startedUtc;
        float startTime;
        bool running;

        public string SessionId => sessionId;
        public bool IsRunning => running;
        public float SessionTime => running ? Time.time - startTime : 0f;
        public string LastExportPath { get; private set; }
        public int EventCount => events.Count;
        public string LogDirectory => Path.Combine(Application.persistentDataPath, logFolderName);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SurgiTwin] More than one SurgicalSessionManager in the scene; the newest one is ignored.");
                enabled = false;
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Start()
        {
            if (autoStartSession)
            {
                StartSession();
            }
        }

        // ------------------------------------------------------------------ session lifecycle

        public void StartSession()
        {
            if (running)
            {
                EndSession(true);
            }

            startedUtc = DateTime.UtcNow;
            sessionId = "ST_" + startedUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            startTime = Time.time;
            events.Clear();
            running = true;

            Subscribe();
            if (recorder != null)
            {
                recorder.BeginRecording(startTime);
            }
            LogEvent("SESSION_STARTED", sessionId, Application.platform.ToString());
            Debug.Log($"[SurgiTwin] Session {sessionId} started.");
        }

        /// <summary>Stops recording and (optionally) exports. Returns the export path or null.</summary>
        public string EndSession(bool export) => EndSession(export, false);

        /// <param name="waitForExport">Block until the export file is written (application quit).</param>
        public string EndSession(bool export, bool waitForExport)
        {
            if (!running)
            {
                return null;
            }
            LogEvent("SESSION_ENDED", sessionId, null);
            if (recorder != null)
            {
                recorder.StopRecording();
            }
            Unsubscribe();
            string path = export ? ExportSession(waitForExport) : null;
            running = false;
            return path;
        }

        public void LogEvent(string type, string subject, string detail)
        {
            if (!running || events.Count >= maxEvents)
            {
                return;
            }
            events.Add(new SessionEvent { time = Time.time - startTime, type = type, subject = subject, detail = detail });
        }

        // ------------------------------------------------------------------ event wiring

        void Subscribe()
        {
            if (patient != null) patient.StateChanged += OnPatientState;
            if (task != null)
            {
                task.TaskStateChanged += OnTaskState;
                task.GatePassed += OnGatePassed;
            }
            if (workspace != null) workspace.InstrumentWorkspaceChanged += OnWorkspace;

            var all = InstrumentController.Active;
            for (int i = 0; i < all.Count; i++)
            {
                all[i].StateChanged += OnInstrumentState;
                all[i].ControlSourceChanged += OnInstrumentSource;
                subscribed.Add(all[i]);
            }
        }

        void Unsubscribe()
        {
            if (patient != null) patient.StateChanged -= OnPatientState;
            if (task != null)
            {
                task.TaskStateChanged -= OnTaskState;
                task.GatePassed -= OnGatePassed;
            }
            if (workspace != null) workspace.InstrumentWorkspaceChanged -= OnWorkspace;
            for (int i = 0; i < subscribed.Count; i++)
            {
                if (subscribed[i] != null)
                {
                    subscribed[i].StateChanged -= OnInstrumentState;
                    subscribed[i].ControlSourceChanged -= OnInstrumentSource;
                }
            }
            subscribed.Clear();
        }

        void OnPatientState(PatientState previous, PatientState next) =>
            LogEvent("PATIENT_STATE", "PATIENT", SurgiTwinTokens.Token(next));

        void OnTaskState(TaskState previous, TaskState next)
        {
            string detail = SurgiTwinTokens.Token(next);
            if (next == TaskState.Aborted && task.AbortReason != null)
            {
                detail += ":" + task.AbortReason;
            }
            if (next == TaskState.Completed)
            {
                detail += ":" + task.ElapsedSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";
            }
            LogEvent("TASK_STATE", task.TaskId, detail);
        }

        void OnGatePassed(int index, int passedCount, int total) =>
            LogEvent("GATE_PASSED", task.TaskId, index + ":" + passedCount + "/" + total);

        void OnWorkspace(InstrumentController ic, bool inside) =>
            LogEvent(inside ? "WORKSPACE_ENTER" : "WORKSPACE_EXIT", ic.Identity.InstrumentId, null);

        void OnInstrumentState(InstrumentController ic, InstrumentState previous, InstrumentState next)
        {
            // HELD <-> MOVING flips constantly; those are captured by the trajectory samples instead.
            bool heldMovingFlip =
                (previous == InstrumentState.Held && next == InstrumentState.Moving) ||
                (previous == InstrumentState.Moving && next == InstrumentState.Held);
            if (!heldMovingFlip)
            {
                LogEvent("INSTRUMENT_STATE", ic.Identity.InstrumentId, SurgiTwinTokens.Token(next));
            }
        }

        void OnInstrumentSource(InstrumentController ic, IInstrumentControlSource previous, IInstrumentControlSource next)
        {
            if (next != null)
            {
                LogEvent("CONTROL_ACQUIRED", ic.Identity.InstrumentId, SurgiTwinTokens.Token(next.SourceType) + ":" + next.SourceId);
            }
            else if (previous != null)
            {
                LogEvent("CONTROL_RELEASED", ic.Identity.InstrumentId, SurgiTwinTokens.Token(previous.SourceType) + ":" + previous.SourceId);
            }
        }

        // ------------------------------------------------------------------ export

        /// <summary>Everything the JSON needs, copied on the main thread so a worker can write it.</summary>
        sealed class ExportSnapshot
        {
            public string path;
            public string sessionId;
            public DateTime startedUtc;
            public float durationSeconds;
            public string device;
            public string unityVersion;
            public bool hasTask;
            public string taskId;
            public string taskState;
            public int gatesPassed;
            public int gateCount;
            public float progress;
            public float elapsedSeconds;
            public string patientState;
            public readonly List<SessionEvent> events = new List<SessionEvent>(256);
            public bool hasTrajectory;
            public readonly InstrumentTrajectoryRecorder.Snapshot trajectory = new InstrumentTrajectoryRecorder.Snapshot();
        }

        // Phase 1.1 (D2): building and writing the whole JSON on the main thread stalled the headset for up to a
        // second (19 fps on Quest 3S). Now the main thread only copies a snapshot; a worker formats and writes it.
        // One export runs at a time; a request made while one is running is queued and runs when it finishes.
        readonly ExportSnapshot exportSnapshot = new ExportSnapshot();
        System.Threading.Tasks.Task exportTask;
        bool exportQueued;

        public bool IsExporting => exportTask != null && !exportTask.IsCompleted;

        /// <summary>
        /// Writes the current session (so far) to disk on a worker thread. Safe to call repeatedly.
        /// Returns the destination path (the file is complete once the "Session exported" log line appears).
        /// </summary>
        public string ExportSession() => ExportSession(false);

        /// <param name="waitForCompletion">Block until the file is written (used on quit, where a worker may be killed).</param>
        public string ExportSession(bool waitForCompletion)
        {
            if (sessionId == null)
            {
                return null;
            }
            string path = Path.Combine(LogDirectory, sessionId + ".json");

            if (IsExporting)
            {
                if (!waitForCompletion)
                {
                    exportQueued = true;
                    return path;
                }
                exportTask.Wait();
            }
            exportQueued = false;

            try
            {
                CaptureExportSnapshot(exportSnapshot, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SurgiTwin] Session export failed: {e.Message}");
                return null;
            }

            ExportSnapshot snap = exportSnapshot;
            exportTask = System.Threading.Tasks.Task.Run(() => WriteSnapshotToDisk(snap));
            if (waitForCompletion)
            {
                exportTask.Wait();
            }
            return path;
        }

        void Update()
        {
            if (exportQueued && !IsExporting)
            {
                ExportSession(false);
            }
        }

        void CaptureExportSnapshot(ExportSnapshot s, string path)
        {
            s.path = path;
            s.sessionId = sessionId;
            s.startedUtc = startedUtc;
            s.durationSeconds = Time.time - startTime;
            s.device = SystemInfo.deviceModel;
            s.unityVersion = Application.unityVersion;
            s.hasTask = task != null;
            if (s.hasTask)
            {
                s.taskId = task.TaskId;
                s.taskState = SurgiTwinTokens.Token(task.State);
                s.gatesPassed = task.GatesPassed;
                s.gateCount = task.GateCount;
                s.progress = task.Progress;
                s.elapsedSeconds = task.ElapsedSeconds;
            }
            s.patientState = patient != null ? SurgiTwinTokens.Token(patient.State) : "UNKNOWN";
            s.events.Clear();
            s.events.AddRange(events);
            s.hasTrajectory = recorder != null;
            if (s.hasTrajectory)
            {
                recorder.CaptureSnapshot(s.trajectory);
            }
        }

        // Worker thread: touches only the snapshot. Debug.Log is thread-safe in Unity.
        void WriteSnapshotToDisk(ExportSnapshot s)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s.path));
                string tmp = s.path + ".tmp";
                using (var writer = new StreamWriter(tmp, false, new UTF8Encoding(false), 1 << 16))
                {
                    WriteJson(writer, s);
                }
                // Replace atomically so a reader never sees a half-written file.
                if (File.Exists(s.path))
                {
                    File.Delete(s.path);
                }
                File.Move(tmp, s.path);
                LastExportPath = s.path;
                Debug.Log($"[SurgiTwin] Session exported: {s.path} ({s.events.Count} events, "
                          + $"{(s.hasTrajectory ? s.trajectory.count : 0)} samples)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SurgiTwin] Session export failed: {e.Message}");
            }
        }

        static void WriteJson(TextWriter w, ExportSnapshot s)
        {
            w.Write('{');
            JsonText.Key(w, "schema"); JsonText.String(w, SchemaId); w.Write(',');
            JsonText.Key(w, "sessionId"); JsonText.String(w, s.sessionId); w.Write(',');
            JsonText.Key(w, "startedUtc"); JsonText.String(w, s.startedUtc.ToString("o", CultureInfo.InvariantCulture)); w.Write(',');
            JsonText.Key(w, "durationSeconds"); JsonText.Number(w, s.durationSeconds); w.Write(',');
            JsonText.Key(w, "device"); JsonText.String(w, s.device); w.Write(',');
            JsonText.Key(w, "unityVersion"); JsonText.String(w, s.unityVersion); w.Write(',');
            JsonText.Key(w, "units"); JsonText.String(w, "metres, unity world space (left-handed, Y up)"); w.Write(',');

            w.Write("\"task\":{");
            if (s.hasTask)
            {
                JsonText.Key(w, "taskId"); JsonText.String(w, s.taskId); w.Write(',');
                JsonText.Key(w, "state"); JsonText.String(w, s.taskState); w.Write(',');
                JsonText.Key(w, "gatesPassed"); JsonText.Number(w, s.gatesPassed); w.Write(',');
                JsonText.Key(w, "gateCount"); JsonText.Number(w, s.gateCount); w.Write(',');
                JsonText.Key(w, "progress"); JsonText.Number(w, s.progress); w.Write(',');
                JsonText.Key(w, "elapsedSeconds"); JsonText.Number(w, s.elapsedSeconds);
            }
            w.Write("},");

            JsonText.Key(w, "patientState");
            JsonText.String(w, s.patientState);
            w.Write(',');

            List<SessionEvent> events = s.events;
            w.Write("\"events\":[\n");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) w.Write(",\n");
                SessionEvent e = events[i];
                w.Write('{');
                JsonText.Key(w, "t"); JsonText.Number(w, e.time); w.Write(',');
                JsonText.Key(w, "type"); JsonText.String(w, e.type); w.Write(',');
                JsonText.Key(w, "subject"); JsonText.String(w, e.subject); w.Write(',');
                JsonText.Key(w, "detail"); JsonText.String(w, e.detail);
                w.Write('}');
            }
            w.Write("\n],");

            w.Write("\"trajectory\":{");
            if (s.hasTrajectory)
            {
                InstrumentTrajectoryRecorder.WriteJson(w, s.trajectory);
            }
            w.Write("}}");
        }

        void OnApplicationPause(bool paused)
        {
            // Quest pauses the app when the headset is removed: keep a copy on disk.
            // The app is not rendering while paused, so waiting here costs no frames and guarantees the file.
            if (paused && running && exportOnPauseOrQuit)
            {
                ExportSession(true);
            }
        }

        void OnApplicationQuit()
        {
            if (running && exportOnPauseOrQuit)
            {
                EndSession(true, true);
            }
            else if (IsExporting)
            {
                exportTask.Wait();
            }
        }
    }
}
