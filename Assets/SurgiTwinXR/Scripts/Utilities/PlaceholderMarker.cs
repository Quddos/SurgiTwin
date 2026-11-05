using UnityEngine;

namespace SurgiTwinXR.Utilities
{
    /// <summary>
    /// Marks a GameObject hierarchy as placeholder geometry (Unity primitives) that is
    /// expected to be replaced by a production asset. Objects carrying this marker are
    /// also named with the "PH_" prefix so they are easy to find in the hierarchy.
    /// See Documentation/BLENDER_ASSET_WORKFLOW.md for the replacement process.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaceholderMarker : MonoBehaviour
    {
        public const string NamePrefix = "PH_";

        [SerializeField, TextArea(2, 5)]
        string note = "Placeholder primitive geometry. Replace with a Blender-authored asset.";

        [SerializeField]
        string intendedAsset = "";

        public string Note => note;
        public string IntendedAsset => intendedAsset;

        public void Configure(string noteText, string assetName)
        {
            note = noteText;
            intendedAsset = assetName;
        }
    }
}
