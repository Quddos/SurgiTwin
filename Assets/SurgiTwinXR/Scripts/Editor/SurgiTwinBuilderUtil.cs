using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SurgiTwinXR.Utilities;

namespace SurgiTwinXR.EditorTools
{
    /// <summary>Helpers used by the Phase 1 builder: folders, materials, primitives, serialized field wiring.</summary>
    internal static class SurgiTwinBuilderUtil
    {
        public static readonly System.Text.StringBuilder Report = new System.Text.StringBuilder();
        public static int Errors;

        public static void Log(string msg)
        {
            Report.AppendLine(msg);
            Debug.Log("[SurgiTwin Builder] " + msg);
        }

        public static void Error(string msg)
        {
            Errors++;
            Report.AppendLine("ERROR: " + msg);
            Debug.LogError("[SurgiTwin Builder] " + msg);
        }

        // ------------------------------------------------------------------ folders

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ materials

        public static Material Mat(string folder, string name, Color color, float metallic = 0f, float smoothness = 0.35f,
            Color? emission = null, bool unlit = false)
        {
            string path = $"{folder}/{name}.mat";
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Error("URP shader not found; is URP the active pipeline?");
                shader = Shader.Find("Standard");
            }

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }

            m.SetColor("_BaseColor", color);
            if (!unlit)
            {
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Mesh SaveMesh(string folder, Mesh mesh)
        {
            string path = $"{folder}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ------------------------------------------------------------------ objects

        public static GameObject Empty(string name, Transform parent, Vector3 localPos, Vector3? euler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            return go;
        }

        /// <summary>Primitive leaf object. Collider removed unless <paramref name="keepCollider"/>.</summary>
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
            Material mat, Vector3? euler = null, bool keepCollider = false, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            if (!keepCollider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos,
            Vector3? euler = null, bool castShadows = false)
        {
            var go = Empty(name, parent, localPos, euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static T Mark<T>(T go, string note, string intendedAsset) where T : Object
        {
            var g = go as GameObject;
            if (g != null)
            {
                if (!g.name.StartsWith(PlaceholderMarker.NamePrefix))
                {
                    g.name = PlaceholderMarker.NamePrefix + g.name;
                }
                if (!g.TryGetComponent(out PlaceholderMarker marker))
                {
                    marker = g.AddComponent<PlaceholderMarker>();
                }
                marker.Configure(note, intendedAsset);
            }
            return go;
        }

        public static void SetStaticRecursive(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        // ------------------------------------------------------------------ serialized wiring

        static SerializedProperty Prop(Object target, string field, out SerializedObject so)
        {
            so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Error($"Field '{field}' not found on {target.GetType().Name} ({target.name}).");
            }
            return p;
        }

        public static void SetRef(Object target, string field, Object value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetRefs(Object target, string field, Object[] values)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string field, int value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string field, string value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector3(Object target, string field, Vector3 value)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnumArray(Object target, string field, int[] values)
        {
            var p = Prop(target, field, out var so);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).enumValueIndex = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
