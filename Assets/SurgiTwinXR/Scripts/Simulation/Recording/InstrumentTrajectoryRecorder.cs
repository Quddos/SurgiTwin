using System.Collections.Generic;
using System.IO;
using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.Patient;
using SurgiTwinXR.Utilities;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>One sampled instrument pose plus the simulation context at that instant.</summary>
    public struct TrajectorySample
    {
        public float time;
        public short instrumentIndex;
        public short sourceIdIndex;
        public Vector3 position;
        public Quaternion rotation;
        public InstrumentState instrumentState;
        public ControlSourceType controlSource;
        public PatientState patientState;
        public TaskState taskState;
        public float jawClosure;
        public bool tipContact;
        public bool tissueInteraction;
    }

    /// <summary>
    /// Samples every active instrument at a fixed rate into a pre-allocated buffer of structs
    /// (no per-sample heap allocation). Exported as JSON by <see cref="SurgicalSessionManager"/>.
    /// </summary>
    public sealed class InstrumentTrajectoryRecorder : MonoBehaviour
    {
        [SerializeField, Range(1f, 90f)] float sampleRateHz = 30f;
        [Tooltip("Pre-allocated sample capacity. 120 000 samples = ~16 min for 4 instruments at 30 Hz.")]
        [SerializeField] int capacity = 120000;
        [SerializeField] PatientStateController patient;
        [SerializeField] SurgicalTaskController task;

        TrajectorySample[] samples;
        int count;
        bool recording;
        bool overflowWarned;
        float startTime;
        float accumulator;

        readonly List<InstrumentController> instruments = new List<InstrumentController>(8);
        readonly Dictionary<InstrumentController, short> instrumentIndex = new Dictionary<InstrumentController, short>(8);
        readonly List<string> sourceIds = new List<string>(8);
        readonly Dictionary<string, short> sourceIdIndex = new Dictionary<string, short>(8);

        public bool IsRecording => recording;
        public int SampleCount => count;
        public int Capacity => capacity;
        public float SampleRateHz => sampleRateHz;
        public bool Overflowed => overflowWarned;

        public void BeginRecording(float sessionStartTime)
        {
            if (samples == null || samples.Length != capacity)
            {
                samples = new TrajectorySample[Mathf.Max(1, capacity)];
            }
            count = 0;
            overflowWarned = false;
            accumulator = 0f;
            startTime = sessionStartTime;
            instruments.Clear();
            instrumentIndex.Clear();
            sourceIds.Clear();
            sourceIdIndex.Clear();
            InternSource("NONE");
            recording = true;
            SampleAll();
        }

        public void StopRecording()
        {
            recording = false;
        }

        void Update()
        {
            if (!recording)
            {
                return;
            }
            accumulator += Time.deltaTime;
            float interval = 1f / sampleRateHz;
            if (accumulator < interval)
            {
                return;
            }
            // Never sample more than once per frame; drop the backlog after a hitch.
            accumulator = accumulator >= interval * 2f ? 0f : accumulator - interval;
            SampleAll();
        }

        void SampleAll()
        {
            float t = Time.time - startTime;
            PatientState ps = patient != null ? patient.State : PatientState.Normal;
            TaskState ts = task != null ? task.State : TaskState.NotStarted;
            var all = InstrumentController.Active;

            for (int i = 0; i < all.Count; i++)
            {
                if (count >= samples.Length)
                {
                    if (!overflowWarned)
                    {
                        overflowWarned = true;
                        Debug.LogWarning("[SurgiTwin] Trajectory buffer full; further samples are dropped. Increase capacity.");
                    }
                    return;
                }

                InstrumentController ic = all[i];
                Transform tr = ic.transform;
                samples[count++] = new TrajectorySample
                {
                    time = t,
                    instrumentIndex = IndexOf(ic),
                    sourceIdIndex = InternSource(ic.CurrentSourceId ?? "NONE"),
                    position = tr.position,
                    rotation = tr.rotation,
                    instrumentState = ic.State,
                    controlSource = ic.CurrentSourceType,
                    patientState = ps,
                    taskState = ts,
                    jawClosure = ic.JawClosure,
                    tipContact = ic.IsInTipContact,
                    tissueInteraction = ic.IsInteractingWithTissue
                };
            }
        }

        short IndexOf(InstrumentController ic)
        {
            if (!instrumentIndex.TryGetValue(ic, out short idx))
            {
                idx = (short)instruments.Count;
                instruments.Add(ic);
                instrumentIndex.Add(ic, idx);
            }
            return idx;
        }

        short InternSource(string id)
        {
            if (!sourceIdIndex.TryGetValue(id, out short idx))
            {
                idx = (short)sourceIds.Count;
                sourceIds.Add(id);
                sourceIdIndex.Add(id, idx);
            }
            return idx;
        }

        /// <summary>Writes the "instruments" and "samples" members (with leading comma handling done by caller).</summary>
        public void WriteJson(TextWriter w)
        {
            JsonText.Key(w, "sampleRateHz"); JsonText.Number(w, sampleRateHz); w.Write(',');
            JsonText.Key(w, "sampleCount"); JsonText.Number(w, count); w.Write(',');
            JsonText.Key(w, "bufferOverflow"); JsonText.Bool(w, overflowWarned); w.Write(',');

            JsonText.Key(w, "instruments");
            w.Write('[');
            for (int i = 0; i < instruments.Count; i++)
            {
                if (i > 0) w.Write(',');
                InstrumentController ic = instruments[i];
                w.Write('{');
                JsonText.Key(w, "id"); JsonText.String(w, ic != null ? ic.Identity.InstrumentId : "DESTROYED"); w.Write(',');
                JsonText.Key(w, "type"); JsonText.String(w, ic != null ? SurgiTwinTokens.Token(ic.Identity.Type) : "UNKNOWN");
                w.Write('}');
            }
            w.Write("],");

            JsonText.Key(w, "samples");
            w.Write("[\n");
            for (int i = 0; i < count; i++)
            {
                ref TrajectorySample s = ref samples[i];
                if (i > 0) w.Write(",\n");
                InstrumentController ic = instruments[s.instrumentIndex];
                w.Write('{');
                JsonText.Key(w, "t"); JsonText.Number(w, s.time); w.Write(',');
                JsonText.Key(w, "instrumentId"); JsonText.String(w, ic != null ? ic.Identity.InstrumentId : "DESTROYED"); w.Write(',');
                JsonText.Key(w, "pos"); JsonText.Vector(w, s.position); w.Write(',');
                JsonText.Key(w, "rot"); JsonText.Quat(w, s.rotation); w.Write(',');
                JsonText.Key(w, "interactionState"); JsonText.String(w, SurgiTwinTokens.Token(s.instrumentState)); w.Write(',');
                JsonText.Key(w, "controlSource"); JsonText.String(w, SurgiTwinTokens.Token(s.controlSource)); w.Write(',');
                JsonText.Key(w, "controlSourceId"); JsonText.String(w, sourceIds[s.sourceIdIndex]); w.Write(',');
                JsonText.Key(w, "jaw"); JsonText.Number(w, s.jawClosure); w.Write(',');
                JsonText.Key(w, "tipContact"); JsonText.Bool(w, s.tipContact); w.Write(',');
                JsonText.Key(w, "tissueInteraction"); JsonText.Bool(w, s.tissueInteraction); w.Write(',');
                JsonText.Key(w, "patientState"); JsonText.String(w, SurgiTwinTokens.Token(s.patientState)); w.Write(',');
                JsonText.Key(w, "taskState"); JsonText.String(w, SurgiTwinTokens.Token(s.taskState));
                w.Write('}');
            }
            w.Write("\n]");
        }
    }
}
