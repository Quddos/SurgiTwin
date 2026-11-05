# SurgiTwin-XR — Phase 1 Testing Report

Date: 2026-11-05. All results below were produced in this session; nothing here was tested on a headset.

## 1. Compilation

| Check | Result |
|---|---|
| Runtime scripts compiled standalone with Unity's Roslyn against the project's assemblies | **0 errors, 0 warnings** from SurgiTwinXR code |
| Editor scripts compiled the same way | **0 errors** |
| Unity Editor compile (Assembly-CSharp + Assembly-CSharp-Editor) | **Succeeded** (final recompile 17:33, heartbeat written after domain reload) |

## 2. Scene build (SurgiTwin → Phase 1 → Build Operating Theatre)

Builder report: XR interaction rig added (1 OVRCameraRigRef, wired automatically by Meta's auto-wiring),
scene saved as `Assets/SurgiTwinXR/Scenes/SurgiTwin_OR_Phase1.unity`, inserted as Build Settings scene 0,
Mobile_RPAsset changed from HDR=True/MSAA=1x to HDR=False/MSAA=4x. **0 errors.**

## 3. Scene validation (SurgiTwin → Phase 1 → Validate Scene)

```
[OK]   No missing scripts.
[OK]   One OVRCameraRig with OVRManager.
[OK]   Interaction rig wired (1 OVRCameraRigRef).
[OK]   Tracking origin: Floor Level
[OK]   Instruments: 5 (NEEDLE_HOLDER_01, GRASPER_01, SCISSORS_01, GRASPER_02, NEEDLE_01)
[OK]   VirtualPatient present.
[OK]   Needle-passing task with 4 rings.
[OK]   Camera roles TRAINEE_POV, NPC_POV, OVERHEAD (one each)
[OK]   All required SurgiTwin references assigned.
[OK]   Scene is first in Build Settings.
[INFO] Renderers: 343, triangles: 137,762, materials: 41, lights: 2 (realtime shadow casters: 1)
Result: 0 error(s), 0 warning(s).
```

Final run at 17:33. An earlier run warned that `SurgicalTaskController.config` was unassigned. The builder
now re-loads its ScriptableObjects after creating the new scene, which fixed it.

## 4. Visual check

Reference renders from the edit-mode scene (`SurgiTwinAutomation/shots/*.png`): trainee eye view, operative
field (pad, 4 rings, needle), instrument tray (needle holder, grasper, scissors), wide theatre view, NPC side
view, monitor view, overhead view. Layout, scale and materials look correct. Finding: the surgical spot
light over-exposed the skin around the pad; intensity reduced from 3.2 to 1.6; the 17:33 screenshots confirm correct exposure.

## 5. Quest build

| Item | Result |
|---|---|
| Target | Android, IL2CPP, ARM64 |
| Result | **Succeeded**, 0 errors (974 warnings, all from packages / IL2CPP) |
| Output | `Builds/Android/SurgiTwinXR_Phase1.apk`, **95.5 MB** on disk |
| Time | 17 min 16 s first build; final rebuild 45 s at 17:33, with the §3/§4 fixes |
| Installed / run on a Quest 3S | **No** — not done in this session |

(The build report's "total size" of 1530 MB counts intermediate build files; the APK itself is 95.5 MB.)

## 6. Pending

Nothing is pending in the editor. Everything in §7 still needs the headset.

## 7. On-device test plan (to do with the headset)

Install: `adb install -r Builds/Android/SurgiTwinXR_Phase1.apk`, launch *SurgiTwin* from Unknown Sources.

| # | Step | Expected |
|---|---|---|
| 1 | Launch, stand up | Floor-level tracking; you stand at the right side of the patient, tray to your left |
| 2 | Look at the monitor tower | Upper screen shows the NPC POV feed; lower screen task status `NOT_STARTED` |
| 3 | Reach the needle holder, press **grip** | Instrument follows the controller; log `CONTROL_ACQUIRED … HUMAN_XR_RIGHT` |
| 4 | Squeeze **index trigger** | Upper jaw closes |
| 5 | Close the jaws on the needle (within 2 cm of the jaw centre) | Needle attaches; task `IN_PROGRESS` |
| 6 | Pass the needle tip through the cyan ring | Ring turns green, next ring turns cyan, status 1/4 |
| 7 | Touch the pad with an instrument tip | Pad tints orange (contact) / red (≥ 4 mm deep); short haptic pulse |
| 8 | Pass all 4 rings | Task `COMPLETED`, pad green, patient `TASK_COMPLETED` |
| 9 | Press **Y** | Task reset, free instruments return to the tray |
| 10 | Press **B** | Log written; `adb pull /sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/SurgiTwinLogs` |
| 11 | Hand-off: grasp the needle with the grasper while the needle holder holds it | Needle moves to the grasper |
| 12 | Performance | Check with OVR Metrics Tool: target 72 Hz stable |

NPC (editor Play Mode, no headset needed): select `NPC_Instructor`, NPCManualController context menu →
*Move test hand to test instrument* → *Grasp test instrument* → *Lift test hand 15 cm* → *Close jaws* →
*Release instrument*. While the NPC holds GRASPER_02, the trainee cannot grab it.

Editor keyboard (Play Mode): Space = close jaws of a held instrument, R = reset, T = start, E = export, C = cycle feed.
