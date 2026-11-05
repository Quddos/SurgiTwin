using SurgiTwinXR.Instruments;
using UnityEngine;

namespace SurgiTwinXR.NPC
{
    /// <summary>
    /// Command interface for the NPC instructor. In Phase 1 the only implementation is
    /// <see cref="NPCManualController"/>, driven by explicit commands (inspector, test code).
    /// A future VLA / policy controller will implement this same interface; the NPC never
    /// moves on its own unless something calls these methods.
    /// </summary>
    public interface INPCController
    {
        NPCRig Rig { get; }

        /// <summary>Always false in Phase 1: there is no autonomous behaviour.</summary>
        bool IsAutonomous { get; }

        void SetHandTarget(NPCHand hand, Vector3 position, Quaternion rotation);
        void ClearHandTarget(NPCHand hand);
        void SetLookTarget(Vector3 worldPosition);

        /// <summary>Grasps an instrument within reach of the hand. Fails if a human holds it.</summary>
        bool GraspInstrument(NPCHand hand, InstrumentController instrument);
        void ReleaseInstrument(NPCHand hand);
        void SetInstrumentJaw(NPCHand hand, float closure01);
        InstrumentController HeldInstrument(NPCHand hand);
    }
}
