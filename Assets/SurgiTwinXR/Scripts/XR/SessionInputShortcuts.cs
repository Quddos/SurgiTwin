using SurgiTwinXR.Cameras;
using SurgiTwinXR.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurgiTwinXR.XR
{
    /// <summary>
    /// Session shortcuts.
    /// Quest controllers: Y (left) = reset task, B (right) = export session log,
    ///                    left thumbstick click = cycle monitor feed.
    /// Keyboard (editor): R = reset task, E = export log, T = start task, C = cycle monitor feed.
    /// </summary>
    public sealed class SessionInputShortcuts : MonoBehaviour
    {
        [SerializeField] SurgicalTaskController task;
        [SerializeField] SurgicalSessionManager session;
        [SerializeField] MonitorFeedDisplay monitor;

        void Update()
        {
            bool reset = OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch);
            bool export = OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch);
            bool cycle = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch);
            bool start = false;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                reset |= kb.rKey.wasPressedThisFrame;
                export |= kb.eKey.wasPressedThisFrame;
                cycle |= kb.cKey.wasPressedThisFrame;
                start = kb.tKey.wasPressedThisFrame;
            }

            if (reset && task != null) task.ResetTask();
            if (start && task != null) task.StartTask();
            if (export && session != null) session.ExportSession();
            if (cycle && monitor != null) monitor.CycleRole();
        }
    }
}
