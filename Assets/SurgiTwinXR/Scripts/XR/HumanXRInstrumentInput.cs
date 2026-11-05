using System.Collections.Generic;
using Oculus.Interaction;
using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurgiTwinXR.XR
{
    /// <summary>
    /// The human trainee side of instrument control (Meta Quest controllers via the
    /// Meta Interaction SDK). Grab is handled by the SDK's <see cref="Grabbable"/>
    /// (grip button with the comprehensive rig's controller grab interactor), which moves the
    /// instrument. This component turns those pointer events into calls on the instrument's
    /// <see cref="IInstrumentControl"/>, and maps the index trigger of the holding controller
    /// to jaw closure.
    ///
    /// While another source (the NPC) controls the instrument, the SDK interactables are
    /// disabled so the trainee cannot grab it out of the NPC's hand.
    /// </summary>
    [RequireComponent(typeof(InstrumentController))]
    public sealed class HumanXRInstrumentInput : MonoBehaviour, IInstrumentControlSource
    {
        const string IdUnknown = "HUMAN_XR";
        const string IdLeft = "HUMAN_XR_LEFT";
        const string IdRight = "HUMAN_XR_RIGHT";

        [SerializeField] Grabbable grabbable;
        [SerializeField] InstrumentController instrument;
        [Tooltip("Meta SDK interactables (GrabInteractable, HandGrabInteractable) toggled when another source holds the instrument.")]
        [SerializeField] Behaviour[] interactables = System.Array.Empty<Behaviour>();
        [SerializeField] bool indexTriggerClosesJaws = true;
        [Tooltip("Editor / desktop testing without controllers: hold this key to close the jaws.")]
        [SerializeField] Key keyboardJawKey = Key.Space;
        [SerializeField] bool haptics = true;
        [SerializeField, Range(0f, 1f)] float hapticAmplitude = 0.3f;
        [SerializeField] float hapticDuration = 0.06f;

        readonly List<int> selectingPointers = new List<int>(2);
        HandSide hand = HandSide.Unknown;
        string sourceId = IdUnknown;
        OVRCameraRig cameraRig;
        bool lastContact;
        float hapticStopTime = -1f;

        public ControlSourceType SourceType => ControlSourceType.HumanXR;
        public string SourceId => sourceId;
        public HandSide Hand => hand;

        void Awake()
        {
            if (instrument == null) instrument = GetComponent<InstrumentController>();
            if (grabbable == null) grabbable = GetComponent<Grabbable>();
        }

        void OnEnable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised += HandlePointerEvent;
            instrument.ControlSourceChanged += HandleControlSourceChanged;
        }

        void OnDisable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised -= HandlePointerEvent;
            instrument.ControlSourceChanged -= HandleControlSourceChanged;
            selectingPointers.Clear();
            instrument.EndControl(this);
            StopHaptics();
        }

        void HandlePointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    if (!selectingPointers.Contains(evt.Identifier))
                    {
                        selectingPointers.Add(evt.Identifier);
                    }
                    if (selectingPointers.Count == 1)
                    {
                        hand = ResolveHand(evt.Pose.position);
                        sourceId = hand == HandSide.Left ? IdLeft : hand == HandSide.Right ? IdRight : IdUnknown;
                        if (!instrument.TryBeginControl(this))
                        {
                            Debug.LogWarning($"[SurgiTwin] {instrument.Identity.InstrumentId} is controlled by {instrument.CurrentSourceId}; human grab ignored.");
                        }
                    }
                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (selectingPointers.Remove(evt.Identifier) && selectingPointers.Count == 0)
                    {
                        instrument.EndControl(this);
                        StopHaptics();
                    }
                    break;
            }
        }

        void HandleControlSourceChanged(InstrumentController c, IInstrumentControlSource previous, IInstrumentControlSource next)
        {
            bool otherHolds = next != null && !ReferenceEquals(next, this);
            for (int i = 0; i < interactables.Length; i++)
            {
                if (interactables[i] != null)
                {
                    interactables[i].enabled = !otherHolds;
                }
            }
        }

        HandSide ResolveHand(Vector3 grabPosition)
        {
            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<OVRCameraRig>();
            }
            if (cameraRig == null || cameraRig.leftHandAnchor == null || cameraRig.rightHandAnchor == null)
            {
                return HandSide.Unknown;
            }
            float l = (cameraRig.leftHandAnchor.position - grabPosition).sqrMagnitude;
            float r = (cameraRig.rightHandAnchor.position - grabPosition).sqrMagnitude;
            return l < r ? HandSide.Left : HandSide.Right;
        }

        OVRInput.Controller ControllerMask =>
            hand == HandSide.Left ? OVRInput.Controller.LTouch :
            hand == HandSide.Right ? OVRInput.Controller.RTouch :
            OVRInput.Controller.Touch;

        void Update()
        {
            if (!ReferenceEquals(instrument.CurrentSource, this))
            {
                return;
            }

            float jaw = 0f;
            if (indexTriggerClosesJaws)
            {
                jaw = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ControllerMask);
            }
            Keyboard kb = Keyboard.current;
            if (kb != null && keyboardJawKey != Key.None && kb[keyboardJawKey].isPressed)
            {
                jaw = 1f;
            }
            instrument.SetJawClosure(this, jaw);

            UpdateHaptics();
        }

        void UpdateHaptics()
        {
            bool contact = instrument.IsInTipContact;
            if (haptics && contact && !lastContact && hand != HandSide.Unknown)
            {
                OVRInput.SetControllerVibration(1f, hapticAmplitude, ControllerMask);
                hapticStopTime = Time.time + hapticDuration;
            }
            lastContact = contact;
            if (hapticStopTime > 0f && Time.time >= hapticStopTime)
            {
                StopHaptics();
            }
        }

        void StopHaptics()
        {
            if (hapticStopTime > 0f && hand != HandSide.Unknown)
            {
                OVRInput.SetControllerVibration(0f, 0f, ControllerMask);
            }
            hapticStopTime = -1f;
        }
    }
}
