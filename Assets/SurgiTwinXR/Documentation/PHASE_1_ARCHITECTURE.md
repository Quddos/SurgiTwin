# SurgiTwin-XR — Phase 1 Architecture

## Goals of the Phase 1 architecture

* A Quest 3S operating theatre where a trainee holds laparoscopic instruments with Touch controllers.
* **One instrument controller per instrument**, driven through an interface by either the human (XR) or
  the NPC (scripted now, a learned policy later). No instrument code knows who is driving it.
* Patient and task state machines that produce clean, structured, JSON-exportable logs.
* Three independently addressable observation cameras (no streaming yet).
* Nothing fake: the NPC does nothing unless commanded; no VLM / VLA / cloud / WebRTC in Phase 1.

## Folder layout (`Assets/SurgiTwinXR`)

```
Art/            Materials, Meshes (procedural ring + needle), Models (Blender, empty), Textures
Prefabs/        Environment/, Patient/, Instruments/, NPC/
Scenes/         SurgiTwin_OR_Phase1.unity
Scripts/
  Core/         enums + log tokens (SurgiTwinTypes)
  Patient/      VirtualPatient, PatientStateController
  Anatomy/      AnatomyRegion, SurgicalTargetArea
  Instruments/  IInstrumentControl / IInstrumentControlSource, InstrumentController, InstrumentIdentity,
                InstrumentTip, InstrumentJawVisual, NeedleBody, NeedleGrasp, SutureThreadVisual, InstrumentDefinition
  Interaction/  (reserved; XR-specific interaction lives in XR/)
  Simulation/   SurgicalSessionManager, SurgicalTaskController, NeedleGate, NeedlePassingTaskConfig,
                TissueInteractionController, SurgicalWorkspaceController, InstrumentTrajectoryRecorder, TaskProgressDisplay
  XR/           HumanXRInstrumentInput, SessionInputShortcuts, XRRuntimeStatus
  Cameras/      ObservationCamera, SurgicalCameraManager, MonitorFeedDisplay
  NPC/          NPCRig, INPCController, NPCManualController, NPCHandInstrumentDriver
  Utilities/    JsonText, ProceduralMeshes, PlaceholderMarker
  Editor/       SurgiTwinPhase1Builder (+Util), SurgiTwinPhase1Validator, SurgiTwinScreenshots, SurgiTwinAutomation
ScriptableObjects/  InstrumentDefinition assets, NeedlePassingTaskConfig
Settings/       (reserved for SurgiTwin-specific settings assets)
Documentation/  this folder
```

No assembly definitions: runtime code compiles into `Assembly-CSharp`, editor code into
`Assembly-CSharp-Editor`. The Meta assemblies are auto-referenced (verified).

## Instrument control — one controller, many drivers

```
             IInstrumentControlSource                    IInstrumentControl
 ┌──────────────────────────┐                 ┌──────────────────────────────────┐
 │ HumanXRInstrumentInput   │──TryBeginControl│                                  │
 │  (Meta ISDK Grabbable    │  EndControl     │       InstrumentController       │
 │   pointer events,        │  SetJawClosure  │  state: IDLE HELD MOVING         │
 │   OVRInput trigger)      │────────────────▶│         INTERACTING RELEASED     │
 ├──────────────────────────┤                 │  jaw closure, tip contact,       │
 │ NPCHandInstrumentDriver  │  + AttachToDriver│  tissue interaction, follow     │
 │  (NPCManualController)   │────────────────▶│  (kinematic attach)              │
 ├──────────────────────────┤                 │                                  │
 │ NeedleGrasp              │────────────────▶│  (the needle is an instrument)   │
 │  (needle holder jaws)    │                 └──────────────────────────────────┘
 └──────────────────────────┘
```

* `TryBeginControl` is exclusive: while the NPC holds an instrument the human cannot take it
  (`HumanXRInstrumentInput` disables the SDK interactables), and vice versa.
* Human motion is applied by the Meta SDK (`Grabbable` + `GrabFreeTransformer`); the controller only observes
  it. NPC and needle-holder motion is applied by `AttachToDriver` (kinematic follow, relative pose kept).
* State: held + tip contact or tissue interaction → `INTERACTING`; held + speed above threshold →
  `MOVING`; held → `HELD`; just let go → `RELEASED` (0.5 s) → `IDLE`.
* The needle uses the same `InstrumentController`. Its control source is a `NeedleGrasp` (on the needle
  holder and grasper), whose `SourceType` reports the human or NPC behind the holding instrument.

## Patient

`VirtualPatient` (PatientRoot) → Head, Torso, LeftArm, RightArm, Abdomen → AbdominalSurface →
SurgicalTargetArea (training pad + approach trigger + needle rings) → TissueInteractionZone (trigger).
Plus Pelvis, Legs and Drapes. Each region carries an `AnatomyRegion` label.

`PatientStateController` derives its state by priority from counters fed by other systems:

| Input | Fed by |
|---|---|
| target selected | `SurgicalTargetArea` (instrument tip inside the approach volume) |
| instrument contact | `TissueInteractionController` (tip within 3 mm of the tissue surface) |
| tissue interaction | `TissueInteractionController` (tip ≥ 4 mm deep, or needle tip below the surface) |
| task completed | `SurgicalTaskController` |

