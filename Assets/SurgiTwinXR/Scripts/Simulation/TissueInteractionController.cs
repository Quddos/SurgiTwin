using System.Collections.Generic;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.Patient;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Tissue interaction zone (trigger volume around the tissue surface).
    /// For every instrument tip inside, the signed depth below the surface is measured:
    ///  - contact:     depth &gt;= -contactTolerance (touching the surface)
    ///  - interaction: depth &gt;= interactionDepth, or a needle tip below the surface
    /// Changes are reported to the instrument and the patient. A needle whose tip is below
    /// the surface is marked lodged so it stays in place when released.
    /// No soft-body deformation in Phase 1.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TissueInteractionController : MonoBehaviour
    {
        struct TipTrack
        {
            public InstrumentTip tip;
            public NeedleBody needle;
            public bool contact;
            public bool interacting;
            public bool lodged;
        }

        [SerializeField] PatientStateController patient;
        [Tooltip("Transform on the tissue surface; its up axis is the outward surface normal.")]
        [SerializeField] Transform surface;
        [SerializeField] float contactTolerance = 0.003f;
        [SerializeField] float interactionDepth = 0.004f;
        [SerializeField] string tissueId = "TRAINING_PAD_TISSUE";

        readonly List<TipTrack> tracks = new List<TipTrack>(4);

        public string TissueId => tissueId;
        public int ActiveContacts { get; private set; }
        public int ActiveInteractions { get; private set; }

        /// <summary>Signed depth of a point below the tissue surface (positive = inside).</summary>
        public float DepthOf(Vector3 worldPoint)
        {
            Transform s = surface != null ? surface : transform;
            return Vector3.Dot(s.position - worldPoint, s.up);
        }

        void OnTriggerEnter(Collider other)
        {
            var tip = other.GetComponent<InstrumentTip>();
            if (tip == null || IndexOf(tip) >= 0)
            {
                return;
            }
            NeedleBody needle = tip.IsNeedleTip && tip.Owner != null ? tip.Owner.GetComponent<NeedleBody>() : null;
            tracks.Add(new TipTrack { tip = tip, needle = needle });
        }

        void OnTriggerExit(Collider other)
        {
            var tip = other.GetComponent<InstrumentTip>();
            if (tip == null)
            {
                return;
            }
            int i = IndexOf(tip);
            if (i >= 0)
            {
                RemoveAt(i);
            }
        }

        void Update()
        {
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                TipTrack t = tracks[i];
                if (t.tip == null || !t.tip.isActiveAndEnabled || t.tip.Owner == null)
                {
                    RemoveAt(i);
                    continue;
                }

                float depth = DepthOf(t.tip.transform.position);
                bool contact = depth >= -contactTolerance;
                bool interacting = depth >= interactionDepth || (t.tip.IsNeedleTip && depth > 0f);
                bool lodged = t.needle != null && depth > 0f;
                Apply(ref t, contact, interacting, lodged);
                tracks[i] = t;
            }
        }

        void Apply(ref TipTrack t, bool contact, bool interacting, bool lodged)
        {
            InstrumentController owner = t.tip.Owner;
            if (contact != t.contact)
            {
                t.contact = contact;
                ActiveContacts += contact ? 1 : -1;
                if (owner != null) owner.ReportTipContact(contact);
                if (patient != null) patient.ReportInstrumentContact(contact);
            }
            if (interacting != t.interacting)
            {
                t.interacting = interacting;
                ActiveInteractions += interacting ? 1 : -1;
                if (owner != null) owner.ReportTissueInteraction(interacting);
                if (patient != null) patient.ReportTissueInteraction(interacting);
            }
            if (lodged != t.lodged)
            {
                t.lodged = lodged;
                if (t.needle != null) t.needle.SetLodged(lodged);
            }
        }

        void RemoveAt(int i)
        {
            TipTrack t = tracks[i];
            if (t.tip != null)
            {
                Apply(ref t, false, false, false);
            }
            else
            {
                // Tip destroyed: still balance the patient counters.
                if (t.contact) { ActiveContacts--; if (patient != null) patient.ReportInstrumentContact(false); }
                if (t.interacting) { ActiveInteractions--; if (patient != null) patient.ReportTissueInteraction(false); }
            }
            tracks.RemoveAt(i);
        }

        int IndexOf(InstrumentTip tip)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].tip == tip)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
