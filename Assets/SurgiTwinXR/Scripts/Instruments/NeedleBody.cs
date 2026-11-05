using System.Collections.Generic;
using SurgiTwinXR.Core;
using SurgiTwinXR.Utilities;
using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Curved surgical needle. The needle cannot be grabbed by hand: it is picked up by an
    /// instrument with a <see cref="NeedleGrasp"/> (needle holder, grasper).
    /// If the needle tip is inside tissue when it is let go it stays lodged in place,
    /// otherwise it falls under physics.
    /// </summary>
    [RequireComponent(typeof(InstrumentController))]
    public sealed class NeedleBody : MonoBehaviour
    {
        static readonly List<NeedleBody> s_Active = new List<NeedleBody>(2);
        public static List<NeedleBody> Active => s_Active;

        [SerializeField] Transform needleTip;
        [SerializeField] Transform needleTail;
        [SerializeField] float arcRadius = 0.013f;
        [SerializeField] float arcDegrees = 180f;
        [SerializeField, Range(4, 32)] int distanceSamples = 12;

        InstrumentController controller;
        Vector3[] localSamples;
        int lodgeCount;

        public InstrumentController Controller => controller != null ? controller : (controller = GetComponent<InstrumentController>());
        public Transform Tip => needleTip != null ? needleTip : transform;
        public Transform Tail => needleTail != null ? needleTail : transform;
        public bool IsLodged => lodgeCount > 0;

        void Awake()
        {
            controller = GetComponent<InstrumentController>();
            localSamples = new Vector3[distanceSamples];
            for (int i = 0; i < distanceSamples; i++)
            {
                localSamples[i] = ProceduralMeshes.NeedleArcPoint(arcRadius, arcDegrees, (float)i / (distanceSamples - 1));
            }
        }

        void OnEnable()
        {
            if (!s_Active.Contains(this))
            {
                s_Active.Add(this);
            }
            Controller.ControlSourceChanged += HandleControlSourceChanged;
        }

        void OnDisable()
        {
            s_Active.Remove(this);
            if (controller != null)
            {
                controller.ControlSourceChanged -= HandleControlSourceChanged;
            }
        }

        /// <summary>Shortest distance from a world point to the needle body (sampled along the arc).</summary>
        public float DistanceTo(Vector3 worldPoint)
        {
            float best = float.MaxValue;
            for (int i = 0; i < localSamples.Length; i++)
            {
                float d = (transform.TransformPoint(localSamples[i]) - worldPoint).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                }
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Called by the tissue zone while the needle tip is inside tissue.</summary>
        public void SetLodged(bool lodged)
        {
            lodgeCount = Mathf.Max(0, lodgeCount + (lodged ? 1 : -1));
            Controller.HoldPositionWhenDetached = IsLodged;
            if (Controller.CurrentSource == null)
            {
                ApplyFreeBodyState();
            }
        }

        public void ClearLodged()
        {
            lodgeCount = 0;
            Controller.HoldPositionWhenDetached = false;
        }

        void HandleControlSourceChanged(InstrumentController c, IInstrumentControlSource previous, IInstrumentControlSource next)
        {
            if (next == null)
            {
                ApplyFreeBodyState();
            }
        }

        void ApplyFreeBodyState()
        {
            Rigidbody rb = Controller.Body;
            if (rb != null)
            {
                rb.isKinematic = IsLodged;
            }
        }
    }
}
