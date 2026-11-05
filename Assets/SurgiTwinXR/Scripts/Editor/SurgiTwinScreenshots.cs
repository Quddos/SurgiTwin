using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurgiTwinXR.EditorTools
{
    /// <summary>
    /// Renders reference views of the Phase 1 scene to PNG (edit mode, no headset needed).
    /// Menu: SurgiTwin > Phase 1 > Capture Reference Screenshots.
    /// </summary>
    public static class SurgiTwinScreenshots
    {
        struct View
        {
            public string name;
            public Vector3 position;
            public Vector3 target;
            public float fov;
            public View(string n, Vector3 p, Vector3 t, float f) { name = n; position = p; target = t; fov = f; }
        }

        static readonly View[] Views =
        {
            new View("01_trainee_eye", new Vector3(0.78f, 1.62f, -0.10f), new Vector3(0f, 1.10f, -0.08f), 90f),
            new View("02_operative_field", new Vector3(0.38f, 1.36f, -0.10f), new Vector3(0f, 1.12f, -0.05f), 60f),
            new View("03_instrument_tray", new Vector3(0.95f, 1.35f, -0.30f), new Vector3(0.78f, 0.96f, -0.66f), 60f),
            new View("04_theatre_wide", new Vector3(2.6f, 2.2f, 2.2f), new Vector3(0f, 0.9f, -0.2f), 70f),
            new View("05_npc_side", new Vector3(-0.2f, 1.7f, -1.6f), new Vector3(-0.6f, 1.2f, -0.2f), 70f),
            new View("06_monitor", new Vector3(0.1f, 1.65f, 0.1f), new Vector3(-1.05f, 1.65f, 0.85f), 60f),
            new View("07_overhead_like", new Vector3(0.25f, 2.75f, -0.75f), new Vector3(0f, 1.15f, -0.05f), 72f)
        };

        [MenuItem("SurgiTwin/Phase 1/Capture Reference Screenshots", priority = 3)]
        static void Menu()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SurgiTwinAutomation", "shots");
            Debug.Log(CaptureAll(dir));
            EditorUtility.RevealInFinder(dir);
        }

        public static string CaptureAll(string dir)
        {
            var sb = new StringBuilder();
            if (EditorSceneManager.GetActiveScene().path != SurgiTwinPhase1Builder.ScenePath)
            {
                EditorSceneManager.OpenScene(SurgiTwinPhase1Builder.ScenePath, OpenSceneMode.Single);
            }
            Directory.CreateDirectory(dir);

            var go = new GameObject("SurgiTwin_ScreenshotCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                foreach (var v in Views)
                {
                    go.transform.position = v.position;
                    go.transform.rotation = Quaternion.LookRotation(v.target - v.position, Vector3.up);
                    cam.fieldOfView = v.fov;
                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    string path = Path.Combine(dir, v.name + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    sb.AppendLine("Saved " + path);
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(go);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            return sb.ToString();
        }
    }
}
