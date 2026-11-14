# itch.io page — SurgiTwin XR (copy into the itch.io "Create new project" form)

## Basic fields

| Field | Value |
|---|---|
| **Title** | SurgiTwin XR |
| **Project URL** | `surgitwin-xr` → your link becomes `https://<your-itch-username>.itch.io/surgitwin-xr` |
| **Short description or tagline** | VR surgical-skills training for Meta Quest: grab real instruments, pass the needle, record every move. |
| **Classification** | Games → or **Tools** (choose *Tools* if you want it seen as a training app / framework) |
| **Kind of project** | **Downloadable** |
| **Release status** | **Released** (or *Prototype* if you prefer to signal an early version) |
| **Pricing** | **No payments** (free), or *$0 or donate* |
| **Genre** | Simulation |
| **Tags** | `vr`, `meta-quest`, `oculus-quest`, `simulation`, `medical`, `surgery`, `training`, `unity`, `3d`, `educational` |
| **App store links** | leave empty (add the Unity Asset Store link later, when it is approved) |
| **Custom noun** | leave empty |
| **Community** | Comments: on |
| **Visibility** | **Draft** first → check the page → then **Public** |

## Uploads (files)

| File (in `Builds/Release/`) | itch.io settings |
|---|---|
| `SurgiTwinXR_v1.0.0_MetaQuest.apk` (91 MB) | Tick **Android**. Display name: *SurgiTwin XR for Meta Quest (APK, sideload)* |
| `SurgiTwinXR_v1.0.0_UnityPackage.unitypackage` (0.5 MB) | No platform tick. Display name: *Unity package (source framework, Unity 6 + Meta XR SDK)* |

## Media

* **Cover image:** `Docs/Showcase/cover-630x500.png`
* **Gameplay video or trailer:** your YouTube link (see the guide)
* **Screenshots** (upload in this order):
  1. `04-operating-theatre.png`
  2. `01-trainee-view.png`
  3. `02-operative-field.png`
  4. `03-instrument-tray.png`
  5. `06-monitor-feeds.png`
  6. `05-npc-instructor.png`
  7. `07-overhead-view.png`

---

## Description (paste into the description box)

**SurgiTwin XR** is a virtual-reality operating theatre for practising core laparoscopic surgical skills on
**Meta Quest**. You stand at the table, pick up the instruments with your Touch controllers and complete a
needle-passing drill, while the app records every movement for later analysis.

### What you can do
* **Pick up real instruments:** a needle holder, two graspers and scissors. Grip to hold them; the index trigger
  opens and closes the jaws.
* **Grasp and hand off a curved needle** between instruments, as in real suturing practice.
* **Needle-passing drill:** guide the needle tip through four rings in order on a training pad. Each ring turns
  green as you pass it and the theatre monitor tracks your progress.
* **Feel the tissue:** the pad changes colour on contact and deep contact, with a short haptic pulse in the
  controller.
* **Three camera views** on the theatre monitor: your point of view, the instructor's, and an overhead camera.
* **Every session recorded:** instrument positions, rotations and jaw states at 30 times a second, plus a
  timeline of events, saved as a JSON file you can analyse.

### Controls
| | |
|---|---|
| Grip | Pick up / hold an instrument |
| Index trigger | Close the jaws |
| Y | Reset the drill |
| B | Save the session file now |
| Left stick click | Switch the monitor camera |

### Install (Meta Quest 3 / 3S)
1. Enable **Developer Mode** for your headset in the Meta Horizon phone app.
2. Download the APK and install it with **Meta Quest Developer Hub** or **SideQuest** (or `adb install`).
3. In the headset, open **Library → Unknown Sources → SurgiTwin**.

Session files are saved in the headset's **Download/SurgiTwinLogs** folder.

### Built with
Unity 6 (URP) · OpenXR · Meta XR SDK (Interaction SDK) · C# · Meta Quest 3S.
It runs at a steady 72 fps on Quest 3S.

### Status
Version 1.0 is a working prototype. The models are simple placeholder shapes; the focus is the interaction,
simulation and data-recording systems. More realistic models and new procedures are planned.

*SurgiTwin XR is a training and research prototype. It is not a medical device and has not been clinically
validated.*

**Developer:** Raheem Quddus
