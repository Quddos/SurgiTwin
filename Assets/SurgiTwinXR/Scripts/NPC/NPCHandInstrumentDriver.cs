using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;

namespace SurgiTwinXR.NPC
{
    /// <summary>
    /// The NPC side of instrument control: one driver per NPC hand. It drives the same
    /// <see cref="IInstrumentControl"/> as the human XR input, through the same methods.
    /// </summary>
    public sealed class NPCHandInstrumentDriver : IInstrumentControlSource
    {
        readonly string sourceId;
        IInstrumentControl held;

        public NPCHandInstrumentDriver(string npcId, NPCHand hand)
        {
            sourceId = npcId + (hand == NPCHand.Left ? "_LEFT_HAND" : "_RIGHT_HAND");
        }

        public ControlSourceType SourceType => ControlSourceType.NPC;
        public string SourceId => sourceId;
        public IInstrumentControl Held => held;

        public bool Grasp(IInstrumentControl instrument, UnityEngine.Transform attachPoint)
        {
            if (instrument == null || held != null)
            {
                return false;
            }
            if (!instrument.TryBeginControl(this))
            {
                return false;
            }
            if (!instrument.AttachToDriver(this, attachPoint))
            {
                instrument.EndControl(this);
                return false;
            }
            held = instrument;
            return true;
        }

        public void Release()
        {
            if (held == null)
            {
                return;
            }
            held.EndControl(this);
            held = null;
        }

        public void SetJaw(float closure01)
        {
            held?.SetJawClosure(this, closure01);
        }
    }
}