`TASK_COMPLETED > TISSUE_INTERACTION > INSTRUMENT_CONTACT > TARGET_SELECTED > NORMAL`. The training pad
is tinted per state (MaterialPropertyBlock).

## Simulation and logging

* `SurgicalTaskController`: needle passing through 4 rings in order. Auto-starts when the needle is first
  grasped. Progress = rings passed / 4. Optional time limit → `ABORTED`. Reset returns everything not held
  to its start pose.
* `NeedleGate`: segment/plane test of the needle tip against the ring plane each frame (no physics triggers,
  no missed fast passes).
* `SurgicalWorkspaceController`: oriented box around the operative field; logs tip enter/exit.
* `InstrumentTrajectoryRecorder`: 30 Hz, pre-allocated `TrajectorySample[]` (120 000 samples ≈ 16 min for
  4 instruments). Zero heap allocation per sample.
* `SurgicalSessionManager`: session id `ST_yyyyMMdd_HHmmss`, event log, JSON export to
  `Application.persistentDataPath/SurgiTwinLogs/<sessionId>.json`, also on app pause (headset removed) and quit.

### JSON schema `surgitwin.phase1.session/1`

```json
{
  "schema": "surgitwin.phase1.session/1",
  "sessionId": "ST_20261105_101500",
  "startedUtc": "...", "durationSeconds": 123.4, "device": "...", "unityVersion": "6000.3.15f1",
  "units": "metres, unity world space (left-handed, Y up)",
  "task": { "taskId": "NEEDLE_PASSING_01", "state": "COMPLETED", "gatesPassed": 4, "gateCount": 4,
            "progress": 1, "elapsedSeconds": 48.2 },
  "patientState": "TASK_COMPLETED",
  "events": [ { "t": 3.21, "type": "CONTROL_ACQUIRED", "subject": "NEEDLE_HOLDER_01", "detail": "HUMAN_XR:HUMAN_XR_RIGHT" } ],
  "trajectory": {
    "sampleRateHz": 30, "sampleCount": 9000, "bufferOverflow": false,
    "instruments": [ { "id": "NEEDLE_HOLDER_01", "type": "NEEDLE_HOLDER" } ],
    "samples": [ { "t": 3.233, "instrumentId": "NEEDLE_HOLDER_01", "pos": [x,y,z], "rot": [x,y,z,w],
                   "interactionState": "MOVING", "controlSource": "HUMAN_XR", "controlSourceId": "HUMAN_XR_RIGHT",
                   "jaw": 0.82, "tipContact": false, "tissueInteraction": false,
                   "patientState": "TARGET_SELECTED", "taskState": "IN_PROGRESS" } ]
  }
}
```

Event types: `SESSION_STARTED/ENDED`, `PATIENT_STATE`, `TASK_STATE`, `GATE_PASSED`, `INSTRUMENT_STATE`
(HELD↔MOVING flips omitted; they are in the samples), `CONTROL_ACQUIRED/RELEASED`, `WORKSPACE_ENTER/EXIT`.

## Cameras

| Role | Object | Notes |
|---|---|---|
| `TRAINEE_POV` | `OVRCameraRig/TrackingSpace/CenterEyeAnchor/TraineePOV_CaptureCamera` | mono capture camera riding on the HMD; the headset itself still renders through the OVR eye camera |
| `NPC_POV` | `NPC_Instructor/Head/CameraMount/NPC_POV_Camera` | follows the NPC head |
| `OVERHEAD` | `ObservationCameras/Overhead_Camera` | ceiling camera over the table |

`SurgicalCameraManager.StartFeed(role)` returns a RenderTexture (640×360, 15 Hz by default: the camera
is enabled for one frame per interval). Feeds are off unless requested; the theatre monitor requests
`NPC_POV`. Phase 2 can hand these textures to a streaming layer without touching the cameras.

## NPC

`NPCRig` (root, head, camera mount, two arms with hands and instrument attach points) + `NPCManualController`
(implements `INPCController`; `IsAutonomous == false`). Hands move only to explicitly set targets at a capped
speed; the inspector context menu has test commands (move hand to the NPC grasper, grasp, lift, close jaws,
release). A future policy controller implements the same `INPCController` and drives the same instruments.

## XR rig

Meta `OVRCameraRig` + `OVRComprehensiveInteractionRig` as prefab instances (same steps as Meta's wizard,
reproduced in the builder), floor-level tracking, controller-driven hands conforming to controller.
Instruments: `Grabbable` (+ default `GrabFreeTransformer`), `GrabInteractable` (controllers),
`HandGrabInteractable` (hands, optional), throw on release disabled.

Controls: **grip** grabs, **index trigger** closes the jaws, **Y** resets the task, **B** exports the log,
**left stick click** cycles the monitor feed.

## Quest performance decisions

* One realtime directional light (hard shadows) + one realtime spot (no shadows); emissive panels for the rest.
* Environment static-batched (except the monitor tower, whose text changes).
* Small parts (instrument pieces, rings, drapes' thin edges) do not cast shadows.
* Observation cameras off by default; when on, low resolution and 15 Hz.
* No per-frame allocations in recorder, task, tissue and instrument code (struct buffers, cached tokens).
* Mobile URP asset: HDR off, MSAA 4x.
