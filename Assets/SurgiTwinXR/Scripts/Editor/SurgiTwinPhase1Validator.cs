using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using SurgiTwinXR.Cameras;
using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.Patient;
using SurgiTwinXR.Simulation;
using SurgiTwinXR.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurgiTwinXR.EditorTools
{
    /// <summary>
    /// Static checks on the Phase 1 scene: wiring, unique IDs, XR rig, cameras, and a rough
    /// rendering budget. Menu: SurgiTwin > Phase 1 > Validate Scene.
    /// </summary>
    public static class SurgiTwinPhase1Validator
    {
        // Optional fields that may legitimately be empty.
        static readonly HashSet<string> OptionalFields = new HashSet<string>
        {
            "lowerJaw", "leftHandTestTarget", "rightHandTestTarget", "handle", "shaft", "definition",
            "label", "headsetCamera", "lookTarget", "testInstrument", "m_Script"
        };

        [MenuItem("SurgiTwin/Phase 1/Validate Scene", priority = 2)]
        static void ValidateMenu()
        {
            string report = Validate(out int errors, out int warnings);
            Debug.Log(report);
            EditorUtility.DisplayDialog("SurgiTwin Phase 1 validation",
                $"{errors} error(s), {warnings} warning(s). Full report in the Console.", "OK");
        }

        public static string Validate(out int errors, out int warnings)
        {
            var sb = new StringBuilder();
            int e = 0, w = 0;
            void Err(string m) { e++; sb.AppendLine("  [ERROR] " + m); }
            void Warn(string m) { w++; sb.AppendLine("  [WARN]  " + m); }
            void Ok(string m) { sb.AppendLine("  [OK]    " + m); }

            sb.AppendLine("SurgiTwin-XR Phase 1 scene validation");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SurgiTwinPhase1Builder.ScenePath)
            {
                if (System.IO.File.Exists(SurgiTwinPhase1Builder.ScenePath))
                {
                    scene = EditorSceneManager.OpenScene(SurgiTwinPhase1Builder.ScenePath, OpenSceneMode.Single);
                }
                else
                {
                    Err("Scene not found: " + SurgiTwinPhase1Builder.ScenePath + ". Run SurgiTwin > Phase 1 > Build Operating Theatre.");
                    errors = e; warnings = w;
                    return sb.ToString();
                }
            }
            Ok("Scene: " + scene.path);

            // Missing scripts
            int missing = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                }
            }
            if (missing > 0) Err($"{missing} missing script reference(s)."); else Ok("No missing scripts.");

            // XR rig
            var managers = Object.FindObjectsByType<OVRManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rigs = Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers.Length == 1 && rigs.Length == 1) Ok("One OVRCameraRig with OVRManager.");
            else Err($"Expected 1 OVRManager and 1 OVRCameraRig, found {managers.Length} and {rigs.Length}.");
            var rigRefs = Object.FindObjectsByType<OVRCameraRigRef>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int unwired = rigRefs.Count(r => new SerializedObject(r).FindProperty("_ovrCameraRig")?.objectReferenceValue == null);
            if (rigRefs.Length == 0) Err("No OVRCameraRigRef found: interaction rig missing.");
            else if (unwired > 0) Err($"{unwired} OVRCameraRigRef component(s) not wired to the OVRCameraRig.");
            else Ok($"Interaction rig wired ({rigRefs.Length} OVRCameraRigRef).");
            if (managers.Length > 0)
            {
                var origin = new SerializedObject(managers[0]).FindProperty("_trackingOriginType");
                if (origin != null) Ok("Tracking origin: " + origin.enumDisplayNames[origin.enumValueIndex]);
            }

            // Serialized references on SurgiTwin components
            int nullRefs = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("SurgiTwinXR")) continue;
                var so = new SerializedObject(mb);
                var p = so.GetIterator();
                while (p.NextVisible(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue != null) continue;
                    string leaf = p.name;
                    if (OptionalFields.Contains(leaf)) continue;
                    if (p.propertyPath.Contains("Array.data")) { nullRefs++; Warn($"{mb.GetType().Name} on '{mb.name}': empty array element {p.propertyPath}"); continue; }
                    nullRefs++;
                    Warn($"{mb.GetType().Name} on '{mb.name}': '{p.propertyPath}' is not assigned.");
                }
            }
            if (nullRefs == 0) Ok("All required SurgiTwin references assigned.");

            // Instruments
            var instruments = Object.FindObjectsByType<InstrumentController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var ids = new HashSet<string>();
            foreach (var ic in instruments)
            {
                var id = ic.GetComponent<InstrumentIdentity>();
                if (!ids.Add(id.InstrumentId)) Err("Duplicate instrument ID " + id.InstrumentId);
                if (ic.GetComponent<Rigidbody>() == null) Err(id.InstrumentId + " has no Rigidbody.");
                if (id.Type != InstrumentType.Needle)
                {
                    bool grab = ic.GetComponent<Grabbable>() != null && ic.GetComponent<GrabInteractable>() != null && ic.GetComponent<HumanXRInstrumentInput>() != null;
                    if (!grab) Err(id.InstrumentId + " is missing Grabbable / GrabInteractable / HumanXRInstrumentInput.");
                }
                if (ic.GetComponentInChildren<InstrumentTip>() == null) Err(id.InstrumentId + " has no InstrumentTip.");
            }
            Ok($"Instruments: {instruments.Length} ({string.Join(", ", ids)})");

            // Patient / task
            var patient = Object.FindFirstObjectByType<VirtualPatient>();
            if (patient == null) Err("VirtualPatient missing."); else Ok("VirtualPatient present.");
            var task = Object.FindFirstObjectByType<SurgicalTaskController>();
            if (task == null) Err("SurgicalTaskController missing.");
            else
            {
                int gates = new SerializedObject(task).FindProperty("gates").arraySize;
                if (gates == 0) Err("Task has no gates."); else Ok($"Needle-passing task with {gates} rings.");
            }
            if (Object.FindFirstObjectByType<SurgicalSessionManager>() == null) Err("SurgicalSessionManager missing.");
            if (Object.FindFirstObjectByType<InstrumentTrajectoryRecorder>() == null) Err("InstrumentTrajectoryRecorder missing.");

            // Cameras
            var cams = Object.FindObjectsByType<ObservationCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < SurgiTwinTokens.CameraRoleCount; i++)
            {
                var role = (CameraRole)i;
                int n = cams.Count(c => c.Role == role);
                if (n == 1) Ok("Camera role " + SurgiTwinTokens.Token(role));
                else Err($"Camera role {SurgiTwinTokens.Token(role)}: {n} cameras (expected 1).");
            }

            // Build settings
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length > 0 && scenes[0].path == SurgiTwinPhase1Builder.ScenePath && scenes[0].enabled) Ok("Scene is first in Build Settings.");
            else Err("Phase 1 scene is not the first enabled scene in Build Settings.");

            // Rendering budget (rough, static)
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            long tris = 0;
            var mats = new HashSet<Material>();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.LongLength / 3;
                foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
            }
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"  [INFO]  Renderers: {renderers.Length}, triangles: {tris:N0}, materials: {mats.Count}, lights: {lights.Length} " +
                          $"(realtime shadow casters: {lights.Count(l => l.shadows != LightShadows.None)})");
            if (tris > 300000) Warn("Triangle count is high for Quest 3S (aim for < 300k).");
            if (renderers.Length > 400) Warn("Many renderers; check draw calls with the Frame Debugger / OVR Metrics Tool.");

            sb.AppendLine($"Result: {e} error(s), {w} warning(s).");
            errors = e; warnings = w;
            return sb.ToString();
        }
    }
}
