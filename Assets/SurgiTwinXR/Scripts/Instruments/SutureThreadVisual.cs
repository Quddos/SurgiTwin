using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Visual-only suture thread: a short verlet rope that trails from the needle tail.
    /// It has no collision and does not take part in the task logic (Phase 1 placeholder).
    /// All buffers are allocated once.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SutureThreadVisual : MonoBehaviour
    {
        [SerializeField] Transform anchor;
        [SerializeField, Range(4, 48)] int pointCount = 20;
        [SerializeField] float segmentLength = 0.02f;
        [SerializeField, Range(1, 8)] int constraintIterations = 4;
        [SerializeField] float damping = 0.96f;
        [SerializeField] float gravity = 3.0f;

        LineRenderer line;
        Vector3[] points;
        Vector3[] previous;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            points = new Vector3[pointCount];
            previous = new Vector3[pointCount];
            Vector3 start = anchor != null ? anchor.position : transform.position;
            for (int i = 0; i < pointCount; i++)
            {
                points[i] = start + Vector3.down * (segmentLength * i * 0.2f) + Vector3.back * (segmentLength * i * 0.8f);
                previous[i] = points[i];
            }
            line.positionCount = pointCount;
            line.SetPositions(points);
        }

        void LateUpdate()
        {
            if (anchor == null)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            Vector3 g = Vector3.down * (gravity * dt * dt);

            points[0] = anchor.position;
            previous[0] = points[0];
            for (int i = 1; i < pointCount; i++)
            {
                Vector3 velocity = (points[i] - previous[i]) * damping;
                previous[i] = points[i];
                points[i] += velocity + g;
            }

            for (int it = 0; it < constraintIterations; it++)
            {
                points[0] = anchor.position;
                for (int i = 1; i < pointCount; i++)
                {
                    Vector3 delta = points[i] - points[i - 1];
                    float len = delta.magnitude;
                    if (len < 1e-6f)
                    {
                        continue;
                    }
                    float error = (len - segmentLength) / len;
                    if (i == 1)
                    {
                        points[i] -= delta * error;
                    }
                    else
                    {
                        Vector3 c = delta * (error * 0.5f);
                        points[i - 1] += c;
                        points[i] -= c;
                    }
                }
            }

            line.SetPositions(points);
        }
    }
}
