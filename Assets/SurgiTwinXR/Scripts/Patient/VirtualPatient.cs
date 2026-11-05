using UnityEngine;

namespace SurgiTwinXR.Patient
{
    /// <summary>
    /// Root of the virtual patient (PatientRoot). Holds named references to the body
    /// regions so other systems never search the hierarchy by name at runtime.
    /// Phase 1 geometry is placeholder primitives (supine adult, head towards +Z).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VirtualPatient : MonoBehaviour
    {
        [SerializeField] PatientStateController stateController;
        [SerializeField] Transform head;
        [SerializeField] Transform torso;
        [SerializeField] Transform leftArm;
        [SerializeField] Transform rightArm;
        [SerializeField] Transform abdomen;
        [SerializeField] Transform abdominalSurface;
        [SerializeField] Transform surgicalTargetArea;
        [SerializeField] Transform tissueInteractionZone;

        public PatientStateController StateController => stateController;
        public Transform Head => head;
        public Transform Torso => torso;
        public Transform LeftArm => leftArm;
        public Transform RightArm => rightArm;
        public Transform Abdomen => abdomen;
        public Transform AbdominalSurface => abdominalSurface;
        public Transform SurgicalTargetArea => surgicalTargetArea;
        public Transform TissueInteractionZone => tissueInteractionZone;
    }
}
