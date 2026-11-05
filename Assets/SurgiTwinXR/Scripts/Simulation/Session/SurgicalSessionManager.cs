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
        public string EndSession(bool export)
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
            string path = export ? ExportSession() : null;
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

        /// <summary>Writes the current session (so far) to disk. Safe to call repeatedly.</summary>
        public string ExportSession()
        {
            if (sessionId == null)
            {
                return null;
            }
            try
            {
                Directory.CreateDirectory(LogDirectory);
                string path = Path.Combine(LogDirectory, sessionId + ".json");
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16))
                {
                    WriteJson(writer);
                }
                LastExportPath = path;
                Debug.Log($"[SurgiTwin] Session exported: {path}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SurgiTwin] Session export failed: {e.Message}");
                return null;
            }
        }

        void WriteJson(TextWriter w)
        {
            w.Write('{');
            JsonText.Key(w, "schema"); JsonText.String(w, SchemaId); w.Write(',');
            JsonText.Key(w, "sessionId"); JsonText.String(w, sessionId); w.Write(',');
            JsonText.Key(w, "startedUtc"); JsonText.String(w, startedUtc.ToString("o", CultureInfo.InvariantCulture)); w.Write(',');
            JsonText.Key(w, "durationSeconds"); JsonText.Number(w, Time.time - startTime); w.Write(',');
            JsonText.Key(w, "device"); JsonText.String(w, SystemInfo.deviceModel); w.Write(',');
            JsonText.Key(w, "unityVersion"); JsonText.String(w, Application.unityVersion); w.Write(',');
            JsonText.Key(w, "units"); JsonText.String(w, "metres, unity world space (left-handed, Y up)"); w.Write(',');

            w.Write("\"task\":{");
            if (task != null)
            {
                JsonText.Key(w, "taskId"); JsonText.String(w, task.TaskId); w.Write(',');
                JsonText.Key(w, "state"); JsonText.String(w, SurgiTwinTokens.Token(task.State)); w.Write(',');
                JsonText.Key(w, "gatesPassed"); JsonText.Number(w, task.GatesPassed); w.Write(',');
                JsonText.Key(w, "gateCount"); JsonText.Number(w, task.GateCount); w.Write(',');
                JsonText.Key(w, "progress"); JsonText.Number(w, task.Progress); w.Write(',');
                JsonText.Key(w, "elapsedSeconds"); JsonText.Number(w, task.ElapsedSeconds);
            }
            w.Write("},");

            JsonText.Key(w, "patientState");
            JsonText.String(w, patient != null ? SurgiTwinTokens.Token(patient.State) : "UNKNOWN");
            w.Write(',');

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
            if (recorder != null)
            {
                recorder.WriteJson(w);
            }
            w.Write("}}");
        }

        void OnApplicationPause(bool paused)
        {
            // Quest pauses the app when the headset is removed: keep a copy on disk.
            if (paused && running && exportOnPauseOrQuit)
            {
                ExportSession();
            }
        }

        void OnApplicationQuit()
        {
            if (running && exportOnPauseOrQuit)
            {
                EndSession(true);
            }
        }
    }
}
