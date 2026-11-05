using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Opens and closes the jaw transforms from <see cref="InstrumentController.JawClosure"/>.
    /// The jaws' rest rotation (as authored) is the closed pose.
    /// Single-action instruments (needle holder) only move the upper jaw.
    /// </summary>
    public sealed class InstrumentJawVisual : MonoBehaviour
    {
        [SerializeField] InstrumentController controller;
        [SerializeField] Transform upperJaw;
        [SerializeField] Transform lowerJaw;
        [SerializeField] float openAngle = 24f;
        [SerializeField] Vector3 hingeAxis = Vector3.right;

        Quaternion upperRest = Quaternion.identity;
        Quaternion lowerRest = Quaternion.identity;
        float applied = -1f;

        void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<InstrumentController>();
            }
            if (upperJaw != null)
            {
                upperRest = upperJaw.localRotation;
            }
            if (lowerJaw != null)
            {
                lowerRest = lowerJaw.localRotation;
            }
        }

        void LateUpdate()
        {
            if (controller == null)
            {
                return;
            }

            float closure = controller.JawClosure;
            if (Mathf.Abs(closure - applied) < 0.002f)
            {
                return;
            }
            applied = closure;

            float angle = Mathf.Lerp(openAngle, 0f, closure);
            bool doubleAction = lowerJaw != null;
            float upperAngle = doubleAction ? angle * 0.5f : angle;

            if (upperJaw != null)
            {
                upperJaw.localRotation = upperRest * Quaternion.AngleAxis(-upperAngle, hingeAxis);
            }
            if (lowerJaw != null)
            {
                lowerJaw.localRotation = lowerRest * Quaternion.AngleAxis(angle * 0.5f, hingeAxis);
            }
        }
    }
}
