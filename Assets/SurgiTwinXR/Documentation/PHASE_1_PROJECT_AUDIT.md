# SurgiTwin-XR — Phase 1 Project Audit

Audit of the existing Unity project at `J:\SurgiTwin-XR\SurgiTwin`, done before any Phase 1 change.
Every Meta SDK API used in Phase 1 was checked against the installed package sources in
`Library/PackageCache` (not against memory or online docs).

## 1. Engine and render pipeline

| Item | Found | Notes |
|---|---|---|
| Unity | **6000.3.15f1** | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline | **URP 17.3.0** | `Assets/Settings/Mobile_RPAsset.asset` for Android/Quest, `PC_RPAsset` for desktop |
| Quality levels | Mobile (index 0) = default for Android; PC (index 1) | `QualitySettings.asset` |
| Mobile URP asset (before Phase 1) | HDR **on**, MSAA **off (1x)**, render scale 0.8, main-light shadows on, soft shadows off | HDR on / MSAA off is not ideal for Quest (see §7) |

## 2. XR stack

| Item | Found |
|---|---|
| XR plug-in | **OpenXR 1.16.1** loader for Android and Standalone, *Initialize XR on Startup* on |
| OpenXR features enabled (Android) | Meta Quest Support, Meta XR Feature, Meta XR Foveation, Hand Tracking Subsystem, Meta Hand Tracking Aim, Oculus Touch / Touch Pro / Touch Plus controller profiles, Hand Interaction Profile |
| Meta SDK | **Meta XR All-in-One 207.0.1**: `com.meta.xr.sdk.core`, `.interaction`, `.interaction.ovr`, audio, haptics, platform, voice, MR Utility Kit |
| Stereo rendering | Single-pass instanced (`m_StereoRenderingPath: 2`) |

Because the **Meta XR Feature** is enabled on top of OpenXR, OVRPlugin runs on the OpenXR backend, so
`OVRCameraRig`, `OVRManager` and `OVRInput` are available. Phase 1 therefore uses **one** interaction
framework: the Meta Interaction SDK (no XR Interaction Toolkit is installed and none was added).

## 3. Android / build

| Item | Found |
|---|---|
| Active build target | Android |
| Scripting backend | IL2CPP |
| Architecture | ARM64 only |
| Min SDK | 32 |
| Application ID | `com.UnityTechnologies.com.unity.template.urpblank` (template default — **should be changed** before any store / device distribution) |
| Company / product | DefaultCompany / SurgiTwin |
| Scenes in build (before) | `Assets/Scenes/SampleScene.unity` only |

## 4. Input

* Active input handling: **Input System package only** (1.19). Old `UnityEngine.Input` is not available.
* Controller input in Phase 1 goes through the Meta Interaction SDK (grab) and `OVRInput` (index trigger, buttons).
* Keyboard shortcuts for editor testing use `UnityEngine.InputSystem.Keyboard`.

## 5. Existing content

* Scenes: `SampleScene` (URP template). Not modified.
* Scripts: none of the project's own (only the URP template `TutorialInfo`).
* `Assets/Oculus`, `Assets/XR`, `Assets/Resources`, `Assets/Plugins`: Meta / XR generated settings. Not modified.
* Git: repository initialised, **no commits yet**.

## 6. Meta Interaction SDK v207 — verified facts used by Phase 1

| Fact | Source file (package cache) |
|---|---|
| `OVRCameraRig` prefab GUID `126d619cf4daa52469682f85c1378b4a`, `OVRComprehensiveInteractionRig` GUID `0a7d2469f24041c4284c66706f84c45e` | `com.meta.xr.sdk.interaction.ovr/Editor/QuickActions/Scripts/OVRComprehensiveInteractionRigWizard.cs` |
| The wizard instantiates the interaction rig **under** the camera rig, sets `OVRManager._trackingOriginType = FloorLevel` by reflection and `controllerDrivenHandPosesType = ConformingToController`, disables duplicate `OVRHand` visuals, then calls `UnityObjectAddedBroadcaster.HandleObjectWasAdded` | same file |
| `OVRCameraRigRef._ovrCameraRig` is auto-wired to ancestors by `OVRAutoWiring`; `InjectInteractionOVRCameraRig(OVRCameraRig)` exists as a fallback | `interaction.ovr/Editor/AutoWiring/OVRAutoWiring.cs`, `Runtime/Scripts/Input/OVRCameraRigRef.cs` |
| Comprehensive rig contains `ControllerGrabInteractor` (selector = **GripButton**, `_controllerButtonUsage: 16`) and controller/hand `HandGrabInteractor`s | `interaction/Runtime/Prefabs/Grab/ControllerGrabInteractor.prefab`, `ControllerPrimitives.cs` |
| `Grabbable` serialized fields `_rigidbody`, `_kinematicWhileSelected`, `_throwWhenUnselected`; creates a `GrabFreeTransformer` if none assigned | `Runtime/Scripts/Interaction/Grabbable/Grabbable.cs` |
| `GrabInteractable._rigidbody`, `HandGrabInteractable._rigidbody`, `PointerInteractable._pointableElement` | `Models/Grab/GrabInteractable.cs`, `Models/HandGrab/HandGrabInteractable.cs`, `Pointable/PointerInteractable.cs` |
| `PointableElement.WhenPointerEventRaised : Action<PointerEvent>`; `PointerEventType {Hover, Unhover, Select, Unselect, Move, Cancel}`; `PointerEvent.Identifier`, `PointerEvent.Pose` | `Pointable/PointableElement.cs`, `Pointable/IPointable.cs` |
| `OVRInput.Get(Axis1D.PrimaryIndexTrigger, Controller)`, `OVRInput.SetControllerVibration(freq, amp, controller)`, `OVRCameraRig.leftHandAnchor/rightHandAnchor`, `OVRPlugin.systemDisplayFrequency` | `com.meta.xr.sdk.core/Scripts/OVRInput.cs`, `OVRCameraRig.cs`, `OVRPlugin.cs` |
| `Oculus.Interaction`, `Oculus.Interaction.OVR`, `Oculus.VR` (+ their Editor assemblies) are **autoReferenced**, so `Assembly-CSharp` can use them without asmdefs | the respective `.asmdef` files |

## 7. Findings and recommendations

1. **Mobile URP asset: HDR on, MSAA off.** On Quest, HDR costs bandwidth and MSAA 4x is cheap on the tiled GPU.
   → The Phase 1 builder sets **HDR off, MSAA 4x** on `Mobile_RPAsset` (logged before/after; reversible).
2. **Application ID is the URP template default.** Not changed (it is a product decision). Change it in
   *Player Settings → Other Settings → Package Name* before distributing.
3. **Only `SampleScene` in Build Settings.** The Phase 1 scene is inserted as scene 0; SampleScene is kept.
4. No packages were installed, removed, upgraded or downgraded.
5. Unity Editor was open on the project during the work, so batch-mode builds were not possible. Phase 1
   provides editor menu items plus a file-based automation hook (`SurgiTwinAutomation/request.txt`) instead.
