# SurgiTwin XR — Asset Store listing draft (v1.0.0)

Not part of the package. Paste these fields into the Publisher Portal (publisher.unity.com) and edit freely.

**Category:** Templates → Systems (or Templates → Tutorials if you position it as a learning project)
**Title:** SurgiTwin XR — Surgical Training Framework for Meta Quest
**Short description (summary):** A Meta Quest VR framework for surgical-skills training: grabbable laparoscopic-style instruments, a needle-passing drill, an NPC instructor interface, three observation cameras and 30 Hz session recording to JSON.

---

## Description

> **Requires the Meta XR All-in-One SDK 207.x (free, not included), the OpenXR Plugin and URP.**
> **Placeholder art:** all models are simple primitives, so the package is a framework rather than an art pack.
> **Not a medical device:** for training and research only; not clinically validated.

SurgiTwin XR gives you a working VR operating theatre for Meta Quest that you can build your own surgical-skills
scenarios on.

**What you get**
* **Instrument control system:** one controller per instrument, which the trainee (Meta Interaction SDK grab),
  an NPC or another instrument can drive. Exclusive control, jaw closure on the index trigger, tip contact and
  needle hand-off between instruments.
* **Needle-passing drill:** a curved needle, a training pad and four rings passed in order, with progress shown
  on an in-theatre monitor and haptic feedback on tissue contact.
* **Patient and task state machines** that produce clean, structured events.
* **Session recording:** the pose, jaw state, contact and controller of every instrument at 30 Hz, plus an event
  log, exported as documented JSON. It holds 30 minutes by default and is written on a background thread.
* **Headset file retrieval:** each session is also copied to the Quest's shared Downloads folder, so you can pull
  it over USB from any Android user account.
* **Three observation cameras** (trainee POV, NPC POV, overhead) with on-demand low-rate RenderTexture feeds.
* **NPC instructor rig** with a non-autonomous controller interface, ready for scripted or learned behaviours.
* **Editor tools** (*Tools → SurgiTwin XR*): rebuild the demo theatre from code, validate the scene, capture
  reference screenshots, apply Quest render settings.
* **Documentation:** setup guide, architecture guide, JSON schema and a Blender art-replacement workflow.

**Performance:** on a Meta Quest 3S the demo holds 72 Hz (App GPU time about 5 ms of the 13.9 ms budget).

**Tested with:** Unity 6000.3.15f1, URP 17.3, OpenXR 1.16.1, Meta XR SDK 207.0.1, Meta Quest 3S.

**Limitations:** rigid tissue (no deformation), visual-only suture, no knot tying or cutting, controller trigger
for jaws (hand tracking can grab only), single user.

**Third-party:** this asset depends on the Meta XR All-in-One SDK under the Meta Platform Technologies SDK
License Agreement, which you install separately. See `Third-Party Notices.txt`.

---

## Technical details

* Render pipeline: URP (Built-in and HDRP not supported).
* Platforms: Android (Meta Quest), 64-bit (ARM64), IL2CPP. Runs in the Editor Play Mode with keyboard shortcuts.
* Scripts: 41 C# files, all under the `SurgiTwinXR` namespaces, full source.
* Content:
  * 1 demo scene and 12 prefabs (environment, patient, instruments, NPC).
  * 32 materials (flat URP Lit) and 2 procedural meshes (ring, needle).
  * No textures, no audio.
* Documentation: `README.md`, `ARCHITECTURE.md`, `BLENDER_ASSET_WORKFLOW.md`, `CHANGELOG.md` (offline, in the package).

## Keywords

VR, XR, Meta Quest, Quest 3, medical, surgery, surgical training, simulation, laparoscopy, healthcare, OpenXR, Interaction SDK, template

---

## Publisher Portal fields to fill in yourself

* **Dependencies:** add **Meta XR All-in-One SDK** as an Asset Store dependency, and the Unity packages
  (OpenXR, Input System, URP) in the package manifest step of the uploader.
* **AI description (rule 1.6):** the guidelines require you to disclose any AI-generated functional part of
  the product. Describe here how AI assistance was used in writing the code and documentation.
* **Price, publisher name, support email and website.**
* **Key images and screenshots** (sizes are given in the portal). Use *Tools → SurgiTwin XR → Capture Reference
  Screenshots* for raw shots, plus in-headset captures. Screenshots must show the real placeholder look.
* **Supported Unity versions:** 6000.3 (tested).
