# SurgiTwin XR — Surgical Training Framework for Meta Quest (v1.0)

SurgiTwin XR is a Unity framework for building **VR surgical-skills training scenarios on Meta Quest**. It ships
with a complete demo operating theatre and a **needle-passing drill**: the trainee picks up laparoscopic-style
instruments with the Touch controllers, grasps a curved needle, passes it through four rings on a training pad,
and every session is recorded to a structured JSON file for analysis.

> **Placeholder art.** All geometry in v1.0 (theatre, patient, instruments) is made of Unity primitives and two
> procedural meshes, named `PH_…`. The value of the package is the framework: interaction, simulation state,
> recording, cameras and the NPC interface. `BLENDER_ASSET_WORKFLOW.md` explains how to swap in real models
> without touching code.

> **Not a medical device.** SurgiTwin XR is a training and research framework. It does not model real tissue
> mechanics, and it has not been clinically validated. Do not use it to assess clinical competence or to make
> medical decisions.

---

## 1. Requirements

| | Version used to build and test v1.0 |
|---|---|
| Unity | **6000.3.15f1** (Unity 6.3) with **Android Build Support** (OpenJDK, SDK & NDK) |
| Render pipeline | **URP** 17.3 |
| Meta XR All-in-One SDK | **207.0.1** (`com.meta.xr.sdk.all`) — *not included, install separately* |
| OpenXR Plugin | **1.16.1** (`com.unity.xr.openxr`) |
| Input System | **1.19.0** (`com.unity.inputsystem`) |
| Headset | Meta Quest 3S (tested). Quest 3 / Quest 2 should work with the same settings but were not tested |

Other Unity 6 versions are likely to work but were not tested.

## 2. Setup (new URP project)

1. **Create a project** from the *Universal 3D* (URP) template, and install Android Build Support in Unity Hub.
2. **Switch platform:** *File → Build Profiles → Android → Switch Platform*.
3. **Install the dependencies** in *Window → Package Manager*:
   * **Meta XR All-in-One SDK** (from the Asset Store / Meta's UPM registry, version 207.x).
   * **OpenXR Plugin** and **Input System** (Unity Registry).
4. **Import SurgiTwin XR.** Everything lives in `Assets/SurgiTwinXR/`.
5. **XR Plug-in Management** (*Edit → Project Settings → XR Plug-in Management*), Android tab:
   * Enable **OpenXR**.
   * Under *OpenXR → Android*, enable the **Meta Quest** feature group (Meta Quest Support, Meta XR feature)
     and add the **Meta Quest Touch Plus / Touch Pro** controller profiles.
   * Run *Meta → Tools → Project Setup Tool* and apply its fixes.
6. **Player settings** (Android):
   * Scripting backend **IL2CPP**, target architecture **ARM64** only, minimum API level **32**.
   * Color space **Linear**. Active input handling **Input System Package (New)** or **Both**.
   * **Set your own Package Name** (e.g. `com.yourcompany.surgitwin`). Do not keep a template default such as
     `com.UnityTechnologies.…`: if another app on the headset already used that name, the new app can hang on its
     loading screen (this happened during testing).
7. **Open the demo:** `Assets/SurgiTwinXR/Scenes/SurgiTwin_OR_Phase1.unity`.
8. **Check it:** *Tools → SurgiTwin XR → Validate Scene*. Expect `0 error(s)`.
9. Optional but recommended for Quest: *Tools → SurgiTwin XR → Apply Quest Render Settings (HDR off, MSAA 4x)*.
   This changes the **project's active URP asset**.
10. **Build:** add the scene as scene 0 in the build profile, connect the Quest (developer mode on), *Build And Run*.

### Rebuilding the theatre

*Tools → SurgiTwin XR → Build Operating Theatre* regenerates the demo scene, prefabs and materials from code. It
**overwrites** those generated files. Use it to start over, not after you have hand-edited the art.

## 3. Using the demo

| Input | Action |
|---|---|
| **Grip** | Grab an instrument (needle holder, two graspers, scissors) |
| **Index trigger** | Close the instrument's jaws |
| Jaws closed on the needle | Needle attaches to the jaws; another instrument can take it from the first (hand-off) |
| Needle tip through the cyan ring | Ring turns green, the next ring turns cyan; 4 rings complete the task |
| Instrument tip on the pad | Pad tints orange (contact) or red (≥ 4 mm deep); short haptic pulse |
| **Y** (left) | Reset the task; free instruments return to the tray |
| **B** (right) | Export the session JSON now |
| **Left stick click** | Cycle the theatre monitor between the three camera feeds |
| Editor keyboard (Play Mode) | Space = close jaws, R = reset, T = start, E = export, C = cycle feed |

The NPC instructor never moves on its own. In Play Mode, select `NPC_Instructor` and use the
`NPCManualController` context menu (move hand to instrument, grasp, lift, close jaws, release). While the NPC
holds an instrument, the trainee cannot take it.

## 4. Session data

Each session is written to `Application.persistentDataPath/SurgiTwinLogs/<sessionId>.json` when you press **B**,
when the app is paused (headset removed) and on quit. The file contains the task result, an event log and the
pose of every instrument at **30 Hz** (position, rotation, jaw closure, contact, who controls it). The schema
(`surgitwin.phase1.session/1`) is documented in `ARCHITECTURE.md`.

* The recording buffer holds **30 minutes** by default (`InstrumentTrajectoryRecorder.capacityMinutes`).
* The JSON is written on a background thread. The main thread only copies a snapshot (0.6 ms for 145 000
  samples on Quest 3S). In testing, exports of up to 59 000 samples kept every frame under 20 ms.
* JSON size is about 305 bytes per sample. A full 30-minute recording of the demo's 5 instruments (270 000
  samples) is about 83 MB and takes about 4 s to write in the background.

