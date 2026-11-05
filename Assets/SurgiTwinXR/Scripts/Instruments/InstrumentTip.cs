using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Marks the trigger collider at the working end of an instrument. Tissue and target
    /// zones only react to colliders that carry this component, so handles and shafts
    /// never count as tissue contact.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class InstrumentTip : MonoBehaviour
    {
        [SerializeField] InstrumentController owner;
        [SerializeField] bool isNeedleTip;

        public InstrumentController Owner => owner != null ? owner : (owner = GetComponentInParent<InstrumentController>());
        public bool IsNeedleTip => isNeedleTip;
    }
}
