using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Identity and anatomy of an instrument: ID used in every log record, type, and the
    /// named reference points (handle, shaft, tip, grasp point).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstrumentIdentity : MonoBehaviour
    {
        [SerializeField] string instrumentId = "INSTRUMENT_01";
        [SerializeField] InstrumentType instrumentType = InstrumentType.Grasper;
        [SerializeField] string displayName = "Instrument";
        [SerializeField] InstrumentDefinition definition;

        [Header("Reference points")]
        [SerializeField] Transform handle;
        [SerializeField] Transform shaft;
        [SerializeField] Transform tip;
        [SerializeField] Transform graspPoint;

        public string InstrumentId => instrumentId;
        public InstrumentType Type => instrumentType;
        public string DisplayName => displayName;
        public InstrumentDefinition Definition => definition;
        public Transform Handle => handle;
        public Transform Shaft => shaft;
        public Transform Tip => tip != null ? tip : transform;
        public Transform GraspPoint => graspPoint != null ? graspPoint : Tip;
    }
}
