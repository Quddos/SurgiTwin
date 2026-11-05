using UnityEngine;

namespace SurgiTwinXR.Utilities
{
    /// <summary>
    /// Small procedural meshes used by the Phase 1 placeholder art (rings, curved needle).
    /// The editor builder saves the results as .asset files, so nothing is generated at runtime.
    /// </summary>
    public static class ProceduralMeshes
    {
        /// <summary>
        /// Point on a curved needle arc. The arc lies in the local XZ plane, centred on the
        /// origin. t = 0 is the swaged tail, t = 1 is the sharp tip.
        /// </summary>
        public static Vector3 NeedleArcPoint(float radius, float arcDegrees, float t)
        {
            float a = Mathf.Deg2Rad * arcDegrees * t;
            return new Vector3(radius * Mathf.Cos(a), 0f, radius * Mathf.Sin(a));
        }

        /// <summary>Torus whose axis is local +Z (the ring lies in the XY plane).</summary>
        public static Mesh Torus(string name, float majorRadius, float minorRadius, int segments = 40, int sides = 10)
        {
            var path = new Vector3[segments];
            var radii = new float[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                path[i] = new Vector3(Mathf.Cos(a) * majorRadius, Mathf.Sin(a) * majorRadius, 0f);
                radii[i] = minorRadius;
            }
            return Tube(name, path, radii, sides, true, Vector3.forward);
        }

        /// <summary>Tapered curved surgical needle, see <see cref="NeedleArcPoint"/>.</summary>
        public static Mesh CurvedNeedle(string name, float arcRadius, float arcDegrees, float wireRadius, int segments = 28, int sides = 8)
        {
            var path = new Vector3[segments + 1];
            var radii = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                path[i] = NeedleArcPoint(arcRadius, arcDegrees, t);
                // Body is uniform; the last 20% tapers to a sharp point.
                float taper = t < 0.8f ? 1f : Mathf.Lerp(1f, 0.05f, (t - 0.8f) / 0.2f);
                radii[i] = wireRadius * taper;
            }
            return Tube(name, path, radii, sides, false, Vector3.up);
        }

        /// <summary>
        /// Sweeps a circle along a polyline. <paramref name="reference"/> is a vector that is
        /// never parallel to the path tangent, used to build a stable frame.
        /// </summary>
        public static Mesh Tube(string name, Vector3[] path, float[] radii, int sides, bool closed, Vector3 reference)
        {
            int rings = path.Length;
            int ringVerts = sides + 1;
            var vertices = new Vector3[rings * ringVerts];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (int i = 0; i < rings; i++)
            {
                Vector3 prev = closed ? path[(i - 1 + rings) % rings] : path[Mathf.Max(0, i - 1)];
                Vector3 next = closed ? path[(i + 1) % rings] : path[Mathf.Min(rings - 1, i + 1)];
                Vector3 tangent = (next - prev).normalized;
                Vector3 n = Vector3.Cross(tangent, reference);
                if (n.sqrMagnitude < 1e-8f)
                {
                    n = Vector3.Cross(tangent, Vector3.right);
                }
                n.Normalize();
                Vector3 b = Vector3.Cross(tangent, n).normalized;

                for (int s = 0; s <= sides; s++)
                {
                    float a = Mathf.PI * 2f * s / sides;
                    Vector3 dir = n * Mathf.Cos(a) + b * Mathf.Sin(a);
                    int idx = i * ringVerts + s;
                    vertices[idx] = path[i] + dir * radii[i];
                    normals[idx] = dir;
                    uvs[idx] = new Vector2((float)s / sides, (float)i / (rings - 1));
                }
            }

            int segCount = closed ? rings : rings - 1;
            var triangles = new int[segCount * sides * 6];
            int tri = 0;
            for (int i = 0; i < segCount; i++)
            {
                int i0 = i * ringVerts;
                int i1 = ((i + 1) % rings) * ringVerts;
                for (int s = 0; s < sides; s++)
                {
                    // Wound so that faces point outward (Unity: clockwise = front).
                    triangles[tri++] = i0 + s;
                    triangles[tri++] = i0 + s + 1;
                    triangles[tri++] = i1 + s;
                    triangles[tri++] = i0 + s + 1;
                    triangles[tri++] = i1 + s + 1;
                    triangles[tri++] = i1 + s;
                }
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
