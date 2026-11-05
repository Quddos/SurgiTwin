using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    [CreateAssetMenu(menuName = "SurgiTwinXR/Needle Passing Task Config", fileName = "NeedlePassingTaskConfig")]
    public sealed class NeedlePassingTaskConfig : ScriptableObject
    {
        public string taskId = "NEEDLE_PASSING_01";
        public string displayName = "Needle passing";
        [TextArea(2, 5)]
        public string instructions =
            "Pick up the needle holder (grip). Close the jaws (index trigger) on the needle, " +
            "then pass the needle tip through each highlighted ring in order.";
        [Tooltip("Rings must be passed in order.")]
        public bool requireOrder = true;
        [Tooltip("Start the task automatically when the needle is first grasped.")]
        public bool autoStartOnFirstGrasp = true;
        [Tooltip("0 = no time limit. When exceeded, the task is ABORTED.")]
        public float timeLimitSeconds = 0f;
    }
}
