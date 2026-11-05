using SurgiTwinXR.Instruments;
using UnityEngine;

namespace SurgiTwinXR.NPC
{
    /// <summary>
    /// Manual / scripted NPC controller for Phase 1 testing. It is NOT an AI: hands move only
    /// towards targets that are explicitly set (through <see cref="INPCController"/>, the
    /// optional test-target transforms, or the context-menu test commands).
    /// Hands move at a capped speed so motion stays readable.
    /// </summary>
    [RequireComponent(typeof(NPCRig))]
    public sealed class NPCManualController : MonoBehaviour, INPCController
    {
        [SerializeField] float handMaxSpeed = 0.5f;
        [SerializeField] float handMaxAngularSpeed = 180f;
        [SerializeField] float headTurnSpeed = 90f;
        [Tooltip("Max distance between hand and instrument handle for a grasp to succeed.")]
        [SerializeField] float graspReach = 0.15f;

        [Header("Manual test inputs (optional)")]
        [SerializeField] Transform lookTarget;
        [SerializeField] Transform leftHandTestTarget;
        [SerializeField] Transform rightHandTestTarget;
        [SerializeField] InstrumentController testInstrument;
        [SerializeField] NPCHand testHand = NPCHand.Right;

        struct HandState
        {
            public bool hasTarget;
            public Vector3 targetPosition;
            public Quaternion targetRotation;
            public Vector3 restLocalPosition;
            public Quaternion restLocalRotation;
        }

        NPCRig rig;
        HandState left;
        HandState right;
        NPCHandInstrumentDriver leftDriver;
        NPCHandInstrumentDriver rightDriver;
        bool hasLook;
        Vector3 lookPosition;
        Quaternion headRestLocal;

        public NPCRig Rig => rig != null ? rig : (rig = GetComponent<NPCRig>());
        public bool IsAutonomous => false;

        void Awake()
        {
            rig = GetComponent<NPCRig>();
            leftDriver = new NPCHandInstrumentDriver(rig.NpcId, NPCHand.Left);
            rightDriver = new NPCHandInstrumentDriver(rig.NpcId, NPCHand.Right);
            CaptureRest(ref left, rig.Arm(NPCHand.Left).hand);
            CaptureRest(ref right, rig.Arm(NPCHand.Right).hand);
            if (rig.Head != null)
            {
                headRestLocal = rig.Head.localRotation;
            }
        }

        static void CaptureRest(ref HandState s, Transform hand)
        {
            if (hand == null)
            {
                return;
            }
            s.restLocalPosition = hand.localPosition;
            s.restLocalRotation = hand.localRotation;
        }

        // ------------------------------------------------------------------ INPCController

        public void SetHandTarget(NPCHand hand, Vector3 position, Quaternion rotation)
        {
            ref HandState s = ref State(hand);
            s.hasTarget = true;
            s.targetPosition = position;
            s.targetRotation = rotation;
        }

        public void ClearHandTarget(NPCHand hand)
        {
            State(hand).hasTarget = false;
        }

        public void SetLookTarget(Vector3 worldPosition)
        {
            hasLook = true;
            lookPosition = worldPosition;
        }

        public bool GraspInstrument(NPCHand hand, InstrumentController instrument)
        {
            if (instrument == null)
            {
                return false;
            }
            NPCArm arm = Rig.Arm(hand);
            Transform reference = instrument.Identity.Handle != null ? instrument.Identity.Handle : instrument.transform;
            float distance = Vector3.Distance(arm.hand.position, reference.position);
            if (distance > graspReach)
            {
                Debug.Log($"[SurgiTwin NPC] {instrument.Identity.InstrumentId} is {distance:0.00} m from the {hand} hand (reach {graspReach:0.00} m). Move the hand first.");
                return false;
            }
            bool ok = Driver(hand).Grasp(instrument, arm.instrumentAttachPoint != null ? arm.instrumentAttachPoint : arm.hand);
            if (!ok)
            {
                Debug.Log($"[SurgiTwin NPC] Could not take {instrument.Identity.InstrumentId}: it is controlled by {instrument.CurrentSourceId}.");
            }
            return ok;
        }

