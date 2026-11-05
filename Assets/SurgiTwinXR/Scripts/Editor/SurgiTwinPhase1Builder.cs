// SurgiTwin-XR Phase 1 scene builder.
using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using SurgiTwinXR.Anatomy;
using SurgiTwinXR.Cameras;
using SurgiTwinXR.Core;
using SurgiTwinXR.Instruments;
using SurgiTwinXR.NPC;
using SurgiTwinXR.Patient;
using SurgiTwinXR.Simulation;
using SurgiTwinXR.Utilities;
using SurgiTwinXR.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static SurgiTwinXR.EditorTools.SurgiTwinBuilderUtil;

namespace SurgiTwinXR.EditorTools
{
    /// <summary>
    /// Generates the Phase 1 operating theatre: materials, meshes, ScriptableObjects, prefabs
    /// (patient, instruments, NPC, equipment) and the scene with the Meta XR rig.
    /// Everything it writes lives under Assets/SurgiTwinXR and is safe to regenerate.
    /// Menu: SurgiTwin > Phase 1 > Build Operating Theatre.
    /// </summary>
    public static class SurgiTwinPhase1Builder
    {
        public const string Root = "Assets/SurgiTwinXR";
        public const string ScenePath = Root + "/Scenes/SurgiTwin_OR_Phase1.unity";
        const string MatDir = Root + "/Art/Materials";
        const string MeshDir = Root + "/Art/Meshes";
        const string PrefabDir = Root + "/Prefabs";
        const string SODir = Root + "/ScriptableObjects";

        // Meta XR prefab GUIDs, verified against
        // com.meta.xr.sdk.interaction.ovr/Editor/QuickActions/Scripts/OVRComprehensiveInteractionRigWizard.cs (v207)
        const string OVRCameraRigGuid = "126d619cf4daa52469682f85c1378b4a";
        const string OVRComprehensiveInteractionRigGuid = "0a7d2469f24041c4284c66706f84c45e";

        // Layout (world space, metres). Table long axis = Z, patient head towards +Z.
        const float TablePadTop = 0.90f;
        static readonly Vector3 TraineeRigPosition = new Vector3(0.80f, 0f, -0.10f);
        const float TraineeYaw = -90f; // faces -X, towards the patient
        static readonly Vector3 NpcPosition = new Vector3(-0.72f, 0f, -0.10f);
        const float NpcYaw = 90f;

        // Needle geometry (scaled ~1.2x a 26 mm half-circle needle for visibility in VR).
        const float NeedleArcRadius = 0.016f;
        const float NeedleArcDegrees = 180f;
        const float NeedleWireRadius = 0.0009f;
        const float ShaftLength = 0.30f;

        static class M
        {
            public static Material floor, wall, wallRail, ceiling, ceilingPanel, door, steel, darkSteel, machine,
                tablePad, drape, skin, pad, gold, black, blueAccent, redAccent, feedScreen, statusScreen, frame,
                lightHousing, lightEmissive, scrubs, npcSkin, glove, cap, mask, ring, needle, suture, rubber, ivBag;
        }

        // ------------------------------------------------------------------ entry points

        [MenuItem("SurgiTwin/Phase 1/Build Operating Theatre", priority = 1)]
        static void BuildMenu()
        {
            string report = BuildAll(true);
            EditorUtility.DisplayDialog("SurgiTwin Phase 1", Errors == 0
                ? "Operating theatre built.\n\n" + ScenePath
                : $"Built with {Errors} error(s). See the Console.", "OK");
            Debug.Log(report);
        }

        [MenuItem("SurgiTwin/Phase 1/Apply Quest Render Settings (HDR off, MSAA 4x)", priority = 20)]
        static void QuestRenderMenu()
        {
            Report.Clear();
            ApplyQuestRenderSettings();
            Debug.Log(Report.ToString());
        }

