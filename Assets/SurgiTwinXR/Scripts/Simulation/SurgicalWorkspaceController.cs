using System;
using System.Collections.Generic;
using SurgiTwinXR.Instruments;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Defines the surgical workspace (an oriented box around the operative field) and
    /// reports when instrument tips enter or leave it. Pure maths, no physics queries.
    /// </summary>
    public sealed class SurgicalWorkspaceController : MonoBehaviour
    {
        [SerializeField] Vector3 localCenter = Vector3.zero;
        [SerializeField] Vector3 size = new Vector3(0.5f, 0.4f, 0.45f);

        readonly Dictionary<InstrumentController, bool> inside = new Dictionary<InstrumentController, bool>(8);

        public event Action<InstrumentController, bool> InstrumentWorkspaceChanged;

        public Vector3 Size => size;

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 l = transform.InverseTransformPoint(worldPoint) - localCenter;
            Vector3 h = size * 0.5f;
            return Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.y) <= h.y && Mathf.Abs(l.z) <= h.z;
        }

        public bool IsInside(InstrumentController instrument)
        {
            return instrument != null && inside.TryGetValue(instrument, out bool v) && v;
        }

        void Update()
        {
            var all = InstrumentController.Active;
            for (int i = 0; i < all.Count; i++)
            {
                InstrumentController ic = all[i];
                bool now = Contains(ic.TipPosition);
                inside.TryGetValue(ic, out bool before);
                if (now != before)
                {
                    inside[ic] = now;
                    InstrumentWorkspaceChanged?.Invoke(ic, now);
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.8f, 0.7f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localCenter, size);
        }
    }
}
