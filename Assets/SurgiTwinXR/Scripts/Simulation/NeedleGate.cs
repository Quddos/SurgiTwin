using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// A ring the needle tip must pass through. The ring axis is the local Z axis.
    /// A pass is detected when the tip crosses the ring plane and the crossing point lies
    /// inside the inner radius (segment / plane intersection, so fast motion is not missed).
    /// Either direction counts. The gate transform must not be scaled.
    /// </summary>
    public sealed class NeedleGate : MonoBehaviour
    {
        public enum Visual { Pending, Next, Passed }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float innerRadius = 0.016f;
        [Tooltip("Ignore crossings where the tip jumped further than this in one frame (teleport / reset).")]
        [SerializeField] float maxStep = 0.08f;
        [SerializeField] Renderer ringRenderer;
        [SerializeField] Color pendingColor = new Color(0.55f, 0.60f, 0.65f);
        [SerializeField] Color nextColor = new Color(0.15f, 0.75f, 0.95f);
        [SerializeField] Color passedColor = new Color(0.25f, 0.85f, 0.40f);

        Vector3 lastLocal;
        bool hasLast;
        MaterialPropertyBlock block;

        public float InnerRadius => innerRadius;

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>Feed the tip position each frame; returns true on the frame the tip passes through.</summary>
        public bool UpdateAndCheckPass(Vector3 tipWorld)
        {
            Vector3 l = transform.InverseTransformPoint(tipWorld);
            bool passed = false;
            if (hasLast && (l - lastLocal).sqrMagnitude <= maxStep * maxStep)
            {
                bool crossed = (lastLocal.z < 0f && l.z >= 0f) || (lastLocal.z > 0f && l.z <= 0f);
                if (crossed)
                {
                    float t = lastLocal.z / (lastLocal.z - l.z);
                    Vector3 hit = Vector3.LerpUnclamped(lastLocal, l, t);
                    passed = hit.x * hit.x + hit.y * hit.y <= innerRadius * innerRadius;
                }
            }
            lastLocal = l;
            hasLast = true;
            return passed;
        }

        public void ResetTracking()
        {
            hasLast = false;
        }

        public void SetVisual(Visual v)
        {
            if (ringRenderer == null)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            Color c = v == Visual.Passed ? passedColor : v == Visual.Next ? nextColor : pendingColor;
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            ringRenderer.SetPropertyBlock(block);
        }
    }
}
