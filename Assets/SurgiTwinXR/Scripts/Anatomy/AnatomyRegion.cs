using UnityEngine;

namespace SurgiTwinXR.Anatomy
{
    public enum AnatomyRegionId : byte
    {
        Head = 0,
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        Abdomen = 4,
        AbdominalSurface = 5,
        SurgicalTargetArea = 6,
        Pelvis = 7,
        Legs = 8
    }

    /// <summary>Labels a body part so contacts and future anatomy data can be attributed to a region.</summary>
    [DisallowMultipleComponent]
    public sealed class AnatomyRegion : MonoBehaviour
    {
        [SerializeField] AnatomyRegionId region;
        [SerializeField] bool isPlaceholderGeometry = true;

        public AnatomyRegionId Region => region;
        public bool IsPlaceholderGeometry => isPlaceholderGeometry;
    }
}
