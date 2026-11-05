using System;
using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.Patient;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Phase 1 needle-passing task: grasp the needle with an instrument and pass the needle
    /// tip through every <see cref="NeedleGate"/> (in order if configured).
    /// Progress = gates passed / gate count. Completion sets the patient to TASK_COMPLETED.
    /// </summary>
    public sealed class SurgicalTaskController : MonoBehaviour
    {
        [SerializeField] NeedlePassingTaskConfig config;
        [SerializeField] NeedleBody needle;
        [SerializeField] NeedleGate[] gates = Array.Empty<NeedleGate>();
        [SerializeField] PatientStateController patient;
        [Tooltip("Instruments returned to their start pose on ResetTask (if not held).")]
        [SerializeField] InstrumentController[] resettableInstruments = Array.Empty<InstrumentController>();

        TaskState state = TaskState.NotStarted;
        int nextGate;
        bool[] passed;
        float startTime;
        float endTime;
        string abortReason;

        public event Action<TaskState, TaskState> TaskStateChanged;
        /// <summary>(gate index, gates passed, gate count)</summary>
        public event Action<int, int, int> GatePassed;

        public TaskState State => state;
        public string TaskId => config != null ? config.taskId : "NEEDLE_PASSING";
        public string DisplayName => config != null ? config.displayName : "Needle passing";
        public string Instructions => config != null ? config.instructions : "";
        public int GateCount => gates.Length;
        public int GatesPassed { get; private set; }
        public int NextGateIndex => nextGate;
        public float Progress => gates.Length == 0 ? 0f : (float)GatesPassed / gates.Length;
        public string AbortReason => abortReason;

        public float ElapsedSeconds =>
            state == TaskState.InProgress ? Time.time - startTime :
            state == TaskState.Completed || state == TaskState.Aborted ? endTime - startTime : 0f;

        bool RequireOrder => config == null || config.requireOrder;
        bool AutoStart => config == null || config.autoStartOnFirstGrasp;

        void Awake()
        {
            passed = new bool[gates.Length];
        }

        void OnEnable()
        {
            if (needle != null)
            {
                needle.Controller.ControlSourceChanged += HandleNeedleSourceChanged;
            }
        }

        void OnDisable()
        {
            if (needle != null && needle.Controller != null)
            {
                needle.Controller.ControlSourceChanged -= HandleNeedleSourceChanged;
            }
        }

        void Start()
        {
            RefreshGateVisuals();
        }

        void HandleNeedleSourceChanged(InstrumentController c, IInstrumentControlSource previous, IInstrumentControlSource next)
        {
            if (next != null && state == TaskState.NotStarted && AutoStart)
            {
                StartTask();
            }
        }

        public void StartTask()
        {
            if (state == TaskState.InProgress)
            {
                return;
            }
            ClearProgress();
            startTime = Time.time;
            SetState(TaskState.InProgress);
        }

        public void AbortTask(string reason)
        {
            if (state != TaskState.InProgress)
            {
                return;
            }
            abortReason = reason;
            endTime = Time.time;
            SetState(TaskState.Aborted);
        }

        /// <summary>Back to NOT_STARTED; instruments that are not held return to their start pose.</summary>
        public void ResetTask()
        {
            ClearProgress();
            if (needle != null)
            {
                needle.ClearLodged();
                needle.Controller.ResetToInitialPose();
            }
            for (int i = 0; i < resettableInstruments.Length; i++)
            {
                if (resettableInstruments[i] != null)
                {
                    resettableInstruments[i].ResetToInitialPose();
                }
            }
            if (patient != null)
            {
                patient.SetTaskCompleted(false);
            }
            SetState(TaskState.NotStarted);
        }

        void ClearProgress()
        {
            nextGate = 0;
            GatesPassed = 0;
            abortReason = null;
            if (passed == null || passed.Length != gates.Length)
            {
                passed = new bool[gates.Length];
            }
            for (int i = 0; i < gates.Length; i++)
            {
                passed[i] = false;
                if (gates[i] != null)
                {
                    gates[i].ResetTracking();
                }
            }
            RefreshGateVisuals();
        }

        void Update()
        {
            if (state != TaskState.InProgress || needle == null)
            {
                return;
            }

            Vector3 tip = needle.Tip.position;
            for (int i = 0; i < gates.Length; i++)
            {
                NeedleGate gate = gates[i];
                if (gate == null)
                {
                    continue;
                }
                // Every gate is fed every frame so its crossing history stays current.
                bool crossed = gate.UpdateAndCheckPass(tip);
                if (!crossed || passed[i])
                {
                    continue;
                }
                if (RequireOrder && i != nextGate)
                {
                    continue;
                }
                MarkPassed(i);
                if (state != TaskState.InProgress)
                {
                    return;
                }
            }

            if (config != null && config.timeLimitSeconds > 0f && Time.time - startTime > config.timeLimitSeconds)
            {
                AbortTask("TIME_LIMIT");
            }
        }

        void MarkPassed(int index)
        {
            passed[index] = true;
            GatesPassed++;
            while (nextGate < gates.Length && passed[nextGate])
            {
                nextGate++;
            }
            RefreshGateVisuals();
            GatePassed?.Invoke(index, GatesPassed, gates.Length);

            if (GatesPassed >= gates.Length)
            {
                endTime = Time.time;
                if (patient != null)
                {
                    patient.SetTaskCompleted(true);
                }
                SetState(TaskState.Completed);
            }
        }

        void RefreshGateVisuals()
        {
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] == null)
                {
                    continue;
                }
                bool isPassed = passed != null && i < passed.Length && passed[i];
                NeedleGate.Visual v = isPassed ? NeedleGate.Visual.Passed :
                    (!RequireOrder || i == nextGate) ? NeedleGate.Visual.Next : NeedleGate.Visual.Pending;
                gates[i].SetVisual(v);
            }
        }

        void SetState(TaskState next)
        {
            if (next == state)
            {
                return;
            }
            TaskState previous = state;
            state = next;
            TaskStateChanged?.Invoke(previous, next);
        }
    }
}
