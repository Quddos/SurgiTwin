# SurgiTwin-XR — Phase 1 Implementation Report

Date: 2026-11-05 · Unity 6000.3.15f1 · URP 17.3 · OpenXR 1.16.1 + Meta XR SDK 207.0.1 · Target: Meta Quest 3S

## 1. What was delivered

All Phase 1 content lives under `Assets/SurgiTwinXR`. It is produced by **SurgiTwin → Phase 1 → Build
Operating Theatre**, which generates materials, meshes, ScriptableObjects, prefabs and the scene
`Scenes/SurgiTwin_OR_Phase1.unity`. Nothing outside that folder was changed except:

* `ProjectSettings/EditorBuildSettings.asset`: Phase 1 scene inserted as scene 0 (SampleScene kept).
* `Assets/Settings/Mobile_RPAsset.asset`: HDR on → **off**, MSAA 1x → **4x** (Quest recommendation).
* `.gitignore`: `/SurgiTwinAutomation/` and `/[Bb]uilds/` added.

No packages were added, removed, upgraded or downgraded.

## 2. Files

### Runtime scripts (`Scripts/`, 34 files)

| Folder | Files |
|---|---|
| Core | `SurgiTwinTypes.cs` (PatientState, InstrumentState, InstrumentType, TaskState, ControlSourceType, CameraRole, HandSide, cached log tokens) |
| Instruments | `InstrumentInterfaces.cs` (IInstrumentControlSource, IInstrumentControl), `InstrumentController.cs`, `InstrumentIdentity.cs`, `InstrumentDefinition.cs`, `InstrumentTip.cs`, `InstrumentJawVisual.cs`, `NeedleBody.cs`, `NeedleGrasp.cs`, `SutureThreadVisual.cs` |
| Patient | `VirtualPatient.cs`, `PatientStateController.cs` |
| Anatomy | `AnatomyRegion.cs`, `SurgicalTargetArea.cs` |
| Simulation | `Session/SurgicalSessionManager.cs`, `Recording/InstrumentTrajectoryRecorder.cs`, `SurgicalTaskController.cs`, `NeedleGate.cs`, `NeedlePassingTaskConfig.cs`, `TissueInteractionController.cs`, `SurgicalWorkspaceController.cs`, `TaskProgressDisplay.cs` |
| XR | `HumanXRInstrumentInput.cs`, `SessionInputShortcuts.cs`, `XRRuntimeStatus.cs` |
| Cameras | `ObservationCamera.cs`, `SurgicalCameraManager.cs`, `MonitorFeedDisplay.cs` |
| NPC | `NPCRig.cs`, `INPCController.cs`, `NPCManualController.cs`, `NPCHandInstrumentDriver.cs` |
| Utilities | `JsonText.cs`, `ProceduralMeshes.cs`, `PlaceholderMarker.cs` |
| Interaction | reserved (empty) |

### Editor scripts (`Scripts/Editor/`)

`SurgiTwinPhase1Builder.cs`, `SurgiTwinBuilderUtil.cs`, `SurgiTwinPhase1Validator.cs`,
`SurgiTwinScreenshots.cs`, `SurgiTwinAutomation.cs`.

Menu **SurgiTwin → Phase 1**: Build Operating Theatre · Validate Scene · Capture Reference Screenshots ·
Apply Quest Render Settings · Build Quest APK (Android).

### Generated assets

* Prefabs: `Environment/PH_OperatingTable`, `PH_SurgicalLights`, `PH_InstrumentTrolley`, `PH_MonitorTower`,
  `PH_AnesthesiaMachine`, `PH_IVStand`; `Patient/VirtualPatient`; `Instruments/Instrument_NeedleHolder`,
  `Instrument_Grasper`, `Instrument_Scissors`, `Instrument_Needle_WithSuture`; `NPC/NPC_Instructor`.