        /// <summary>Builds everything. Returns a text report. <see cref="SurgiTwinBuilderUtil.Errors"/> holds the error count.</summary>
        public static string BuildAll(bool askToSaveOpenScenes)
        {
            Report.Clear();
            Errors = 0;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Error("Exit Play Mode before building.");
                return Report.ToString();
            }
            if (askToSaveOpenScenes)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Error("Cancelled: open scenes have unsaved changes.");
                    return Report.ToString();
                }
            }
            else
            {
                // Non-interactive (automation): never discard someone's unsaved work.
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var open = EditorSceneManager.GetSceneAt(i);
                    if (open.isDirty && open.path != ScenePath)
                    {
                        Error($"Scene '{open.path}' has unsaved changes. Save it, then build again.");
                        return Report.ToString();
                    }
                }
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                CreateFolders();
                CreateMaterials();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();

            Mesh ringMesh = SaveMesh(MeshDir, ProceduralMeshes.Torus("SM_NeedleRing", 0.019f, 0.0022f, 40, 10));
            Mesh needleMesh = SaveMesh(MeshDir, ProceduralMeshes.CurvedNeedle("SM_CurvedNeedle", NeedleArcRadius, NeedleArcDegrees, NeedleWireRadius));
            var defs = CreateDefinitions(out NeedlePassingTaskConfig taskConfig);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene unloads unreferenced assets: reload everything created above by path.
            taskConfig = AssetDatabase.LoadAssetAtPath<NeedlePassingTaskConfig>(SODir + "/NeedlePassingTaskConfig.asset");
            defs[InstrumentType.NeedleHolder] = AssetDatabase.LoadAssetAtPath<InstrumentDefinition>(SODir + "/ID_NeedleHolder.asset");
            defs[InstrumentType.Grasper] = AssetDatabase.LoadAssetAtPath<InstrumentDefinition>(SODir + "/ID_Grasper.asset");
            defs[InstrumentType.Scissors] = AssetDatabase.LoadAssetAtPath<InstrumentDefinition>(SODir + "/ID_Scissors.asset");
            defs[InstrumentType.Needle] = AssetDatabase.LoadAssetAtPath<InstrumentDefinition>(SODir + "/ID_Needle.asset");
            ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/SM_NeedleRing.asset");
            needleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/SM_CurvedNeedle.asset");
            CreateMaterials();

            // ---------------- environment
            var env = new GameObject("Environment").transform;
            BuildLighting(env);
            var room = BuildRoom(env);
            var table = SavePrefab(BuildOperatingTable(env), "Environment/PH_OperatingTable");
            var lights = SavePrefab(BuildSurgicalLights(env), "Environment/PH_SurgicalLights");
            var trolley = SavePrefab(BuildTrolley(env, "InstrumentTrolley", new Vector3(0.78f, 0f, -0.66f), 0.62f, 0.42f), "Environment/PH_InstrumentTrolley");
            var npcTray = BuildTrolley(env, "NPCInstrumentTray", new Vector3(-0.80f, 0f, -0.66f), 0.46f, 0.36f);
            var monitor = BuildMonitorTower(env, out Renderer feedScreen, out TextMesh feedLabel, out TextMesh statusText);
            SavePrefab(monitor, "Environment/PH_MonitorTower");
            SavePrefab(BuildAnesthesiaMachine(env), "Environment/PH_AnesthesiaMachine");
            SavePrefab(BuildIVStand(env), "Environment/PH_IVStand");
            BuildBackTable(env);
            foreach (Transform t in env)
            {
                // Monitor text and feed change at runtime, so the monitor tower is not static-batched.
                if (t.GetComponentInChildren<TextMesh>(true) == null) SetStaticRecursive(t.gameObject);
            }

            // ---------------- patient
            var patientGo = BuildPatient(ringMesh, out var patientRefs);
            SavePrefab(patientGo, "Patient/VirtualPatient");

            // ---------------- instruments
            var instrumentsRoot = new GameObject("Instruments").transform;
            float trayTop = 0.954f;
            var nh = BuildLapInstrument(instrumentsRoot, "Instrument_NeedleHolder", "NEEDLE_HOLDER_01", InstrumentType.NeedleHolder,
                defs[InstrumentType.NeedleHolder], M.gold, 0.018f, false, true, 0f,
                new Vector3(0.88f, trayTop + 0.05f, -0.78f), TraineeYaw);
            var gr = BuildLapInstrument(instrumentsRoot, "Instrument_Grasper", "GRASPER_01", InstrumentType.Grasper,
                defs[InstrumentType.Grasper], M.blueAccent, 0.022f, true, true, 0f,
                new Vector3(0.88f, trayTop + 0.05f, -0.66f), TraineeYaw);
            var sc = BuildLapInstrument(instrumentsRoot, "Instrument_Scissors", "SCISSORS_01", InstrumentType.Scissors,
                defs[InstrumentType.Scissors], M.redAccent, 0.016f, true, false, 10f,
                new Vector3(0.88f, trayTop + 0.05f, -0.54f), TraineeYaw);
            SavePrefab(nh, "Instruments/Instrument_NeedleHolder");
            SavePrefab(gr, "Instruments/Instrument_Grasper");
            SavePrefab(sc, "Instruments/Instrument_Scissors");

            // Second grasper for the NPC (same prefab, different ID) to demonstrate the shared controller.
            var grPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Instruments/Instrument_Grasper.prefab");
            var gr2 = (GameObject)PrefabUtility.InstantiatePrefab(grPrefab, instrumentsRoot);
            gr2.name = "Instrument_Grasper_NPC";
            gr2.transform.SetPositionAndRotation(new Vector3(-0.90f, trayTop + 0.05f, -0.66f), Quaternion.Euler(0f, 90f, 0f));
            SetString(gr2.GetComponent<InstrumentIdentity>(), "instrumentId", "GRASPER_02");

            Vector3 padTop = patientRefs.padTopWorld;
            var needle = BuildNeedle(instrumentsRoot, needleMesh, defs[InstrumentType.Needle],
                padTop + new Vector3(0.06f, NeedleWireRadius, 0f), Quaternion.Euler(0f, 200f, 0f));
            SavePrefab(needle, "Instruments/Instrument_Needle_WithSuture");

            // ---------------- NPC
            var npcGo = BuildNPC(out var npcRefs);
            SavePrefab(npcGo, "NPC/NPC_Instructor");

            // ---------------- XR rig
            var rig = BuildXRRig(out Camera headsetCamera, out Transform centerEye);

            // ---------------- cameras
            var camerasRoot = new GameObject("ObservationCameras").transform;
            var overhead = BuildOverheadCamera(camerasRoot, padTop);
            var traineeCam = BuildTraineeCaptureCamera(centerEye);

            // ---------------- systems
            var systems = new GameObject("SurgiTwinSystems").transform;
            var sessionGo = Empty("SessionManager", systems, Vector3.zero);
            var session = sessionGo.AddComponent<SurgicalSessionManager>();
            var recorder = sessionGo.AddComponent<InstrumentTrajectoryRecorder>();
            var shortcuts = sessionGo.AddComponent<SessionInputShortcuts>();
            sessionGo.AddComponent<XRRuntimeStatus>();

            var camMgrGo = Empty("CameraManager", systems, Vector3.zero);
            var camMgr = camMgrGo.AddComponent<SurgicalCameraManager>();
            if (headsetCamera != null) SetRef(camMgr, "headsetCamera", headsetCamera);

            var taskGo = Empty("NeedlePassingTask", systems, Vector3.zero);
            var task = taskGo.AddComponent<SurgicalTaskController>();

            var wsGo = Empty("SurgicalWorkspace", systems, padTop);
            var workspace = wsGo.AddComponent<SurgicalWorkspaceController>();
            SetVector3(workspace, "localCenter", new Vector3(0f, 0.12f, 0f));
            SetVector3(workspace, "size", new Vector3(0.5f, 0.35f, 0.45f));

            // monitor
            var feed = monitor.AddComponent<MonitorFeedDisplay>();
            SetRef(feed, "screen", feedScreen);
            SetEnum(feed, "role", (int)CameraRole.NPCPOV);
            SetRef(feed, "label", feedLabel);
            var status = monitor.AddComponent<TaskProgressDisplay>();
            SetRef(status, "text", statusText);
            SetRef(status, "task", task);
            SetRef(status, "patient", patientRefs.state);

            // wiring
            SetRef(session, "patient", patientRefs.state);
            SetRef(session, "task", task);
            SetRef(session, "recorder", recorder);
            SetRef(session, "workspace", workspace);
            SetRef(session, "cameras", camMgr);
            SetRef(recorder, "patient", patientRefs.state);
            SetRef(recorder, "task", task);
            SetRef(shortcuts, "task", task);
            SetRef(shortcuts, "session", session);
            SetRef(shortcuts, "monitor", feed);

            SetRef(task, "config", taskConfig);
            SetRef(task, "needle", needle.GetComponent<NeedleBody>());
            SetRefs(task, "gates", patientRefs.gates);
            SetRef(task, "patient", patientRefs.state);
            SetRefs(task, "resettableInstruments", new Object[]
            {
                nh.GetComponent<InstrumentController>(), gr.GetComponent<InstrumentController>(),
                sc.GetComponent<InstrumentController>(), gr2.GetComponent<InstrumentController>()
            });

            var npcCtrl = npcGo.GetComponent<NPCManualController>();
            SetRef(npcCtrl, "lookTarget", patientRefs.targetArea);
            SetRef(npcCtrl, "testInstrument", gr2.GetComponent<InstrumentController>());
            SetEnum(npcCtrl, "testHand", (int)NPCHand.Right);

            // ---------------- save
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            ApplyQuestRenderSettings();
            AssetDatabase.SaveAssets();

            Log($"Scene saved: {ScenePath}");
            Log($"Build finished with {Errors} error(s).");
            return Report.ToString();
        }

        // ------------------------------------------------------------------ assets

        static void CreateFolders()
        {
            string[] folders =
            {
                Root + "/Art/Materials", Root + "/Art/Meshes", Root + "/Art/Models", Root + "/Art/Textures",
                Root + "/Prefabs/Environment", Root + "/Prefabs/Patient", Root + "/Prefabs/Instruments", Root + "/Prefabs/NPC",
                Root + "/Scenes", Root + "/ScriptableObjects", Root + "/Settings", Root + "/Documentation"
            };
            foreach (var f in folders) EnsureFolder(f);
        }

        static void CreateMaterials()
        {
            M.floor = Mat(MatDir, "M_Floor", new Color(0.56f, 0.62f, 0.64f), 0f, 0.55f);
            M.wall = Mat(MatDir, "M_Wall", new Color(0.66f, 0.78f, 0.76f), 0f, 0.25f);
            M.wallRail = Mat(MatDir, "M_WallRail", new Color(0.30f, 0.52f, 0.55f), 0f, 0.3f);
            M.ceiling = Mat(MatDir, "M_Ceiling", new Color(0.90f, 0.92f, 0.93f), 0f, 0.1f);
            M.ceilingPanel = Mat(MatDir, "M_CeilingPanel", new Color(0.95f, 0.97f, 1f), 0f, 0.1f, new Color(1.1f, 1.15f, 1.2f));
            M.door = Mat(MatDir, "M_Door", new Color(0.50f, 0.58f, 0.62f), 0.2f, 0.4f);
            M.steel = Mat(MatDir, "M_SurgicalSteel", new Color(0.78f, 0.80f, 0.83f), 0.9f, 0.7f);
            M.darkSteel = Mat(MatDir, "M_DarkSteel", new Color(0.22f, 0.24f, 0.27f), 0.8f, 0.5f);
            M.machine = Mat(MatDir, "M_EquipmentPlastic", new Color(0.84f, 0.86f, 0.88f), 0f, 0.45f);
            M.tablePad = Mat(MatDir, "M_TablePad", new Color(0.10f, 0.11f, 0.13f), 0f, 0.3f);
            M.drape = Mat(MatDir, "M_SurgicalDrape", new Color(0.13f, 0.36f, 0.60f), 0f, 0.12f);
            M.skin = Mat(MatDir, "M_PatientSkin", new Color(0.85f, 0.66f, 0.56f), 0f, 0.35f);
            M.pad = Mat(MatDir, "M_TrainingPadTissue", new Color(0.86f, 0.55f, 0.55f), 0f, 0.55f);
            M.gold = Mat(MatDir, "M_InstrumentGold", new Color(0.86f, 0.66f, 0.26f), 1f, 0.65f);
            M.black = Mat(MatDir, "M_InstrumentBlack", new Color(0.05f, 0.05f, 0.06f), 0.1f, 0.45f);
            M.blueAccent = Mat(MatDir, "M_InstrumentBlue", new Color(0.12f, 0.32f, 0.75f), 0.2f, 0.5f);
            M.redAccent = Mat(MatDir, "M_InstrumentRed", new Color(0.70f, 0.15f, 0.15f), 0.2f, 0.5f);
            M.feedScreen = Mat(MatDir, "M_MonitorFeed", Color.white, unlit: true);
            M.statusScreen = Mat(MatDir, "M_MonitorStatus", new Color(0.03f, 0.08f, 0.12f), unlit: true);
            M.frame = Mat(MatDir, "M_MonitorFrame", new Color(0.12f, 0.12f, 0.14f), 0.3f, 0.5f);
            M.lightHousing = Mat(MatDir, "M_LightHousing", new Color(0.88f, 0.90f, 0.92f), 0.2f, 0.6f);
            M.lightEmissive = Mat(MatDir, "M_LightEmissive", new Color(1f, 0.98f, 0.93f), 0f, 0.9f, new Color(2.6f, 2.5f, 2.3f));
            M.scrubs = Mat(MatDir, "M_NPCScrubs", new Color(0.16f, 0.50f, 0.53f), 0f, 0.2f);
            M.npcSkin = Mat(MatDir, "M_NPCSkin", new Color(0.72f, 0.55f, 0.45f), 0f, 0.35f);
            M.glove = Mat(MatDir, "M_NitrileGlove", new Color(0.52f, 0.68f, 0.92f), 0f, 0.5f);
            M.cap = Mat(MatDir, "M_SurgicalCap", new Color(0.22f, 0.42f, 0.70f), 0f, 0.15f);
            M.mask = Mat(MatDir, "M_SurgicalMask", new Color(0.62f, 0.78f, 0.90f), 0f, 0.15f);
            M.ring = Mat(MatDir, "M_NeedleRing", new Color(0.55f, 0.60f, 0.65f), 0.3f, 0.5f);
            M.needle = Mat(MatDir, "M_Needle", new Color(0.85f, 0.87f, 0.90f), 1f, 0.8f);
            M.suture = Mat(MatDir, "M_SutureViolet", new Color(0.45f, 0.20f, 0.62f), unlit: true);
            M.rubber = Mat(MatDir, "M_Rubber", new Color(0.07f, 0.07f, 0.08f), 0f, 0.2f);
            M.ivBag = Mat(MatDir, "M_IVBag", new Color(0.85f, 0.90f, 0.92f), 0f, 0.7f);
        }

        static Dictionary<InstrumentType, InstrumentDefinition> CreateDefinitions(out NeedlePassingTaskConfig taskConfig)
        {
            var d = new Dictionary<InstrumentType, InstrumentDefinition>
            {
                [InstrumentType.NeedleHolder] = Def("ID_NeedleHolder", InstrumentType.NeedleHolder, "Laparoscopic needle holder",
                    "5 mm needle driver, single-action jaws, ratchet handle (placeholder geometry).", true, true),
                [InstrumentType.Grasper] = Def("ID_Grasper", InstrumentType.Grasper, "Laparoscopic grasper",
                    "5 mm atraumatic grasper, double-action jaws (placeholder geometry).", true, true),
                [InstrumentType.Scissors] = Def("ID_Scissors", InstrumentType.Scissors, "Laparoscopic scissors",
                    "5 mm curved scissors. Visual and grab only in Phase 1: no cutting.", true, false),
                [InstrumentType.Needle] = Def("ID_Needle", InstrumentType.Needle, "Curved suture needle",
                    "Half-circle taper needle with visual suture thread.", false, false)
            };

            string path = SODir + "/NeedlePassingTaskConfig.asset";
            taskConfig = AssetDatabase.LoadAssetAtPath<NeedlePassingTaskConfig>(path);
            if (taskConfig == null)
            {
                taskConfig = ScriptableObject.CreateInstance<NeedlePassingTaskConfig>();
                AssetDatabase.CreateAsset(taskConfig, path);
            }
            return d;
        }

        static InstrumentDefinition Def(string file, InstrumentType type, string name, string description, bool jaws, bool needle)
        {
            string path = $"{SODir}/{file}.asset";
            var def = AssetDatabase.LoadAssetAtPath<InstrumentDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<InstrumentDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.instrumentType = type;
            def.displayName = name;
            def.description = description;
            def.hasJaws = jaws;
            def.canGraspNeedle = needle;
            def.shaftLength = type == InstrumentType.Needle ? 0f : ShaftLength;
            EditorUtility.SetDirty(def);
            return def;
        }

        static GameObject SavePrefab(GameObject go, string relativePath)
        {
            string path = $"{PrefabDir}/{relativePath}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction, out bool success);
            if (!success) Error("Failed to save prefab " + path);
            return go;
        }

        // ------------------------------------------------------------------ environment

        static void BuildLighting(Transform env)
        {
            var sunGo = Empty("DirectionalLight_Fill", env, new Vector3(0f, 3f, 0f), new Vector3(58f, -35f, 0f));
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.55f;
            sun.color = new Color(0.95f, 0.97f, 1f);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.55f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.54f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.31f, 0.32f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.sun = sun;
        }

        static GameObject BuildRoom(Transform env)
        {
            var room = Empty("Room", env, Vector3.zero);
            Mark(room, "Room shell built from primitives.", "OR room shell (walls, floor, ceiling, doors)");
            float w = 7f, d = 7f, h = 3f, cz = -0.3f;
            Prim(PrimitiveType.Cube, "Floor", room.transform, new Vector3(0f, -0.05f, cz), new Vector3(w, 0.1f, d), M.floor, keepCollider: true);
            Prim(PrimitiveType.Cube, "Ceiling", room.transform, new Vector3(0f, h + 0.05f, cz), new Vector3(w, 0.1f, d), M.ceiling, castShadows: false);
            Prim(PrimitiveType.Cube, "Wall_North", room.transform, new Vector3(0f, h / 2f, cz + d / 2f), new Vector3(w, h, 0.1f), M.wall, keepCollider: true);
            Prim(PrimitiveType.Cube, "Wall_South", room.transform, new Vector3(0f, h / 2f, cz - d / 2f), new Vector3(w, h, 0.1f), M.wall, keepCollider: true);
            Prim(PrimitiveType.Cube, "Wall_East", room.transform, new Vector3(w / 2f, h / 2f, cz), new Vector3(0.1f, h, d), M.wall, keepCollider: true);
            Prim(PrimitiveType.Cube, "Wall_West", room.transform, new Vector3(-w / 2f, h / 2f, cz), new Vector3(0.1f, h, d), M.wall, keepCollider: true);
            // protective wall rail
            Prim(PrimitiveType.Cube, "Rail_North", room.transform, new Vector3(0f, 0.95f, cz + d / 2f - 0.06f), new Vector3(w, 0.12f, 0.03f), M.wallRail, castShadows: false);
            Prim(PrimitiveType.Cube, "Rail_South", room.transform, new Vector3(0f, 0.95f, cz - d / 2f + 0.06f), new Vector3(w, 0.12f, 0.03f), M.wallRail, castShadows: false);
            Prim(PrimitiveType.Cube, "Rail_East", room.transform, new Vector3(w / 2f - 0.06f, 0.95f, cz), new Vector3(0.03f, 0.12f, d), M.wallRail, castShadows: false);
            Prim(PrimitiveType.Cube, "Rail_West", room.transform, new Vector3(-w / 2f + 0.06f, 0.95f, cz), new Vector3(0.03f, 0.12f, d), M.wallRail, castShadows: false);
            Prim(PrimitiveType.Cube, "Door_South", room.transform, new Vector3(-1.6f, 1.05f, cz - d / 2f + 0.07f), new Vector3(1.4f, 2.1f, 0.04f), M.door, castShadows: false);
            Prim(PrimitiveType.Cube, "DoorWindow", room.transform, new Vector3(-1.6f, 1.5f, cz - d / 2f + 0.095f), new Vector3(0.4f, 0.3f, 0.01f), M.statusScreen, castShadows: false);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1.3f : 1.3f;
                float z = i < 2 ? -1.6f : 1.0f;
                Prim(PrimitiveType.Cube, "CeilingLightPanel_" + i, room.transform, new Vector3(x, h - 0.01f, z), new Vector3(1.2f, 0.02f, 0.6f), M.ceilingPanel, castShadows: false);
            }
            return room;
        }

        static GameObject BuildOperatingTable(Transform env)
        {
            var t = Empty("OperatingTable", env, Vector3.zero);
            Mark(t, "Operating table from primitives.", "Operating table (Blender)");
            float cz = -0.30f;
            Prim(PrimitiveType.Cube, "BasePlate", t.transform, new Vector3(0f, 0.04f, cz), new Vector3(0.55f, 0.08f, 0.95f), M.darkSteel, keepCollider: true);
            Prim(PrimitiveType.Cube, "Column", t.transform, new Vector3(0f, 0.45f, cz), new Vector3(0.28f, 0.75f, 0.36f), M.machine, keepCollider: true);
            Prim(PrimitiveType.Cube, "TopFrame", t.transform, new Vector3(0f, 0.845f, cz), new Vector3(0.56f, 0.05f, 2.1f), M.steel, keepCollider: true);
            Prim(PrimitiveType.Cube, "Pad", t.transform, new Vector3(0f, TablePadTop - 0.015f, cz), new Vector3(0.54f, 0.03f, 2.06f), M.tablePad, keepCollider: true);
            Prim(PrimitiveType.Cylinder, "SideRail_L", t.transform, new Vector3(-0.29f, 0.84f, cz), new Vector3(0.012f, 1.0f, 0.012f), M.steel, new Vector3(90f, 0f, 0f), castShadows: false);
            Prim(PrimitiveType.Cylinder, "SideRail_R", t.transform, new Vector3(0.29f, 0.84f, cz), new Vector3(0.012f, 1.0f, 0.012f), M.steel, new Vector3(90f, 0f, 0f), castShadows: false);
            return t;
        }

        static GameObject BuildSurgicalLights(Transform env)
        {
            var g = Empty("SurgicalLights", env, Vector3.zero);
            Mark(g, "Ceiling-mounted surgical light from primitives; one realtime spot light, no shadows.", "Surgical light head + arm (Blender)");
            Vector3 target = new Vector3(0f, TablePadTop + 0.21f, -0.05f);
            Prim(PrimitiveType.Cylinder, "CeilingMount", g.transform, new Vector3(0f, 2.94f, -0.1f), new Vector3(0.25f, 0.05f, 0.25f), M.lightHousing, castShadows: false);
            Prim(PrimitiveType.Cylinder, "CentralPost", g.transform, new Vector3(0f, 2.72f, -0.1f), new Vector3(0.05f, 0.2f, 0.05f), M.lightHousing, castShadows: false);
            Prim(PrimitiveType.Cube, "ArmA", g.transform, new Vector3(0.19f, 2.52f, -0.08f), new Vector3(0.42f, 0.05f, 0.05f), M.lightHousing, castShadows: false);
            Prim(PrimitiveType.Cylinder, "DropA", g.transform, new Vector3(0.38f, 2.32f, -0.06f), new Vector3(0.035f, 0.2f, 0.035f), M.lightHousing, castShadows: false);
            Prim(PrimitiveType.Cube, "ArmB", g.transform, new Vector3(-0.22f, 2.6f, 0.08f), new Vector3(0.48f, 0.05f, 0.05f), M.lightHousing, new Vector3(0f, -35f, 0f), castShadows: false);
            Prim(PrimitiveType.Cylinder, "DropB", g.transform, new Vector3(-0.42f, 2.42f, 0.22f), new Vector3(0.035f, 0.18f, 0.035f), M.lightHousing, castShadows: false);

            LightHead(g.transform, "LightHead_Main", new Vector3(0.38f, 2.08f, -0.06f), target, true);
            LightHead(g.transform, "LightHead_Satellite", new Vector3(-0.42f, 2.18f, 0.22f), target, false);
            return g;
        }

        static void LightHead(Transform parent, string name, Vector3 pos, Vector3 target, bool withLight)
        {
            var head = Empty(name, parent, pos);
            head.transform.rotation = Quaternion.FromToRotation(Vector3.down, (target - pos).normalized);
            Prim(PrimitiveType.Cylinder, "Housing", head.transform, Vector3.zero, new Vector3(0.62f, 0.045f, 0.62f), M.lightHousing, castShadows: false);
            Prim(PrimitiveType.Cylinder, "EmissiveFace", head.transform, new Vector3(0f, -0.047f, 0f), new Vector3(0.50f, 0.004f, 0.50f), M.lightEmissive, castShadows: false);
            Prim(PrimitiveType.Cylinder, "SterileHandle", head.transform, new Vector3(0f, -0.09f, 0f), new Vector3(0.04f, 0.04f, 0.04f), M.steel, castShadows: false);
            if (!withLight) return;
            var lgo = Empty("SpotLight", head.transform, new Vector3(0f, -0.06f, 0f), new Vector3(90f, 0f, 0f));
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 3.2f;
            l.spotAngle = 42f;
            l.innerSpotAngle = 24f;
            l.intensity = 1.6f;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.shadows = LightShadows.None;
        }

        static GameObject BuildTrolley(Transform env, string name, Vector3 pos, float width, float depth)
        {
            var t = Empty(name, env, pos);
            Mark(t, "Mayo / instrument trolley from primitives.", "Instrument trolley (Blender)");
            float top = 0.94f;
            Prim(PrimitiveType.Cube, "Tray", t.transform, new Vector3(0f, top, 0f), new Vector3(width, 0.02f, depth), M.steel, keepCollider: true);
            Prim(PrimitiveType.Cube, "SterileTowel", t.transform, new Vector3(0f, top + 0.012f, 0f), new Vector3(width - 0.04f, 0.004f, depth - 0.04f), M.drape, keepCollider: true, castShadows: false);
            Prim(PrimitiveType.Cube, "Rim_N", t.transform, new Vector3(0f, top + 0.02f, depth / 2f), new Vector3(width, 0.03f, 0.01f), M.steel, keepCollider: true, castShadows: false);
            Prim(PrimitiveType.Cube, "Rim_S", t.transform, new Vector3(0f, top + 0.02f, -depth / 2f), new Vector3(width, 0.03f, 0.01f), M.steel, keepCollider: true, castShadows: false);
            Prim(PrimitiveType.Cube, "Rim_E", t.transform, new Vector3(width / 2f, top + 0.02f, 0f), new Vector3(0.01f, 0.03f, depth), M.steel, keepCollider: true, castShadows: false);
            Prim(PrimitiveType.Cube, "Rim_W", t.transform, new Vector3(-width / 2f, top + 0.02f, 0f), new Vector3(0.01f, 0.03f, depth), M.steel, keepCollider: true, castShadows: false);
            Prim(PrimitiveType.Cube, "LowerShelf", t.transform, new Vector3(0f, 0.25f, 0f), new Vector3(width - 0.04f, 0.015f, depth - 0.04f), M.steel, castShadows: false);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (width / 2f - 0.03f);
                float z = (i < 2 ? -1f : 1f) * (depth / 2f - 0.03f);
                Prim(PrimitiveType.Cylinder, "Leg_" + i, t.transform, new Vector3(x, top / 2f, z), new Vector3(0.02f, top / 2f, 0.02f), M.steel, castShadows: false);
                Prim(PrimitiveType.Sphere, "Wheel_" + i, t.transform, new Vector3(x, 0.03f, z), new Vector3(0.06f, 0.06f, 0.06f), M.rubber, castShadows: false);
            }
            return t;
        }

        static GameObject BuildMonitorTower(Transform env, out Renderer feedScreen, out TextMesh feedLabel, out TextMesh statusText)
        {
            Vector3 pos = new Vector3(-1.05f, 0f, 0.85f);
            var t = Empty("MonitorTower", env, pos);
            Vector3 toTrainee = TraineeRigPosition - pos;
            toTrainee.y = 0f;
            // Screens face local -Z (Unity quad front), so local +Z points away from the trainee.
            t.transform.rotation = Quaternion.LookRotation(-toTrainee.normalized, Vector3.up);
            Mark(t, "Theatre monitor tower. Upper screen: observation camera feed. Lower screen: task status.", "Laparoscopy tower / boom monitors (Blender)");

            Prim(PrimitiveType.Cube, "Base", t.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.06f, 0.5f), M.darkSteel, keepCollider: true);
            Prim(PrimitiveType.Cylinder, "Pole", t.transform, new Vector3(0f, 0.95f, 0.05f), new Vector3(0.06f, 0.95f, 0.06f), M.steel);
            Prim(PrimitiveType.Cube, "EquipmentStack", t.transform, new Vector3(0f, 0.55f, 0.05f), new Vector3(0.45f, 0.5f, 0.4f), M.machine, keepCollider: true);

            var feedGroup = Empty("FeedScreen", t.transform, new Vector3(0f, 1.86f, 0f), new Vector3(-6f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Frame", feedGroup.transform, new Vector3(0f, 0f, 0.016f), new Vector3(0.64f, 0.40f, 0.03f), M.frame, castShadows: false);
            var feedQuad = Prim(PrimitiveType.Quad, "Screen", feedGroup.transform, new Vector3(0f, 0f, -0.0005f), new Vector3(0.60f, 0.3375f, 1f), M.feedScreen, castShadows: false);
            feedScreen = feedQuad.GetComponent<Renderer>();
            feedLabel = Text("FeedLabel", feedGroup.transform, new Vector3(-0.30f, -0.18f, -0.002f), "FEED: NPC_POV", 0.0022f, new Color(0.7f, 0.9f, 1f));

            var statusGroup = Empty("StatusScreen", t.transform, new Vector3(0f, 1.42f, 0f), new Vector3(-6f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Frame", statusGroup.transform, new Vector3(0f, 0f, 0.016f), new Vector3(0.64f, 0.40f, 0.03f), M.frame, castShadows: false);
            Prim(PrimitiveType.Quad, "Screen", statusGroup.transform, new Vector3(0f, 0f, -0.0005f), new Vector3(0.60f, 0.3375f, 1f), M.statusScreen, castShadows: false);
            statusText = Text("StatusText", statusGroup.transform, new Vector3(-0.285f, 0.155f, -0.002f), "SURGITWIN-XR | PHASE 1", 0.0032f, new Color(0.85f, 0.97f, 0.95f));
            return t;
        }

        static TextMesh Text(string name, Transform parent, Vector3 localPos, string content, float characterSize, Color color)
        {
            var go = Empty(name, parent, localPos);
            go.AddComponent<MeshRenderer>();
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.UpperLeft;
            tm.alignment = TextAlignment.Left;
            tm.color = color;
            tm.text = content;
            var r = go.GetComponent<MeshRenderer>();
            if (font != null) r.sharedMaterial = font.material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }

        static GameObject BuildAnesthesiaMachine(Transform env)
        {
            var a = Empty("AnesthesiaMachine", env, new Vector3(-0.60f, 0f, 1.32f), new Vector3(0f, 90f, 0f));
            Mark(a, "Anaesthesia workstation from primitives (non-interactive).", "Anaesthesia machine (Blender)");
            Prim(PrimitiveType.Cube, "Body", a.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.62f, 1.24f, 0.55f), M.machine, keepCollider: true);
            Prim(PrimitiveType.Cube, "WorkSurface", a.transform, new Vector3(0f, 0.92f, 0.30f), new Vector3(0.62f, 0.03f, 0.12f), M.machine);
            Prim(PrimitiveType.Cube, "VitalsMonitor", a.transform, new Vector3(0f, 1.48f, 0.05f), new Vector3(0.42f, 0.30f, 0.06f), M.frame);
            Prim(PrimitiveType.Quad, "VitalsScreen", a.transform, new Vector3(0f, 1.48f, 0.081f), new Vector3(0.38f, 0.26f, 1f), M.statusScreen, new Vector3(0f, 180f, 0f), castShadows: false);
            Prim(PrimitiveType.Cylinder, "GasCylinder_O2", a.transform, new Vector3(0.22f, 0.35f, -0.32f), new Vector3(0.1f, 0.33f, 0.1f), M.wallRail);
            Prim(PrimitiveType.Cylinder, "GasCylinder_Air", a.transform, new Vector3(0.06f, 0.35f, -0.32f), new Vector3(0.1f, 0.33f, 0.1f), M.machine);
            Prim(PrimitiveType.Cube, "Vaporizer", a.transform, new Vector3(-0.18f, 1.02f, 0.2f), new Vector3(0.10f, 0.16f, 0.10f), M.gold);
            return a;
        }

        static GameObject BuildIVStand(Transform env)
        {
            var s = Empty("IVStand", env, new Vector3(0.55f, 0f, 1.02f));
            Mark(s, "IV pole with fluid bag (non-interactive).", "IV stand (Blender)");
            Prim(PrimitiveType.Cube, "BaseX", s.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.03f, 0.04f), M.steel);
            Prim(PrimitiveType.Cube, "BaseZ", s.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.04f, 0.03f, 0.5f), M.steel);
            Prim(PrimitiveType.Cylinder, "Pole", s.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.02f, 1.0f, 0.02f), M.steel);
            Prim(PrimitiveType.Cube, "HookBar", s.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.24f, 0.012f, 0.012f), M.steel, castShadows: false);
            Prim(PrimitiveType.Cube, "FluidBag", s.transform, new Vector3(0.09f, 1.80f, 0f), new Vector3(0.10f, 0.16f, 0.03f), M.ivBag, castShadows: false);
            return s;
        }

        static void BuildBackTable(Transform env)
        {
            var b = Empty("BackTable", env, new Vector3(2.1f, 0f, -1.7f));
            Mark(b, "Back table with draped instruments area (non-interactive).", "Back table (Blender)");
            Prim(PrimitiveType.Cube, "Top", b.transform, new Vector3(0f, 0.9f, 0f), new Vector3(1.4f, 0.03f, 0.7f), M.steel, keepCollider: true);
            Prim(PrimitiveType.Cube, "Drape", b.transform, new Vector3(0f, 0.85f, 0f), new Vector3(1.44f, 0.12f, 0.74f), M.drape);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.65f;
                float z = (i < 2 ? -1f : 1f) * 0.30f;
                Prim(PrimitiveType.Cylinder, "Leg_" + i, b.transform, new Vector3(x, 0.4f, z), new Vector3(0.025f, 0.4f, 0.025f), M.steel, castShadows: false);
            }
            Prim(PrimitiveType.Cylinder, "KickBucket", env, new Vector3(1.25f, 0.18f, 0.55f), new Vector3(0.32f, 0.18f, 0.32f), M.steel, keepCollider: true);
        }

        // ------------------------------------------------------------------ patient

        struct PatientRefs
        {
            public PatientStateController state;
            public Transform targetArea;
            public Object[] gates;
            public Vector3 padTopWorld;
        }

        static GameObject BuildPatient(Mesh ringMesh, out PatientRefs refs)
        {
            var root = new GameObject("VirtualPatient");
            root.transform.position = new Vector3(0f, TablePadTop, 0f);
            var patient = root.AddComponent<VirtualPatient>();
            var state = root.AddComponent<PatientStateController>();
            var t = root.transform;

            GameObject Part(string name, AnatomyRegionId id, Vector3 pos)
            {
                var g = Empty(name, t, pos);
                var region = g.AddComponent<AnatomyRegion>();
                SetEnum(region, "region", (int)id);
                return g;
            }

            var head = Part("Head", AnatomyRegionId.Head, new Vector3(0f, 0.11f, 0.68f));
            Prim(PrimitiveType.Sphere, "PH_HeadMesh", head.transform, Vector3.zero, new Vector3(0.17f, 0.20f, 0.22f), M.skin, keepCollider: true);
            Prim(PrimitiveType.Cylinder, "PH_NeckMesh", head.transform, new Vector3(0f, -0.02f, -0.12f), new Vector3(0.10f, 0.05f, 0.10f), M.skin, new Vector3(90f, 0f, 0f));

            var torso = Part("Torso", AnatomyRegionId.Torso, new Vector3(0f, 0f, 0.30f));
            Prim(PrimitiveType.Cube, "PH_ChestMesh", torso.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.36f, 0.22f, 0.40f), M.skin, keepCollider: true);
            Prim(PrimitiveType.Capsule, "PH_ShoulderMesh", torso.transform, new Vector3(0f, 0.10f, 0.16f), new Vector3(0.22f, 0.21f, 0.22f), M.skin, new Vector3(0f, 0f, 90f));

            var leftArm = Part("LeftArm", AnatomyRegionId.LeftArm, new Vector3(-0.235f, 0.06f, 0.20f));
            Prim(PrimitiveType.Capsule, "PH_LeftArmMesh", leftArm.transform, Vector3.zero, new Vector3(0.09f, 0.30f, 0.09f), M.skin, new Vector3(90f, 0f, 0f), keepCollider: true);
            var rightArm = Part("RightArm", AnatomyRegionId.RightArm, new Vector3(0.235f, 0.06f, 0.20f));
            Prim(PrimitiveType.Capsule, "PH_RightArmMesh", rightArm.transform, Vector3.zero, new Vector3(0.09f, 0.30f, 0.09f), M.skin, new Vector3(90f, 0f, 0f), keepCollider: true);

            var abdomen = Part("Abdomen", AnatomyRegionId.Abdomen, new Vector3(0f, 0f, -0.05f));
            Prim(PrimitiveType.Cube, "PH_AbdomenMesh", abdomen.transform, new Vector3(0f, 0.095f, 0f), new Vector3(0.34f, 0.19f, 0.30f), M.skin, keepCollider: true);

            var surface = Empty("AbdominalSurface", abdomen.transform, new Vector3(0f, 0.19f, 0f));
            SetEnum(surface.AddComponent<AnatomyRegion>(), "region", (int)AnatomyRegionId.AbdominalSurface);
            Prim(PrimitiveType.Cube, "PH_SkinSurfaceMesh", surface.transform, new Vector3(0f, 0.005f, 0f), new Vector3(0.32f, 0.01f, 0.28f), M.skin, keepCollider: true);

            // Surgical target: suture training pad on the abdomen with the needle-passing rings.
            var target = Empty("SurgicalTargetArea", surface.transform, new Vector3(0f, 0.01f, 0f));
            SetEnum(target.AddComponent<AnatomyRegion>(), "region", (int)AnatomyRegionId.SurgicalTargetArea);
            var padMesh = Prim(PrimitiveType.Cube, "PH_TrainingPadMesh", target.transform, new Vector3(0f, 0.003f, 0f), new Vector3(0.18f, 0.006f, 0.14f), M.pad, keepCollider: true);
            var approach = target.AddComponent<BoxCollider>();
            approach.isTrigger = true;
            approach.center = new Vector3(0f, 0.07f, 0f);
            approach.size = new Vector3(0.24f, 0.14f, 0.20f);
            var targetArea = target.AddComponent<SurgicalTargetArea>();
            SetRef(targetArea, "patient", state);

            var tissue = Empty("TissueInteractionZone", target.transform, new Vector3(0f, 0.006f, 0f));
            var tissueCol = tissue.AddComponent<BoxCollider>();
            tissueCol.isTrigger = true;
            tissueCol.center = new Vector3(0f, -0.015f, 0f);
            tissueCol.size = new Vector3(0.18f, 0.05f, 0.14f);
            var tissueCtrl = tissue.AddComponent<TissueInteractionController>();
            SetRef(tissueCtrl, "patient", state);
            SetRef(tissueCtrl, "surface", tissue.transform);

            var rings = Empty("NeedlePassingRings", target.transform, new Vector3(0f, 0.006f, 0f));
            var gates = new List<Object>();
            for (int i = 0; i < 4; i++)
            {
                float z = -0.05f + i * 0.033f;
                float x = i % 2 == 0 ? 0.02f : -0.02f;
                float yaw = i % 2 == 0 ? 40f : -40f;
                Prim(PrimitiveType.Cylinder, $"RingPost_{i + 1}", rings.transform, new Vector3(x, 0.0075f, z), new Vector3(0.003f, 0.0075f, 0.003f), M.steel, castShadows: false);
                var gateGo = Empty($"Ring_{i + 1}", rings.transform, new Vector3(x, 0.036f, z), new Vector3(0f, yaw, 0f));
                var ringObj = MeshObject("RingMesh", gateGo.transform, ringMesh, M.ring, Vector3.zero);
                var gate = gateGo.AddComponent<NeedleGate>();
                SetRef(gate, "ringRenderer", ringObj.GetComponent<Renderer>());
                SetFloat(gate, "innerRadius", 0.0165f);
                gates.Add(gate);
            }

            var pelvis = Part("Pelvis", AnatomyRegionId.Pelvis, new Vector3(0f, 0f, -0.31f));
            Prim(PrimitiveType.Cube, "PH_PelvisMesh", pelvis.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.17f, 0.22f), M.skin, keepCollider: true);
            var legs = Part("Legs", AnatomyRegionId.Legs, new Vector3(0f, 0f, -0.86f));
            Prim(PrimitiveType.Capsule, "PH_LeftLegMesh", legs.transform, new Vector3(-0.10f, 0.075f, 0f), new Vector3(0.15f, 0.43f, 0.15f), M.skin, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Capsule, "PH_RightLegMesh", legs.transform, new Vector3(0.10f, 0.075f, 0f), new Vector3(0.15f, 0.43f, 0.15f), M.skin, new Vector3(90f, 0f, 0f));

            // Drapes leave an abdominal window (z -0.20 .. 0.15) exposed.
            var drapes = Empty("Drapes", t, Vector3.zero);
            Mark(drapes, "Flat drape sheets; replace with a cloth-simulated or sculpted drape mesh.", "Surgical drapes (Blender)");
            Prim(PrimitiveType.Cube, "LowerDrape", drapes.transform, new Vector3(0f, 0.205f, -0.78f), new Vector3(0.78f, 0.008f, 1.16f), M.drape, keepCollider: true);
            Prim(PrimitiveType.Cube, "UpperDrape", drapes.transform, new Vector3(0f, 0.235f, 0.32f), new Vector3(0.78f, 0.008f, 0.34f), M.drape, keepCollider: true);
            Prim(PrimitiveType.Cube, "WindowDrape_L", drapes.transform, new Vector3(-0.28f, 0.205f, -0.03f), new Vector3(0.22f, 0.008f, 0.36f), M.drape, keepCollider: true);
            Prim(PrimitiveType.Cube, "WindowDrape_R", drapes.transform, new Vector3(0.28f, 0.205f, -0.03f), new Vector3(0.22f, 0.008f, 0.36f), M.drape, keepCollider: true);
            Prim(PrimitiveType.Cube, "SideDrape_L", drapes.transform, new Vector3(-0.39f, -0.10f, -0.435f), new Vector3(0.008f, 0.62f, 1.85f), M.drape);
            Prim(PrimitiveType.Cube, "SideDrape_R", drapes.transform, new Vector3(0.39f, -0.10f, -0.435f), new Vector3(0.008f, 0.62f, 1.85f), M.drape);
            Prim(PrimitiveType.Cube, "AnesthesiaScreen", drapes.transform, new Vector3(0f, 0.40f, 0.50f), new Vector3(0.84f, 0.36f, 0.006f), M.drape);
            Prim(PrimitiveType.Cylinder, "ScreenBar", drapes.transform, new Vector3(0f, 0.585f, 0.50f), new Vector3(0.015f, 0.42f, 0.015f), M.steel, new Vector3(0f, 0f, 90f), castShadows: false);
            Prim(PrimitiveType.Cylinder, "ScreenPost_L", drapes.transform, new Vector3(-0.42f, 0.29f, 0.50f), new Vector3(0.012f, 0.30f, 0.012f), M.steel, castShadows: false);
            Prim(PrimitiveType.Cylinder, "ScreenPost_R", drapes.transform, new Vector3(0.42f, 0.29f, 0.50f), new Vector3(0.012f, 0.30f, 0.012f), M.steel, castShadows: false);

            SetRef(state, "feedbackRenderer", padMesh.GetComponent<Renderer>());
            SetRef(patient, "stateController", state);
            SetRef(patient, "head", head.transform);
            SetRef(patient, "torso", torso.transform);
            SetRef(patient, "leftArm", leftArm.transform);
            SetRef(patient, "rightArm", rightArm.transform);
            SetRef(patient, "abdomen", abdomen.transform);
            SetRef(patient, "abdominalSurface", surface.transform);
            SetRef(patient, "surgicalTargetArea", target.transform);
            SetRef(patient, "tissueInteractionZone", tissue.transform);

            refs = new PatientRefs
            {
                state = state,
                targetArea = target.transform,
                gates = gates.ToArray(),
                padTopWorld = tissue.transform.position
            };
            return root;
        }

        // ------------------------------------------------------------------ instruments

        static Rigidbody AddBody(GameObject go, float mass, bool kinematic)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = kinematic;
            return rb;
        }

        /// <summary>
        /// Laparoscopic hand instrument. Local +Z points from the handle to the tip.
        /// Hierarchy: Root / Handle / Shaft / Jaws (JawLower, JawUpperPivot, GraspPoint, TipPoint).
        /// </summary>
        static GameObject BuildLapInstrument(Transform parent, string name, string id, InstrumentType type, InstrumentDefinition def,
            Material accent, float jawLength, bool doubleAction, bool canGraspNeedle, float bladeCurve, Vector3 position, float yaw)
        {
            var root = Empty(name, parent, position, new Vector3(0f, yaw, 0f));
            var rb = AddBody(root, 0.25f, false);
            Transform rt = root.transform;

            var handle = Empty("Handle", rt, new Vector3(0f, 0f, -0.06f));
            Prim(PrimitiveType.Cube, "PH_HandleBody", handle.transform, new Vector3(0f, -0.005f, 0f), new Vector3(0.022f, 0.035f, 0.12f), M.black, castShadows: false);
            Prim(PrimitiveType.Cube, "PH_GripPad_L", handle.transform, new Vector3(0.0115f, -0.005f, 0f), new Vector3(0.002f, 0.028f, 0.09f), accent, castShadows: false);
            Prim(PrimitiveType.Cube, "PH_GripPad_R", handle.transform, new Vector3(-0.0115f, -0.005f, 0f), new Vector3(0.002f, 0.028f, 0.09f), accent, castShadows: false);
            Prim(PrimitiveType.Cube, "PH_Lever", handle.transform, new Vector3(0f, -0.036f, -0.015f), new Vector3(0.016f, 0.03f, 0.065f), M.black, new Vector3(14f, 0f, 0f), castShadows: false);
            if (type == InstrumentType.NeedleHolder)
            {
                Prim(PrimitiveType.Cube, "PH_Ratchet", handle.transform, new Vector3(0f, -0.03f, 0.035f), new Vector3(0.006f, 0.022f, 0.025f), M.steel, castShadows: false);
            }
            Prim(PrimitiveType.Cylinder, "PH_RotationKnob", handle.transform, new Vector3(0f, 0f, 0.066f), new Vector3(0.018f, 0.005f, 0.018f), accent, new Vector3(90f, 0f, 0f), castShadows: false);
            var handleCol = handle.AddComponent<BoxCollider>();
            handleCol.center = new Vector3(0f, -0.012f, 0f);
            handleCol.size = new Vector3(0.026f, 0.06f, 0.13f);

            var shaft = Empty("Shaft", rt, new Vector3(0f, 0f, ShaftLength / 2f));
            Prim(PrimitiveType.Cylinder, "PH_ShaftMesh", shaft.transform, Vector3.zero, new Vector3(0.005f, ShaftLength / 2f, 0.005f), M.steel, new Vector3(90f, 0f, 0f), castShadows: false);
            var shaftCol = shaft.AddComponent<CapsuleCollider>();
            shaftCol.direction = 2;
            shaftCol.radius = 0.003f;
            shaftCol.height = ShaftLength;

            var jaws = Empty("Jaws", rt, new Vector3(0f, 0f, ShaftLength), new Vector3(0f, bladeCurve, 0f));
            Vector3 jawScale = new Vector3(0.0042f, 0.0022f, jawLength);
            Transform lowerJaw = null;
            if (doubleAction)
            {
                var lowerPivot = Empty("JawLowerPivot", jaws.transform, new Vector3(0f, -0.0005f, 0f));
                Prim(PrimitiveType.Cube, "PH_JawLowerMesh", lowerPivot.transform, new Vector3(0f, -0.0012f, jawLength / 2f), jawScale, M.steel, castShadows: false);
                lowerJaw = lowerPivot.transform;
            }
            else
            {
                Prim(PrimitiveType.Cube, "PH_JawLowerMesh", jaws.transform, new Vector3(0f, -0.0012f, jawLength / 2f), jawScale, M.steel, castShadows: false);
            }
            var upperPivot = Empty("JawUpperPivot", jaws.transform, new Vector3(0f, 0.0005f, 0f));
            Prim(PrimitiveType.Cube, "PH_JawUpperMesh", upperPivot.transform, new Vector3(0f, 0.0012f, jawLength / 2f), jawScale, M.steel, castShadows: false);

            var grasp = Empty("GraspPoint", jaws.transform, new Vector3(0f, 0f, jawLength * 0.55f));
            var tipPoint = Empty("TipPoint", jaws.transform, new Vector3(0f, 0f, jawLength));
            var tipCol = tipPoint.AddComponent<SphereCollider>();
            tipCol.isTrigger = true;
            tipCol.radius = 0.007f;
            tipCol.center = new Vector3(0f, 0f, -0.006f);

            var identity = root.AddComponent<InstrumentIdentity>();
            SetString(identity, "instrumentId", id);
            SetEnum(identity, "instrumentType", (int)type);
            SetString(identity, "displayName", def.displayName);
            SetRef(identity, "definition", def);
            SetRef(identity, "handle", handle.transform);
            SetRef(identity, "shaft", shaft.transform);
            SetRef(identity, "tip", tipPoint.transform);
            SetRef(identity, "graspPoint", grasp.transform);

            var controller = root.AddComponent<InstrumentController>();
            SetRef(controller, "body", rb);

            var tip = tipPoint.AddComponent<InstrumentTip>();
            SetRef(tip, "owner", controller);

            var jawVisual = root.AddComponent<InstrumentJawVisual>();
            SetRef(jawVisual, "controller", controller);
            SetRef(jawVisual, "upperJaw", upperPivot.transform);
            SetRef(jawVisual, "lowerJaw", lowerJaw);
            SetFloat(jawVisual, "openAngle", doubleAction ? 40f : 26f);

            if (canGraspNeedle)
            {
                var ng = root.AddComponent<NeedleGrasp>();
                SetRef(ng, "instrument", controller);
                SetRef(ng, "graspPoint", grasp.transform);
            }

            AddMetaGrab(root, rb, controller);
            return root;
        }

        /// <summary>
        /// Meta Interaction SDK grab setup (verified against v207 sources):
        /// Grabbable (moves the object) + GrabInteractable (controllers) + HandGrabInteractable (hands / controller-driven hands).
        /// </summary>
        static void AddMetaGrab(GameObject root, Rigidbody rb, InstrumentController controller)
        {
            var grabbable = root.AddComponent<Grabbable>();
            SetRef(grabbable, "_rigidbody", rb);
            SetBool(grabbable, "_kinematicWhileSelected", true);
            SetBool(grabbable, "_throwWhenUnselected", false);

            var grabInteractable = root.AddComponent<GrabInteractable>();
            SetRef(grabInteractable, "_rigidbody", rb);
            SetRef(grabInteractable, "_pointableElement", grabbable);

            var handGrab = root.AddComponent<HandGrabInteractable>();
            SetRef(handGrab, "_rigidbody", rb);
            SetRef(handGrab, "_pointableElement", grabbable);

            var human = root.AddComponent<HumanXRInstrumentInput>();
            SetRef(human, "grabbable", grabbable);
            SetRef(human, "instrument", controller);
            SetRefs(human, "interactables", new Object[] { grabInteractable, handGrab });
        }

        static GameObject BuildNeedle(Transform parent, Mesh needleMesh, InstrumentDefinition def, Vector3 position, Quaternion rotation)
        {
            var root = Empty("Instrument_Needle", parent, Vector3.zero);
            root.transform.SetPositionAndRotation(position, rotation);
            var rb = AddBody(root, 0.01f, true);
            rb.angularDamping = 4f;
            MeshObject("NeedleMesh", root.transform, needleMesh, M.needle, Vector3.zero);

            Vector3 tipPos = ProceduralMeshes.NeedleArcPoint(NeedleArcRadius, NeedleArcDegrees, 1f);
            Vector3 tailPos = ProceduralMeshes.NeedleArcPoint(NeedleArcRadius, NeedleArcDegrees, 0f);
            var tipGo = Empty("TipPoint", root.transform, tipPos);
            var tipCol = tipGo.AddComponent<SphereCollider>();
            tipCol.isTrigger = true;
            tipCol.radius = 0.003f;
            var tail = Empty("TailPoint", root.transform, tailPos);

            float[] ts = { 0.15f, 0.5f, 0.85f };
            foreach (float t in ts)
            {
                var c = root.AddComponent<SphereCollider>();
                c.center = ProceduralMeshes.NeedleArcPoint(NeedleArcRadius, NeedleArcDegrees, t);
                c.radius = 0.0015f;
            }

            var identity = root.AddComponent<InstrumentIdentity>();
            SetString(identity, "instrumentId", "NEEDLE_01");
            SetEnum(identity, "instrumentType", (int)InstrumentType.Needle);
            SetString(identity, "displayName", def.displayName);
            SetRef(identity, "definition", def);
            SetRef(identity, "tip", tipGo.transform);
            SetRef(identity, "graspPoint", root.transform);

            var controller = root.AddComponent<InstrumentController>();
            SetRef(controller, "body", rb);
            var tip = tipGo.AddComponent<InstrumentTip>();
            SetRef(tip, "owner", controller);
            SetBool(tip, "isNeedleTip", true);

            var body = root.AddComponent<NeedleBody>();
            SetRef(body, "needleTip", tipGo.transform);
            SetRef(body, "needleTail", tail.transform);
            SetFloat(body, "arcRadius", NeedleArcRadius);
            SetFloat(body, "arcDegrees", NeedleArcDegrees);

            var thread = Empty("PH_SutureThread", root.transform, tailPos);
            Mark(thread, "Visual-only verlet suture thread (no collision, no knot tying).", "Suture thread (procedural, keep)");
            var line = thread.AddComponent<LineRenderer>();
            line.sharedMaterial = M.suture;
            line.widthMultiplier = 0.0012f;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            var suture = thread.AddComponent<SutureThreadVisual>();
            SetRef(suture, "anchor", tail.transform);
            return root;
        }

        // ------------------------------------------------------------------ NPC

        struct NpcRefs { public NPCRig rig; }

        static GameObject BuildNPC(out NpcRefs refs)
        {
            var root = new GameObject("NPC_Instructor");
            root.transform.SetPositionAndRotation(NpcPosition, Quaternion.Euler(0f, NpcYaw, 0f));
            var rig = root.AddComponent<NPCRig>();
            root.AddComponent<NPCManualController>();
            Transform t = root.transform;

            var body = Empty("Body", t, Vector3.zero);
            Mark(body, "Capsule mannequin. No animation, no IK. Replace with a rigged character.", "Rigged NPC surgeon (Blender)");
            Prim(PrimitiveType.Capsule, "Legs", body.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.30f, 0.45f, 0.22f), M.scrubs, keepCollider: true);
            Prim(PrimitiveType.Capsule, "Torso", body.transform, new Vector3(0f, 1.18f, 0f), new Vector3(0.40f, 0.30f, 0.26f), M.scrubs, keepCollider: true);
            Prim(PrimitiveType.Cylinder, "Neck", body.transform, new Vector3(0f, 1.50f, 0f), new Vector3(0.09f, 0.04f, 0.09f), M.npcSkin);

            var head = Empty("Head", t, new Vector3(0f, 1.62f, 0f));
            Prim(PrimitiveType.Sphere, "PH_HeadMesh", head.transform, Vector3.zero, new Vector3(0.19f, 0.23f, 0.21f), M.npcSkin);
            Prim(PrimitiveType.Sphere, "PH_SurgicalCap", head.transform, new Vector3(0f, 0.06f, -0.01f), new Vector3(0.20f, 0.15f, 0.22f), M.cap);
            Prim(PrimitiveType.Cube, "PH_Mask", head.transform, new Vector3(0f, -0.04f, 0.095f), new Vector3(0.14f, 0.08f, 0.02f), M.mask);
            var mount = Empty("CameraMount", head.transform, new Vector3(0f, 0.02f, 0.10f));
            var camGo = Empty("NPC_POV_Camera", mount.transform, Vector3.zero);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.enabled = false; // ObservationCamera renders only while a feed is active
            var obs = camGo.AddComponent<ObservationCamera>();
            SetEnum(obs, "role", (int)CameraRole.NPCPOV);

            NPCArm Arm(string side, float x)
            {
                var shoulder = Empty(side + "Shoulder", t, new Vector3(x * 0.21f, 1.42f, 0.02f));
                var armVisual = Prim(PrimitiveType.Cylinder, "PH_" + side + "ArmVisual", t, new Vector3(x * 0.2f, 1.25f, 0.18f), new Vector3(0.07f, 0.2f, 0.07f), M.scrubs, castShadows: true);
                var hand = Empty(side + "Hand", t, new Vector3(x * 0.18f, 1.08f, 0.35f));
                Prim(PrimitiveType.Sphere, "PH_" + side + "GloveMesh", hand.transform, Vector3.zero, new Vector3(0.08f, 0.05f, 0.10f), M.glove);
                var attach = Empty(side + "InstrumentAttachPoint", hand.transform, new Vector3(0f, 0f, 0.03f));
                return new NPCArm { shoulder = shoulder.transform, armVisual = armVisual.transform, hand = hand.transform, instrumentAttachPoint = attach.transform };
            }

            var left = Arm("Left", -1f);
            var right = Arm("Right", 1f);
            SetRef(rig, "head", head.transform);
            SetRef(rig, "cameraMount", mount.transform);
            SetRef(rig, "leftArm.shoulder", left.shoulder);
            SetRef(rig, "leftArm.armVisual", left.armVisual);
            SetRef(rig, "leftArm.hand", left.hand);
            SetRef(rig, "leftArm.instrumentAttachPoint", left.instrumentAttachPoint);
            SetRef(rig, "rightArm.shoulder", right.shoulder);
            SetRef(rig, "rightArm.armVisual", right.armVisual);
            SetRef(rig, "rightArm.hand", right.hand);
            SetRef(rig, "rightArm.instrumentAttachPoint", right.instrumentAttachPoint);

            refs = new NpcRefs { rig = rig };
            return root;
        }

        // ------------------------------------------------------------------ XR + cameras

        static GameObject InstantiateByGuid(string guid, Transform parent)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Error($"Meta XR prefab with GUID {guid} not found. Is com.meta.xr.sdk.interaction.ovr installed?");
                return null;
            }
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        static GameObject BuildXRRig(out Camera headsetCamera, out Transform centerEye)
        {
            headsetCamera = null;
            centerEye = null;

            var rigGo = InstantiateByGuid(OVRCameraRigGuid, null);
            if (rigGo == null) return null;
            rigGo.transform.SetPositionAndRotation(TraineeRigPosition, Quaternion.Euler(0f, TraineeYaw, 0f));

            if (rigGo.TryGetComponent(out OVRManager manager))
            {
                // Same approach as Meta's OVRComprehensiveInteractionRigWizard: the public setter is runtime only.
                FieldInfo f = typeof(OVRManager).GetField("_trackingOriginType", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(manager, OVRManager.TrackingOrigin.FloorLevel);
                else Error("OVRManager._trackingOriginType not found; set Tracking Origin to Floor Level manually.");
                manager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.ConformingToController;
                EditorUtility.SetDirty(manager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            }
            else
            {
                Error("OVRManager not found on OVRCameraRig.");
            }

            var interactionRig = InstantiateByGuid(OVRComprehensiveInteractionRigGuid, rigGo.transform);
            if (interactionRig != null)
            {
                foreach (OVRHand hand in rigGo.GetComponentsInChildren<OVRHand>(true))
                {
                    if (hand.transform.IsChildOf(interactionRig.transform)) continue;
                    if (hand.TryGetComponent(out OVRSkeletonRenderer sr)) sr.enabled = false;
                    if (hand.TryGetComponent(out OVRMesh mesh)) mesh.enabled = false;
                    if (hand.TryGetComponent(out OVRMeshRenderer mr)) mr.enabled = false;
                    if (hand.TryGetComponent(out SkinnedMeshRenderer smr)) smr.enabled = false;
                }

                Oculus.Interaction.Editor.UnityObjectAddedBroadcaster.HandleObjectWasAdded(interactionRig);

                var cameraRig = rigGo.GetComponent<OVRCameraRig>();
                int wired = 0, injected = 0;
                foreach (var rigRef in interactionRig.GetComponentsInChildren<OVRCameraRigRef>(true))
                {
                    var so = new SerializedObject(rigRef);
                    var p = so.FindProperty("_ovrCameraRig");
                    if (p != null && p.objectReferenceValue == null)
                    {
                        rigRef.InjectInteractionOVRCameraRig(cameraRig);
                        EditorUtility.SetDirty(rigRef);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(rigRef);
                        injected++;
                    }
                    wired++;
                }
                Log($"Interaction rig added. OVRCameraRigRef components: {wired} (manually injected: {injected}).");
            }

            centerEye = rigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (centerEye != null)
            {
                headsetCamera = centerEye.GetComponent<Camera>();
                if (headsetCamera != null)
                {
                    headsetCamera.nearClipPlane = 0.03f;
                    headsetCamera.farClipPlane = 30f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(headsetCamera);
                }
            }
            else
            {
                Error("TrackingSpace/CenterEyeAnchor not found under OVRCameraRig.");
            }
            return rigGo;
        }

        static ObservationCamera BuildOverheadCamera(Transform parent, Vector3 focus)
        {
            Vector3 pos = new Vector3(0.25f, 2.75f, -0.75f);
            var go = Empty("Overhead_Camera", parent, pos);
            go.transform.rotation = Quaternion.LookRotation((focus + Vector3.up * 0.05f - pos).normalized, Vector3.forward);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 12f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.enabled = false; // ObservationCamera renders only while a feed is active
            var obs = go.AddComponent<ObservationCamera>();
            SetEnum(obs, "role", (int)CameraRole.Overhead);
            Mark(Prim(PrimitiveType.Cube, "CameraHousing", go.transform, new Vector3(0f, 0f, -0.06f), new Vector3(0.08f, 0.08f, 0.12f), M.frame, castShadows: false),
                "Ceiling camera housing.", "Ceiling camera (Blender)");
            return obs;
        }

        static ObservationCamera BuildTraineeCaptureCamera(Transform centerEye)
        {
            if (centerEye == null) return null;
            var go = Empty("TraineePOV_CaptureCamera", centerEye, Vector3.zero);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 90f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 20f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.enabled = false; // ObservationCamera renders only while a feed is active
            var obs = go.AddComponent<ObservationCamera>();
            SetEnum(obs, "role", (int)CameraRole.TraineePOV);
            return obs;
        }

        // ------------------------------------------------------------------ project settings

        static void AddSceneToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Log("Build Settings: " + ScenePath + " is scene 0 (existing scenes kept).");
        }

        /// <summary>
        /// Quest recommendations on the Mobile URP asset used by Android: HDR off (saves
        /// bandwidth, avoids an extra resolve), MSAA 4x (cheap on tiled mobile GPUs).
        /// </summary>
        public static void ApplyQuestRenderSettings()
        {
            const string path = "Assets/Settings/Mobile_RPAsset.asset";
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                Error("Mobile_RPAsset not found at " + path);
                return;
            }
            var so = new SerializedObject(asset);
            var hdr = so.FindProperty("m_SupportsHDR");
            var msaa = so.FindProperty("m_MSAA");
            if (hdr == null || msaa == null)
            {
                Error("Could not find m_SupportsHDR / m_MSAA on the URP asset.");
                return;
            }
            Log($"Mobile_RPAsset before: HDR={hdr.boolValue}, MSAA={msaa.intValue}x");
            hdr.boolValue = false;
            msaa.intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            Log("Mobile_RPAsset after: HDR=False, MSAA=4x");
        }
    }
}
