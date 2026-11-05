using UnityEngine;
using UnityEngine.XR;

namespace SurgiTwinXR.XR
{
    /// <summary>Logs the XR runtime state once at startup (useful in adb logcat on Quest).</summary>
    public sealed class XRRuntimeStatus : MonoBehaviour
    {
        public static bool IsXRDeviceActive => XRSettings.isDeviceActive;

        void Start()
        {
            Debug.Log($"[SurgiTwin XR] deviceActive={XRSettings.isDeviceActive} device='{XRSettings.loadedDeviceName}' " +
                      $"model='{SystemInfo.deviceModel}' platform={Application.platform} " +
                      $"refreshRate={OVRPlugin.systemDisplayFrequency:0}");
        }
    }
}
