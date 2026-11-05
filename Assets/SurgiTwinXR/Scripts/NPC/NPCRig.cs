using System;
using UnityEngine;

namespace SurgiTwinXR.NPC
{
    public enum NPCHand : byte
    {
        Left = 0,
        Right = 1
    }

    [Serializable]
    public sealed class NPCArm
    {
        public Transform shoulder;
        [Tooltip("Cylinder primitive stretched between shoulder and hand (placeholder).")]
        public Transform armVisual;
        public Transform hand;
        [Tooltip("Instruments attach here when the NPC grasps them.")]
        public Transform instrumentAttachPoint;
        public float armRadius = 0.035f;
    }

    /// <summary>
    /// Skeleton of the NPC instructor placeholder: root, head, camera mount, two arms with
    /// hands and instrument attachment points. It only holds references and keeps the
    /// placeholder arm visuals stretched between shoulder and hand. It has no behaviour of
    /// its own; see <see cref="INPCController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCRig : MonoBehaviour
    {
        [SerializeField] string npcId = "NPC_INSTRUCTOR_01";
        [SerializeField] Transform head;
        [SerializeField] Transform cameraMount;
        [SerializeField] NPCArm leftArm = new NPCArm();
        [SerializeField] NPCArm rightArm = new NPCArm();

        public string NpcId => npcId;
        public Transform Head => head;
        public Transform CameraMount => cameraMount;

        public NPCArm Arm(NPCHand hand) => hand == NPCHand.Left ? leftArm : rightArm;

        void LateUpdate()
        {
            UpdateArmVisual(leftArm);
            UpdateArmVisual(rightArm);
        }

        static void UpdateArmVisual(NPCArm arm)
        {
            if (arm == null || arm.shoulder == null || arm.hand == null || arm.armVisual == null)
            {
                return;
            }
            Vector3 a = arm.shoulder.position;
            Vector3 b = arm.hand.position;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f)
            {
                return;
            }
            arm.armVisual.SetPositionAndRotation((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len));
            // Unity cylinder primitive is 2 units tall and 1 unit wide.
            Vector3 parentScale = arm.armVisual.parent != null ? arm.armVisual.parent.lossyScale : Vector3.one;
            arm.armVisual.localScale = new Vector3(
                arm.armRadius * 2f / parentScale.x,
                len * 0.5f / parentScale.y,
                arm.armRadius * 2f / parentScale.z);
        }
    }
}
