using System.Text;
using SurgiTwinXR.Core;
using SurgiTwinXR.Patient;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Writes task / patient status to a world-space TextMesh (theatre monitor).
    /// Refreshes at a low rate and reuses one StringBuilder.
    /// </summary>
    public sealed class TaskProgressDisplay : MonoBehaviour
    {
        [SerializeField] TextMesh text;
        [SerializeField] SurgicalTaskController task;
        [SerializeField] PatientStateController patient;
        [SerializeField] float refreshInterval = 0.25f;

        readonly StringBuilder sb = new StringBuilder(256);
        float nextRefresh;

        void Update()
        {
            if (text == null || Time.unscaledTime < nextRefresh)
            {
                return;
            }
            nextRefresh = Time.unscaledTime + refreshInterval;

            sb.Length = 0;
            sb.Append("SURGITWIN-XR  |  PHASE 1\n");
            if (task != null)
            {
                sb.Append(task.DisplayName.ToUpperInvariant()).Append('\n');
                sb.Append("Task: ").Append(SurgiTwinTokens.Token(task.State)).Append('\n');
                sb.Append("Rings: ").Append(task.GatesPassed).Append(" / ").Append(task.GateCount);
                sb.Append("  (").Append(Mathf.RoundToInt(task.Progress * 100f)).Append("%)\n");
                int seconds = Mathf.FloorToInt(task.ElapsedSeconds);
                sb.Append("Time: ").Append(seconds / 60).Append(':');
                int s = seconds % 60;
                if (s < 10) sb.Append('0');
                sb.Append(s).Append('\n');
            }
            if (patient != null)
            {
                sb.Append("Patient: ").Append(SurgiTwinTokens.Token(patient.State));
            }
            text.text = sb.ToString();
        }
    }
}
