using System;
using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Patient
{
    /// <summary>
    /// Patient state machine. Inputs come from the target area, the tissue zone and the task.
    /// The state is derived by priority each time an input changes:
    /// TASK_COMPLETED &gt; TISSUE_INTERACTION &gt; INSTRUMENT_CONTACT &gt; TARGET_SELECTED &gt; NORMAL.
    /// The target area is tinted per state for immediate visual feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatientStateController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer feedbackRenderer;
        [SerializeField] bool tintFeedbackRenderer = true;
        [SerializeField] Color normalColor = new Color(0.86f, 0.55f, 0.55f);
        [SerializeField] Color targetSelectedColor = new Color(0.95f, 0.75f, 0.45f);
        [SerializeField] Color instrumentContactColor = new Color(0.95f, 0.55f, 0.25f);
        [SerializeField] Color tissueInteractionColor = new Color(0.85f, 0.25f, 0.25f);
        [SerializeField] Color taskCompletedColor = new Color(0.40f, 0.80f, 0.50f);

        PatientState state = PatientState.Normal;
        int targetSelections;
        int contacts;
        int interactions;
        bool taskCompleted;
        MaterialPropertyBlock block;

        public PatientState State => state;
        public event Action<PatientState, PatientState> StateChanged;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            ApplyFeedback();
        }

        public void SetTargetSelected(bool selected)
        {
            targetSelections = Mathf.Max(0, targetSelections + (selected ? 1 : -1));
            Evaluate();
        }

        public void ReportInstrumentContact(bool contact)
        {
            contacts = Mathf.Max(0, contacts + (contact ? 1 : -1));
            Evaluate();
        }

        public void ReportTissueInteraction(bool active)
        {
            interactions = Mathf.Max(0, interactions + (active ? 1 : -1));
            Evaluate();
        }

        public void SetTaskCompleted(bool completed)
        {
            taskCompleted = completed;
            Evaluate();
        }

        void Evaluate()
        {
            PatientState next =
                taskCompleted ? PatientState.TaskCompleted :
                interactions > 0 ? PatientState.TissueInteraction :
                contacts > 0 ? PatientState.InstrumentContact :
                targetSelections > 0 ? PatientState.TargetSelected :
                PatientState.Normal;

            if (next == state)
            {
                return;
            }
            PatientState previous = state;
            state = next;
            ApplyFeedback();
            StateChanged?.Invoke(previous, next);
        }

        void ApplyFeedback()
        {
            if (!tintFeedbackRenderer || feedbackRenderer == null)
            {
                return;
            }
            Color c;
            switch (state)
            {
                case PatientState.TargetSelected: c = targetSelectedColor; break;
                case PatientState.InstrumentContact: c = instrumentContactColor; break;
                case PatientState.TissueInteraction: c = tissueInteractionColor; break;
                case PatientState.TaskCompleted: c = taskCompletedColor; break;
                default: c = normalColor; break;
            }
            feedbackRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            feedbackRenderer.SetPropertyBlock(block);
        }
    }
}
