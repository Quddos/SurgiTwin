using System;
using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Cameras
{
    /// <summary>
    /// Registry of the three observation cameras, addressable by <see cref="CameraRole"/>.
    /// Start or stop a feed per role and get its RenderTexture. No network streaming here.
    /// The trainee's headset view itself is rendered by the OVRCameraRig eye camera, which
    /// this manager exposes as <see cref="HeadsetCamera"/> but never retargets.
    /// </summary>
    public sealed class SurgicalCameraManager : MonoBehaviour
    {
        static readonly ObservationCamera[] s_ByRole = new ObservationCamera[SurgiTwinTokens.CameraRoleCount];

        public static SurgicalCameraManager Instance { get; private set; }
        public static event Action<ObservationCamera> CameraRegistered;

        [SerializeField] Camera headsetCamera;
        [Tooltip("Feeds started automatically (e.g. for the in-theatre monitor).")]
        [SerializeField] CameraRole[] feedsOnStart = Array.Empty<CameraRole>();

        public Camera HeadsetCamera => headsetCamera;

        internal static void Register(ObservationCamera cam)
        {
            int i = (int)cam.Role;
            if (s_ByRole[i] != null && s_ByRole[i] != cam)
            {
                Debug.LogWarning($"[SurgiTwin] Two observation cameras use role {SurgiTwinTokens.Token(cam.Role)}; the latest wins.");
            }
            s_ByRole[i] = cam;
            CameraRegistered?.Invoke(cam);
        }

        internal static void Unregister(ObservationCamera cam)
        {
            int i = (int)cam.Role;
            if (s_ByRole[i] == cam)
            {
                s_ByRole[i] = null;
            }
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Start()
        {
            for (int i = 0; i < feedsOnStart.Length; i++)
            {
                StartFeed(feedsOnStart[i]);
            }
        }

        public ObservationCamera Get(CameraRole role) => s_ByRole[(int)role];

        public RenderTexture StartFeed(CameraRole role)
        {
            ObservationCamera cam = Get(role);
            if (cam == null)
            {
                Debug.LogWarning($"[SurgiTwin] No camera registered for {SurgiTwinTokens.Token(role)}.");
                return null;
            }
            return cam.BeginCapture();
        }

        public void StopFeed(CameraRole role)
        {
            ObservationCamera cam = Get(role);
            if (cam != null)
            {
                cam.EndCapture();
            }
        }

        public bool IsFeedActive(CameraRole role)
        {
            ObservationCamera cam = Get(role);
            return cam != null && cam.IsCapturing;
        }

        public RenderTexture GetFeedTexture(CameraRole role)
        {
            ObservationCamera cam = Get(role);
            return cam != null ? cam.Texture : null;
        }
    }
}