* Materials: 32 URP/Lit or URP/Unlit materials in `Art/Materials`.
* Meshes: `Art/Meshes/SM_NeedleRing.asset` (torus), `SM_CurvedNeedle.asset` (tapered half-circle).
* ScriptableObjects: `ID_NeedleHolder`, `ID_Grasper`, `ID_Scissors`, `ID_Needle`, `NeedlePassingTaskConfig`.

## 3. Scene hierarchy (`SurgiTwin_OR_Phase1`)

```
Environment
  DirectionalLight_Fill
  PH_Room (floor, walls, ceiling, rails, door, ceiling light panels)
  PH_OperatingTable
  PH_SurgicalLights (LightHead_Main with SpotLight, LightHead_Satellite)
  PH_InstrumentTrolley          (trainee side)
  PH_NPCInstrumentTray          (NPC side)
  PH_MonitorTower (FeedScreen + FeedLabel, StatusScreen + StatusText) [MonitorFeedDisplay, TaskProgressDisplay]
  PH_AnesthesiaMachine, PH_IVStand, PH_BackTable, KickBucket
VirtualPatient
Instruments
  Instrument_NeedleHolder  (NEEDLE_HOLDER_01)
  Instrument_Grasper       (GRASPER_01)
  Instrument_Scissors      (SCISSORS_01)
  Instrument_Grasper_NPC   (GRASPER_02, on the NPC tray)
  Instrument_Needle        (NEEDLE_01, on the training pad, with PH_SutureThread)
NPC_Instructor
OVRCameraRig                      (Meta prefab instance, floor-level tracking)
  TrackingSpace/CenterEyeAnchor/TraineePOV_CaptureCamera
  OVRComprehensiveInteractionRig  (Meta prefab instance)
ObservationCameras
  Overhead_Camera
SurgiTwinSystems
  SessionManager    [SurgicalSessionManager, InstrumentTrajectoryRecorder, SessionInputShortcuts, XRRuntimeStatus]
  CameraManager     [SurgicalCameraManager]
  NeedlePassingTask [SurgicalTaskController]
  SurgicalWorkspace [SurgicalWorkspaceController]
```

## 4. Prefab hierarchies

**VirtualPatient** [VirtualPatient, PatientStateController]
```
Head (PH_HeadMesh, PH_NeckMesh)
Torso (PH_ChestMesh, PH_ShoulderMesh)
LeftArm / RightArm
Abdomen (PH_AbdomenMesh)
  AbdominalSurface (PH_SkinSurfaceMesh)
    SurgicalTargetArea [SurgicalTargetArea, trigger approach box] (PH_TrainingPadMesh)
      TissueInteractionZone [TissueInteractionController, trigger box]
      NeedlePassingRings / Ring_1..4 [NeedleGate] (RingMesh, RingPost_n)
Pelvis, Legs
PH_Drapes (lower, upper, window, side drapes, anaesthesia screen)
```

**Instrument_NeedleHolder** (grasper and scissors are the same layout; grasper/scissors have a `JawLowerPivot`)
```
Root [Rigidbody, InstrumentIdentity, InstrumentController, InstrumentJawVisual, NeedleGrasp,
      Grabbable, GrabInteractable, HandGrabInteractable, HumanXRInstrumentInput]
  Handle [BoxCollider] (PH_HandleBody, PH_GripPad_L/R, PH_Lever, PH_Ratchet, PH_RotationKnob)
  Shaft  [CapsuleCollider] (PH_ShaftMesh)
  Jaws   (PH_JawLowerMesh, JawUpperPivot/PH_JawUpperMesh, GraspPoint, TipPoint [InstrumentTip, trigger sphere])
```

**Instrument_Needle_WithSuture** [Rigidbody (kinematic until grasped), InstrumentIdentity, InstrumentController, NeedleBody, 3 sphere colliders]
→ NeedleMesh, TipPoint [InstrumentTip needle], TailPoint, PH_SutureThread [LineRenderer, SutureThreadVisual].

