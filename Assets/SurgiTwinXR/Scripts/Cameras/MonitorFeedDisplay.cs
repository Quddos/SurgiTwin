using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Cameras
{
    /// <summary>
    /// Shows one observation camera feed on an in-theatre monitor screen.
    /// Press-to-cycle is exposed via <see cref="CycleRole"/> for debugging.
    /// </summary>
    public sealed class MonitorFeedDisplay : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] Renderer screen;
        [SerializeField] CameraRole role = CameraRole.NPCPOV;
        [SerializeField] bool showOnStart = true;
        [SerializeField] TextMesh label;

        MaterialPropertyBlock block;
        bool showing;

        public CameraRole Role => role;

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        void Start()
        {
            if (showOnStart)
            {
                Show(role);
            }
        }

        public void Show(CameraRole newRole)
        {
            SurgicalCameraManager manager = SurgicalCameraManager.Instance;
            if (manager == null || screen == null)
            {
                return;
            }
            if (showing && newRole != role)
            {
                manager.StopFeed(role);
            }
            role = newRole;
            RenderTexture rt = manager.StartFeed(role);
            showing = rt != null;
            screen.GetPropertyBlock(block);
            if (rt != null)
            {
                block.SetTexture(BaseMapId, rt);
            }
            screen.SetPropertyBlock(block);
            if (label != null)
            {
                label.text = "FEED: " + SurgiTwinTokens.Token(role);
            }
        }

        public void CycleRole()
        {
            Show((CameraRole)(((int)role + 1) % SurgiTwinTokens.CameraRoleCount));
        }
    }
}
