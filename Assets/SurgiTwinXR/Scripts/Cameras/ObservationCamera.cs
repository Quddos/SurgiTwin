using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Cameras
{
    /// <summary>
    /// One addressable observation viewpoint (trainee POV, NPC POV, overhead).
    /// Observation cameras never render to the headset: they are disabled until a feed is
    /// requested, then render into their own RenderTexture at <see cref="captureRateHz"/>.
    /// Phase 1 only provides the textures; streaming them (WebRTC) is Phase 2+.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ObservationCamera : MonoBehaviour
    {
        [SerializeField] CameraRole role;
        [SerializeField] int textureWidth = 640;
        [SerializeField] int textureHeight = 360;
        [Tooltip("Renders per second while capturing. 0 = every frame.")]
        [SerializeField] float captureRateHz = 15f;

        Camera cam;
        RenderTexture texture;
        bool capturing;
        float nextCaptureTime;

        public CameraRole Role => role;
        public Camera Camera => cam != null ? cam : (cam = GetComponent<Camera>());
        public RenderTexture Texture => texture;
        public bool IsCapturing => capturing;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.enabled = false;
        }

        void OnEnable()
        {
            SurgicalCameraManager.Register(this);
        }

        void OnDisable()
        {
            SurgicalCameraManager.Unregister(this);
            if (cam != null)
            {
                cam.enabled = false;
            }
        }

        void OnDestroy()
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }

        public RenderTexture BeginCapture()
        {
            if (texture == null)
            {
                texture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "RT_" + SurgiTwinTokens.Token(role),
                    antiAliasing = 1,
                    useMipMap = false
                };
                texture.Create();
            }
            Camera.targetTexture = texture;
            capturing = true;
            nextCaptureTime = 0f;
            return texture;
        }

        public void EndCapture()
        {
            capturing = false;
            Camera.enabled = false;
        }

        void Update()
        {
            if (!capturing)
            {
                return;
            }
            if (captureRateHz <= 0f)
            {
                cam.enabled = true;
                return;
            }
            // Enable the camera for exactly one frame per capture interval.
            float now = Time.unscaledTime;
            if (now >= nextCaptureTime)
            {
                cam.enabled = true;
                nextCaptureTime = now + 1f / captureRateHz;
            }
            else
            {
                cam.enabled = false;
            }
        }
    }
}
