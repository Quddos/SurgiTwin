using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Lets a jawed instrument pick up the needle: closing the jaws while the needle is within
    /// <see cref="graspRadius"/> of the grasp point attaches it; opening the jaws lets it go.
    /// The needle's control source is this component, so logs show which instrument holds it.
    /// Taking the needle from another instrument (hand-off) is allowed.
    /// </summary>
    public sealed class NeedleGrasp : MonoBehaviour, IInstrumentControlSource
    {
        [SerializeField] InstrumentController instrument;
        [SerializeField] Transform graspPoint;
        [SerializeField] float graspRadius = 0.02f;
        [SerializeField, Range(0f, 1f)] float closeThreshold = 0.7f;
        [SerializeField, Range(0f, 1f)] float openThreshold = 0.35f;

        NeedleBody held;
        float lastJaw;

        public ControlSourceType SourceType =>
            instrument != null && instrument.CurrentSource != null ? instrument.CurrentSource.SourceType : ControlSourceType.Scripted;

        public string SourceId => instrument != null ? instrument.Identity.InstrumentId : name;
        public NeedleBody HeldNeedle => held;

        void Awake()
        {
            if (instrument == null)
            {
                instrument = GetComponentInParent<InstrumentController>();
            }
            if (graspPoint == null && instrument != null)
            {
                graspPoint = instrument.Identity.GraspPoint;
            }
        }

        void OnDisable()
        {
            ReleaseNeedle();
        }

        void Update()
        {
            if (instrument == null)
            {
                return;
            }

            float j = instrument.JawClosure;
            if (held == null)
            {
                if (j >= closeThreshold && lastJaw < closeThreshold)
                {
                    TryGrasp();
                }
            }
            else if (j <= openThreshold || !ReferenceEquals(held.Controller.CurrentSource, this))
            {
                ReleaseNeedle();
            }
            lastJaw = j;
        }

        void TryGrasp()
        {
            NeedleBody best = null;
            float bestDistance = graspRadius;
            var needles = NeedleBody.Active;
            for (int i = 0; i < needles.Count; i++)
            {
                float d = needles[i].DistanceTo(graspPoint.position);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = needles[i];
                }
            }
            if (best == null)
            {
                return;
            }

            InstrumentController needle = best.Controller;
            if (needle.CurrentSource is NeedleGrasp other && !ReferenceEquals(other, this))
            {
                other.ReleaseNeedle();
            }

            if (needle.TryBeginControl(this) && needle.AttachToDriver(this, graspPoint))
            {
                held = best;
            }
        }

        public void ReleaseNeedle()
        {
            if (held == null)
            {
                return;
            }
            NeedleBody n = held;
            held = null;
            n.Controller.EndControl(this);
        }
    }
}
