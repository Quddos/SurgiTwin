using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// Anything that can drive an instrument: the human XR trainee, the NPC instructor,
    /// or another instrument (a needle holder grasping the needle).
    /// Each driver is a separate implementation; they all talk to the same
    /// <see cref="IInstrumentControl"/> so the instrument behaves identically for every source.
    /// </summary>
    public interface IInstrumentControlSource
    {
        ControlSourceType SourceType { get; }
        string SourceId { get; }
    }

    /// <summary>
    /// The single control surface of an instrument. Implemented by <see cref="InstrumentController"/>.
    /// Only the current control source may change jaw closure or attach the instrument to a driver.
    /// </summary>
    public interface IInstrumentControl
    {
        InstrumentIdentity Identity { get; }
        InstrumentState State { get; }
        IInstrumentControlSource CurrentSource { get; }
        float JawClosure { get; }

        /// <summary>Claims the instrument. Fails if another source already holds it.</summary>
        bool TryBeginControl(IInstrumentControlSource source);

        /// <summary>Gives the instrument up. Ignored if <paramref name="source"/> is not the holder.</summary>
        void EndControl(IInstrumentControlSource source);

        /// <summary>0 = jaws fully open, 1 = fully closed.</summary>
        void SetJawClosure(IInstrumentControlSource source, float closure01);

        /// <summary>
        /// Kinematically attaches the instrument to a transform (NPC hand, needle-holder jaws),
        /// keeping the relative pose it has at the moment of attachment.
        /// Human XR control does not use this: the Meta Interaction SDK moves the instrument.
        /// </summary>
        bool AttachToDriver(IInstrumentControlSource source, Transform attachPoint);

        void DetachFromDriver(IInstrumentControlSource source);
    }
}
