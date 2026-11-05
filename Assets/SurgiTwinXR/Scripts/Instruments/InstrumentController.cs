using System;
using System.Collections.Generic;
using SurgiTwinXR.Core;
using UnityEngine;

namespace SurgiTwinXR.Instruments
{
    /// <summary>
    /// The one controller every instrument has. Human XR input, the NPC and other
    /// instruments all drive it through <see cref="IInstrumentControl"/>.
    ///
    /// State rules:
    ///  - not held: IDLE (RELEASED for a short moment after being let go)
    ///  - held: HELD, MOVING when the tip moves faster than the threshold,
    ///    INTERACTING while the tip is in contact with tissue.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(InstrumentIdentity))]
    public sealed class InstrumentController : MonoBehaviour, IInstrumentControl
    {
        static readonly List<InstrumentController> s_Active = new List<InstrumentController>(8);

        /// <summary>All enabled instruments. Do not modify.</summary>
        public static List<InstrumentController> Active => s_Active;

        [SerializeField] Rigidbody body;
        [Tooltip("Linear speed (m/s) above which a held instrument is MOVING.")]
        [SerializeField] float movingSpeedThreshold = 0.03f;
        [Tooltip("Angular speed (deg/s) above which a held instrument is MOVING.")]
        [SerializeField] float movingAngularThreshold = 35f;
        [Tooltip("How long the RELEASED state is shown before returning to IDLE.")]
        [SerializeField] float releasedStateDuration = 0.5f;
        [SerializeField] float speedSmoothing = 12f;

        InstrumentIdentity identity;
        InstrumentState state = InstrumentState.Idle;
        IInstrumentControlSource source;

        Transform followTarget;
        Vector3 followLocalPosition;
        Quaternion followLocalRotation;
        bool following;
        bool kinematicBeforeFollow;

        float jaw;
        int tipContacts;
        int tissueInteractions;
        float releasedAt;

        Vector3 lastPosition;
        Quaternion lastRotation;
        float smoothedSpeed;
        float smoothedAngularSpeed;

        Vector3 initialPosition;
        Quaternion initialRotation;
        bool initialKinematic;

        public event Action<InstrumentController, InstrumentState, InstrumentState> StateChanged;
        public event Action<InstrumentController, IInstrumentControlSource, IInstrumentControlSource> ControlSourceChanged;

        public InstrumentIdentity Identity => identity != null ? identity : (identity = GetComponent<InstrumentIdentity>());
        public InstrumentState State => state;
        public IInstrumentControlSource CurrentSource => source;
        public ControlSourceType CurrentSourceType => source != null ? source.SourceType : ControlSourceType.None;
        public string CurrentSourceId => source != null ? source.SourceId : null;
        public bool IsHeld => source != null;
        public float JawClosure => jaw;
        public bool IsInTipContact => tipContacts > 0;
        public bool IsInteractingWithTissue => tissueInteractions > 0;
        public float LinearSpeed => smoothedSpeed;
        public float AngularSpeed => smoothedAngularSpeed;
        public Rigidbody Body => body;
        public Vector3 TipPosition => Identity.Tip.position;

        /// <summary>When true the rigidbody stays kinematic after being detached (e.g. needle lodged in tissue).</summary>
        public bool HoldPositionWhenDetached { get; set; }

        void Awake()
        {
            identity = GetComponent<InstrumentIdentity>();
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialKinematic = body != null && body.isKinematic;
        }

        void OnEnable()
        {
            if (!s_Active.Contains(this))
            {
                s_Active.Add(this);
            }
        }

        void OnDisable()
        {
            s_Active.Remove(this);
        }

        // ------------------------------------------------------------------ control API

        public bool TryBeginControl(IInstrumentControlSource newSource)
        {
            if (newSource == null)
            {
                return false;
            }
            if (ReferenceEquals(source, newSource))
            {
                return true;
            }
            if (source != null)
            {
                return false;
            }

            source = newSource;
            ControlSourceChanged?.Invoke(this, null, newSource);
            SetState(InstrumentState.Held);
            return true;
        }

