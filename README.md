# SurgiTwin XR

**VR surgical-skills training for Meta Quest, built with Unity 6, OpenXR and the Meta XR SDK.**

![SurgiTwin XR operating theatre](Docs/Showcase/banner-1280x720.png)

SurgiTwin XR is a virtual operating theatre where a trainee holds laparoscopic-style instruments with the Quest
Touch controllers and practises a needle-passing drill. Every movement is recorded at 30 Hz to a structured JSON
file for analysis. The project is designed as a framework: any driver can control the same instruments, whether
the trainee, a scripted NPC instructor or, later, a learned policy.

**▶ Demo video:** [YouTube](https://youtu.be/yHMeXCzPlD0) · **⬇ Download:** [Releases](../../releases) · **🎮 itch.io:** [quddosr.itch.io/surgitwin-xr](https://quddosr.itch.io/surgitwin-xr)

---

## Features

* **Grabbable surgical instruments:** needle holder, two graspers and scissors, using the Meta Interaction SDK.
  The index trigger drives the jaws.
* **Curved needle with hand-off** between instruments, with exclusive control so two drivers never fight over one instrument.
* **Needle-passing drill:** four rings passed in order, progress on an in-theatre monitor, auto-start and reset.
* **Patient and tissue state machine:** the pad tints on contact and deep contact, with controller haptics.
* **Session recording:** pose, jaw state, contact and controller of every instrument at 30 Hz, plus an event log.
  The JSON is written on a background thread with no frame drops, and the buffer holds 30 minutes.
* **Headset data retrieval:** sessions are copied to the Quest's shared Downloads folder, so they can be pulled
  over USB even from secondary Android user accounts.
* **Three observation cameras** (trainee POV, instructor POV, overhead) with on-demand RenderTexture feeds.
* **NPC instructor rig** with a non-autonomous controller interface, ready for scripted or AI behaviours.
* **Editor tooling:** rebuild the whole theatre from code, scene validator, screenshot capture, Quest render settings.

## Screenshots

| | |
|---|---|
| ![Trainee view](Docs/Showcase/01-trainee-view.png) | ![Operative field](Docs/Showcase/02-operative-field.png) |
| ![Instrument tray](Docs/Showcase/03-instrument-tray.png) | ![Monitor feeds](Docs/Showcase/06-monitor-feeds.png) |
| ![NPC instructor](Docs/Showcase/05-npc-instructor.png) | ![Overhead view](Docs/Showcase/07-overhead-view.png) |

## Tested on Meta Quest 3S

| | |
|---|---|
| Frame rate | Steady **72 Hz** (99.5 % of one-second samples at 71 Hz or more over a 26-minute interactive session) |
| GPU time | App GPU about **5 ms** of the 13.9 ms frame budget |
| Session export | Main-thread snapshot copy **0.6 ms** for 145 000 samples; formatting and writing happen on a worker thread |
| Recording | Exactly 30 Hz for each of 5 instruments over 16 continuous minutes; no samples lost |

Validation reports, including the defects found on the headset and their root causes, are in
[`Docs/Internal`](Docs/Internal).

## Tech stack

Unity 6000.3 (URP 17.3) · OpenXR 1.16 · Meta XR All-in-One SDK 207 (Interaction SDK) · C# · Android IL2CPP ARM64 ·
Meta Quest 3S

## Run it

* **Headset:** download the APK from [Releases](../../releases), enable Developer Mode, and install it with Meta
  Quest Developer Hub, SideQuest or `adb install`. Then open *Library → Unknown Sources → SurgiTwin*.
* **Unity:** open this project in Unity 6000.3 with Android Build Support, then open
  `Assets/SurgiTwinXR/Scenes/SurgiTwin_OR_Phase1.unity`. Full setup:
  [`Assets/SurgiTwinXR/Documentation/README.md`](Assets/SurgiTwinXR/Documentation/README.md).

| Control | Action |
|---|---|
| Grip | Grab an instrument |
| Index trigger | Close the jaws |
| Y | Reset the drill |
| B | Save the session file |
| Left stick click | Switch the monitor camera |

## Documentation

* [Setup guide](Assets/SurgiTwinXR/Documentation/README.md)
* [Architecture and JSON schema](Assets/SurgiTwinXR/Documentation/ARCHITECTURE.md)
* [Replacing the placeholder art](Assets/SurgiTwinXR/Documentation/BLENDER_ASSET_WORKFLOW.md)
* [Changelog](Assets/SurgiTwinXR/Documentation/CHANGELOG.md)

## Status and roadmap

**v1.0** is a working prototype: the models are simple placeholder shapes, and the focus is the interaction,
simulation and data systems. Planned: realistic models, soft-tissue and suture physics, more procedures, and
AI-driven instructor behaviour.

*SurgiTwin XR is a training and research prototype. It is not a medical device and has not been clinically validated.*

## Author

**Raheem Quddus**, design and development.

© 2026 Raheem Quddus. All rights reserved.