### Getting session files off the headset

On Android, every export is also copied to the headset's shared **`Download/SurgiTwinLogs/`** folder. You can:

* open it with the headset's *Files* app, or
* pull it over USB:

```bash
adb shell content query --user $(adb shell am get-current-user) --uri content://media/external/downloads --projection _id:_display_name:_size
adb exec-out content read --user <user> --uri content://media/external/downloads/<_id> > session.json
```

This works even when the headset is signed in to a secondary Android user, where `adb pull` of the app's own
folder fails with *Permission denied*. The copy never leaves the device unless you pull it; nothing is uploaded.
Set `SurgicalSessionManager.publishToDownloads = false` to keep files only in the app's private folder.

**Privacy.** Session files contain motion data and the device model; they contain no names or account data. If
you add trainee identifiers, treat the files as personal data.

## 5. Extending the framework

* **New instrument:** create an `InstrumentDefinition` asset, give the prefab an `InstrumentController`,
  `InstrumentIdentity`, an `InstrumentTip` trigger collider and the Meta `Grabbable`/`GrabInteractable`
  components (copy one of the demo instrument prefabs). The recorder, task and tissue systems pick it up
  automatically.
* **New driver** (scripted robot, AI policy, network peer): implement `IInstrumentControlSource` and call
  `InstrumentController.TryBeginControl` / `EndControl`. Instrument code never needs to know who drives it.
* **New NPC behaviour:** implement `INPCController` (see `NPCManualController`).
* **New task:** see `SurgicalTaskController` and `NeedlePassingTaskConfig`; tasks report state changes as events
  that the session log records.
* **Camera feeds:** `SurgicalCameraManager.StartFeed(CameraRole)` returns a `RenderTexture` you can show, save
  or stream.

## 6. Known limitations (v1.0)

* Placeholder art; no textures beyond flat materials.
* The tissue pad is rigid: contact is depth-based, with no deformation, bleeding or suture physics.
* The suture thread is visual only. There is no knot tying and no scissor cutting.
* Hand tracking can grab, but jaw closure uses the controller trigger only.
* Single user; no networking or streaming.
* Tested on Quest 3S only.

## 7. Files in this folder

| File | Contents |
|---|---|
| `README.md` | This guide |
| `ARCHITECTURE.md` | How the systems fit together, the JSON schema, performance decisions |
| `BLENDER_ASSET_WORKFLOW.md` | Replacing placeholder geometry with your own models |
| `CHANGELOG.md` | Version history |
| `Third-Party Notices.txt` | Dependencies and their licenses |