        public void ReleaseInstrument(NPCHand hand) => Driver(hand).Release();

        public void SetInstrumentJaw(NPCHand hand, float closure01) => Driver(hand).SetJaw(closure01);

        public InstrumentController HeldInstrument(NPCHand hand) => Driver(hand).Held as InstrumentController;

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (leftHandTestTarget != null) SetHandTarget(NPCHand.Left, leftHandTestTarget.position, leftHandTestTarget.rotation);
            if (rightHandTestTarget != null) SetHandTarget(NPCHand.Right, rightHandTestTarget.position, rightHandTestTarget.rotation);
            if (lookTarget != null) SetLookTarget(lookTarget.position);

            float dt = Time.deltaTime;
            MoveHand(Rig.Arm(NPCHand.Left).hand, ref left, dt);
            MoveHand(Rig.Arm(NPCHand.Right).hand, ref right, dt);
            TurnHead(dt);
        }

        void MoveHand(Transform hand, ref HandState s, float dt)
        {
            if (hand == null)
            {
                return;
            }
            Vector3 targetPos;
            Quaternion targetRot;
            if (s.hasTarget)
            {
                targetPos = s.targetPosition;
                targetRot = s.targetRotation;
            }
            else
            {
                Transform parent = hand.parent;
                targetPos = parent != null ? parent.TransformPoint(s.restLocalPosition) : s.restLocalPosition;
                targetRot = parent != null ? parent.rotation * s.restLocalRotation : s.restLocalRotation;
            }
            hand.SetPositionAndRotation(
                Vector3.MoveTowards(hand.position, targetPos, handMaxSpeed * dt),
                Quaternion.RotateTowards(hand.rotation, targetRot, handMaxAngularSpeed * dt));
        }

        void TurnHead(float dt)
        {
            Transform head = Rig.Head;
            if (head == null)
            {
                return;
            }
            Quaternion target;
            if (hasLook)
            {
                Vector3 dir = lookPosition - head.position;
                if (dir.sqrMagnitude < 1e-6f)
                {
                    return;
                }
                target = Quaternion.LookRotation(dir, Vector3.up);
            }
            else
            {
                target = head.parent != null ? head.parent.rotation * headRestLocal : headRestLocal;
            }
            head.rotation = Quaternion.RotateTowards(head.rotation, target, headTurnSpeed * dt);
        }

        ref HandState State(NPCHand hand)
        {
            if (hand == NPCHand.Left)
            {
                return ref left;
            }
            return ref right;
        }

        NPCHandInstrumentDriver Driver(NPCHand hand) => hand == NPCHand.Left ? leftDriver : rightDriver;

        // ------------------------------------------------------------------ manual test commands

        [ContextMenu("NPC Test/Move test hand to test instrument")]
        void TestMoveHandToInstrument()
        {
            if (testInstrument == null) return;
            Transform h = testInstrument.Identity.Handle != null ? testInstrument.Identity.Handle : testInstrument.transform;
            SetHandTarget(testHand, h.position, Rig.Arm(testHand).hand.rotation);
        }

        [ContextMenu("NPC Test/Grasp test instrument")]
        void TestGrasp()
        {
            if (testInstrument != null) GraspInstrument(testHand, testInstrument);
        }

        [ContextMenu("NPC Test/Lift test hand 15 cm")]
        void TestLift()
        {
            Transform h = Rig.Arm(testHand).hand;
            SetHandTarget(testHand, h.position + Vector3.up * 0.15f, h.rotation);
        }

        [ContextMenu("NPC Test/Close jaws")]
        void TestCloseJaw() => SetInstrumentJaw(testHand, 1f);

        [ContextMenu("NPC Test/Open jaws")]
        void TestOpenJaw() => SetInstrumentJaw(testHand, 0f);

        [ContextMenu("NPC Test/Release instrument")]
        void TestRelease() => ReleaseInstrument(testHand);

        [ContextMenu("NPC Test/Return hands to rest")]
        void TestRest()
        {
            ClearHandTarget(NPCHand.Left);
            ClearHandTarget(NPCHand.Right);
        }
    }
}
