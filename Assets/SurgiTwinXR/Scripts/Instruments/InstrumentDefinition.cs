using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>Static, data-only description of an instrument type.</summary>
    [CreateAssetMenu(menuName = "SurgiTwinXR/Instrument Definition", fileName = "InstrumentDefinition")]
    public sealed class InstrumentDefinition : ScriptableObject
    {
        public InstrumentType instrumentType;
        public string displayName = "Instrument";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Nominal shaft length in metres (placeholder geometry uses this).")]
        public float shaftLength = 0.30f;
        [Tooltip("Nominal shaft diameter in metres (5 mm laparoscopic instrument = 0.005).")]
        public float shaftDiameter = 0.005f;
        public bool hasJaws = true;
        public bool canGraspNeedle;
    }
}
