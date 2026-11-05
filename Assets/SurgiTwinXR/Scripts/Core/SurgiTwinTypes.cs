namespace SurgiTwinXR.Core
{
    /// <summary>Patient state machine required by Phase 1.</summary>
    public enum PatientState : byte
    {
        Normal = 0,
        TargetSelected = 1,
        InstrumentContact = 2,
        TissueInteraction = 3,
        TaskCompleted = 4
    }

    /// <summary>Instrument state machine required by Phase 1.</summary>
    public enum InstrumentState : byte
    {
        Idle = 0,
        Held = 1,
        Moving = 2,
        Interacting = 3,
        Released = 4
    }

    public enum InstrumentType : byte
    {
        NeedleHolder = 0,
        Grasper = 1,
        Needle = 2,
        Scissors = 3,
        SutureThread = 4
    }

    public enum TaskState : byte
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2,
        Aborted = 3
    }

    /// <summary>Who is currently driving an instrument.</summary>
    public enum ControlSourceType : byte
    {
        None = 0,
        HumanXR = 1,
        NPC = 2,
        Scripted = 3
    }

    /// <summary>The three independently addressable observation viewpoints.</summary>
    public enum CameraRole : byte
    {
        TraineePOV = 0,
        NPCPOV = 1,
        Overhead = 2
    }

    public enum HandSide : byte
    {
        Unknown = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// Cached UPPER_SNAKE_CASE tokens used in logs and JSON exports.
    /// Lookups are array indexed, so they never allocate.
    /// </summary>
    public static class SurgiTwinTokens
    {
        public const int CameraRoleCount = 3;

        static readonly string[] s_Patient =
            { "NORMAL", "TARGET_SELECTED", "INSTRUMENT_CONTACT", "TISSUE_INTERACTION", "TASK_COMPLETED" };
        static readonly string[] s_Instrument =
            { "IDLE", "HELD", "MOVING", "INTERACTING", "RELEASED" };
        static readonly string[] s_InstrumentType =
            { "NEEDLE_HOLDER", "GRASPER", "NEEDLE", "SCISSORS", "SUTURE_THREAD" };
        static readonly string[] s_Task =
            { "NOT_STARTED", "IN_PROGRESS", "COMPLETED", "ABORTED" };
        static readonly string[] s_Source =
            { "NONE", "HUMAN_XR", "NPC", "SCRIPTED" };
        static readonly string[] s_Camera =
            { "TRAINEE_POV", "NPC_POV", "OVERHEAD" };

        public static string Token(PatientState s) => s_Patient[(int)s];
        public static string Token(InstrumentState s) => s_Instrument[(int)s];
        public static string Token(InstrumentType s) => s_InstrumentType[(int)s];
        public static string Token(TaskState s) => s_Task[(int)s];
        public static string Token(ControlSourceType s) => s_Source[(int)s];
        public static string Token(CameraRole s) => s_Camera[(int)s];
    }
}
