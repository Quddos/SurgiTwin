using System.Collections.Generic;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.Patient;
using UnityEngine;

namespace SurgiTwinXR.Anatomy
{
    /// <summary>
    /// Approach volume above the surgical target. An instrument tip entering it selects the
    /// target (patient state TARGET_SELECTED). Requires a trigger collider on this object.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SurgicalTargetArea : MonoBehaviour
    {
        [SerializeField] PatientStateController patient;
        [SerializeField] string targetId = "ABDOMINAL_TRAINING_PAD";

        readonly List<InstrumentTip> tipsInside = new List<InstrumentTip>(4);

        public string TargetId => targetId;
        public int TipsInside => tipsInside.Count;

        void OnTriggerEnter(Collider other)
        {
            var tip = other.GetComponent<InstrumentTip>();
            if (tip == null || tipsInside.Contains(tip))
            {
                return;
            }
            tipsInside.Add(tip);
            if (patient != null)
            {
                patient.SetTargetSelected(true);
            }
        }

        void OnTriggerExit(Collider other)
        {
            var tip = other.GetComponent<InstrumentTip>();
            if (tip == null || !tipsInside.Remove(tip))
            {
                return;
            }
            if (patient != null)
            {
                patient.SetTargetSelected(false);
            }
        }

        void FixedUpdate()
        {
            // OnTriggerExit is not raised for colliders that are disabled or destroyed while inside.
            for (int i = tipsInside.Count - 1; i >= 0; i--)
            {
                InstrumentTip tip = tipsInside[i];
                if (tip == null || !tip.isActiveAndEnabled)
                {
                    tipsInside.RemoveAt(i);
                    if (patient != null)
                    {
                        patient.SetTargetSelected(false);
                    }
                }
            }
        }
    }
}