        public void EndControl(IInstrumentControlSource releasingSource)
        {
            if (source == null || !ReferenceEquals(source, releasingSource))
            {
                return;
            }

            DetachInternal();
            IInstrumentControlSource previous = source;
            source = null;
            releasedAt = Time.time;
            SetState(InstrumentState.Released);
            ControlSourceChanged?.Invoke(this, previous, null);
        }

        public void SetJawClosure(IInstrumentControlSource caller, float closure01)
        {
            if (!ReferenceEquals(caller, source))
            {
                return;
            }
            jaw = Mathf.Clamp01(closure01);
        }

        public bool AttachToDriver(IInstrumentControlSource caller, Transform attachPoint)
        {
            if (attachPoint == null || !ReferenceEquals(caller, source))
            {
                return false;
            }

            followTarget = attachPoint;
            followLocalPosition = attachPoint.InverseTransformPoint(transform.position);
            followLocalRotation = Quaternion.Inverse(attachPoint.rotation) * transform.rotation;
            if (body != null && !following)
            {
                kinematicBeforeFollow = body.isKinematic;
                body.isKinematic = true;
            }
            following = true;
            return true;
        }

        public void DetachFromDriver(IInstrumentControlSource caller)
        {
            if (!ReferenceEquals(caller, source))
            {
                return;
            }
            DetachInternal();
        }

        void DetachInternal()
        {
            if (!following)
            {
                return;
            }
            following = false;
            followTarget = null;
            if (body != null)
            {
                body.isKinematic = kinematicBeforeFollow || HoldPositionWhenDetached;
            }
        }

        // ------------------------------------------------------------------ contact reporting

        /// <summary>Called by tissue / target zones when this instrument's tip enters or leaves contact.</summary>
        public void ReportTipContact(bool entered)
        {
            tipContacts = Mathf.Max(0, tipContacts + (entered ? 1 : -1));
        }

        /// <summary>Called by the tissue zone when the tip starts or stops a tissue interaction.</summary>
        public void ReportTissueInteraction(bool active)
        {
            tissueInteractions = Mathf.Max(0, tissueInteractions + (active ? 1 : -1));
        }

        /// <summary>Returns the instrument to its start pose. Ignored while it is held.</summary>
        public bool ResetToInitialPose()
        {
            if (source != null)
            {
                return false;
            }

            DetachInternal();
            HoldPositionWhenDetached = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            if (body != null)
            {
                body.isKinematic = initialKinematic;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            jaw = 0f;
            lastPosition = initialPosition;
            lastRotation = initialRotation;
            smoothedSpeed = 0f;
            smoothedAngularSpeed = 0f;
            SetState(InstrumentState.Idle);
            return true;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 p = transform.position;
            Quaternion r = transform.rotation;
            float speed = (p - lastPosition).magnitude / dt;
            float angular = Quaternion.Angle(lastRotation, r) / dt;
            float k = 1f - Mathf.Exp(-speedSmoothing * dt);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, k);
            smoothedAngularSpeed = Mathf.Lerp(smoothedAngularSpeed, angular, k);
            lastPosition = p;
            lastRotation = r;

            InstrumentState next;
            if (source != null)
            {
                if (tipContacts > 0 || tissueInteractions > 0)
                {
                    next = InstrumentState.Interacting;
                }
                else if (smoothedSpeed > movingSpeedThreshold || smoothedAngularSpeed > movingAngularThreshold)
                {
                    next = InstrumentState.Moving;
                }
                else
                {
                    next = InstrumentState.Held;
                }
            }
            else if (state == InstrumentState.Released && Time.time - releasedAt < releasedStateDuration)
            {
                next = InstrumentState.Released;
            }
            else
            {
                next = InstrumentState.Idle;
            }

            SetState(next);
        }

        void LateUpdate()
        {
            if (following && followTarget != null)
            {
                transform.SetPositionAndRotation(
                    followTarget.TransformPoint(followLocalPosition),
                    followTarget.rotation * followLocalRotation);
            }
        }

        void SetState(InstrumentState next)
        {
            if (next == state)
            {
                return;
            }
            InstrumentState previous = state;
            state = next;
            StateChanged?.Invoke(this, previous, next);
        }
    }
}