**NPC_Instructor** [NPCRig, NPCManualController]
→ PH_Body (legs, torso, neck), Head (mesh, cap, mask, CameraMount/NPC_POV_Camera [ObservationCamera]),
Left/RightShoulder, PH_Left/RightArmVisual, Left/RightHand (glove mesh, InstrumentAttachPoint).

## 5. Feature status

### Fully implemented (built, compiled, wired, validated in the editor)
* Folder structure, procedural scene/prefab builder, validator, screenshot tool, automation hook.
* Operating theatre layout (room, table, lights, trolleys, monitor tower, anaesthesia machine, IV stand, back table).
* VirtualPatient hierarchy with anatomy labels and PatientStateController (5 states, priority rules, visual tint).
* Instrument architecture: one InstrumentController per instrument; separate human (HumanXRInstrumentInput),
  NPC (NPCHandInstrumentDriver) and instrument (NeedleGrasp) drivers behind IInstrumentControlSource.
* InstrumentIdentity (ID, type, handle, shaft, tip, grasp point) and InstrumentState machine (5 states).
* Meta XR rig: OVRCameraRig + OVRComprehensiveInteractionRig, floor-level tracking, rig reference wired.
* Three observation cameras registered by role in SurgicalCameraManager; feed-on-demand RenderTextures.
* NPC placeholder rig (root, head, camera mount, arms, hands, attach points) with interface-based manual control.
* Needle-passing task (4 rings, order, progress, completion, reset, optional time limit).
* Trajectory recorder (30 Hz, pre-allocated struct buffer) and JSON session export with event log.
* Quest render settings (HDR off, MSAA 4x), static batching of the environment, light/shadow budget.
* **Quest APK build completed** (see the testing report for the exact result).

### Implemented, not physically tested (needs a headset)
* Grabbing instruments with Touch controllers (grip) through the Meta Interaction SDK.
* Index-trigger jaw closure, needle pick-up with the needle holder / grasper, needle hand-off.
* Ring passing detection, patient state transitions and pad tint during real use.
* Haptic pulse on tissue contact.
* Controller shortcuts (Y reset, B export, left stick click cycle feed).
* Log export to `/sdcard/Android/data/<package>/files/SurgiTwinLogs` on pause / quit.
* Runtime frame rate on Quest 3S (no profiling has been done).
* NPC test commands at runtime (context menu in Play Mode).

### Placeholder
* All geometry: Unity primitives named `PH_…` with PlaceholderMarker (see BLENDER_ASSET_WORKFLOW.md).
* Suture thread: visual verlet line only (no collision, no knot, no tension).
* Tissue: rigid pad; contact is depth-based, no deformation or bleeding.
* NPC: static capsule mannequin, arms are stretched cylinders, no IK or animation.
* Monitor status text uses the legacy TextMesh with the built-in font.

### Not implemented (out of Phase 1 scope by design)
* VLM / VLA / cloud inference, any AI-driven NPC behaviour.
* WebRTC / render streaming of the camera feeds.
* Hand-tracking jaw control (pinch) — hands can grab, but jaw closure is mapped to the controller trigger only.
* Scissors cutting, knot tying, suture physics, soft-tissue simulation, scoring / skill metrics.
* Multi-user networking.

## 6. Notes and known issues

* **Unity file-watcher quirk during development.** Two scripts written while the editor was importing were
  not registered by the asset database; they were moved to `Simulation/Session/` and `Simulation/Recording/`
  so Unity imported them fresh. No action needed now.
* The builder **overwrites** its generated prefabs/materials/scene. Stop using it once art is hand-edited.
* The Android package name is still the URP template default (`com.UnityTechnologies.com.unity.template.urpblank`).
* The automation hook (`SurgiTwinAutomation.cs`) polls a folder outside `Assets`. It is editor-only and can be
  deleted at any time without affecting the project.
