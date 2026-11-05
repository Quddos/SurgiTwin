# SurgiTwin-XR — Blender Asset Workflow

Phase 1 geometry is placeholder Unity primitives. Every placeholder object is named `PH_…` and carries a
`PlaceholderMarker` component whose *Intended Asset* field names the asset that should replace it.
This document describes how to replace them without breaking the scripts.

## Golden rule: replace meshes, keep the hierarchy

All scripts reference **empty group transforms** (e.g. `Handle`, `Shaft`, `Jaws/JawUpperPivot`,
`TipPoint`, `GraspPoint`, `SurgicalTargetArea`, `Head/CameraMount`). The `PH_` mesh children under them can
be deleted and replaced by imported meshes freely. Do not rename or move the group transforms unless you also
update the references (the validator will tell you if something is unassigned).

## Units, axes and scale

| Setting | Value |
|---|---|
| Blender unit scale | 1.0, metric, **metres** |
| Export | FBX, *Apply Scalings: FBX All*, *Apply Transform* on (axis settings: see the note below) |
| Unity import | Scale factor 1, *Convert Units* on, *Bake Axis Conversion* on |
| Instrument axis convention | local **+Z = towards the tip**, origin at the handle/shaft junction |
| Needle | arc in the local XZ plane, centre at origin, tail at +X (t=0), tip at -X (t=1), radius 16 mm |
| Patient | supine, head towards +Z, origin on the table pad top under the abdomen |

> **Axis check.** Blender is Z-up, Unity is Y-up, and the FBX axis options interact with *Bake Axis Conversion*.
> Do one test export of a simple arrow first and confirm in Unity that it points along local **+Z**; keep
> those settings for every asset. If an asset arrives rotated, fix it with a parent rotation in Blender, not in the prefab.

Real sizes: 5 mm instrument shaft, ~300 mm shaft length, handle ~120 mm; needle 26–32 mm chord.

## Budgets for Quest 3S

| Asset | Triangles (LOD0) | Materials |
|---|---|---|
| Each laparoscopic instrument | ≤ 4 000 | 1–2 (shared atlas) |
| Needle | ≤ 500 | shared steel |
| Patient (visible parts) | ≤ 15 000 | 2 (skin, tissue) |
| NPC surgeon | ≤ 15 000 | 2 |
| Operating table, lights, trolleys, monitors | ≤ 5 000 each | shared atlas |
| Whole scene | ≤ 150 000 visible | as few as possible (SRP Batcher compatible URP/Lit) |

Textures: ASTC compression, 1024² for hero props, 512² for background, mip maps on. Bake AO into the base map.
Use one trim sheet / atlas for theatre equipment so static batching keeps draw calls low.

## Materials

Use **URP/Lit** (or URP/Simple Lit for background). Keep the `_BaseColor` property: `PatientStateController`
and `NeedleGate` tint renderers through a MaterialPropertyBlock on `_BaseColor`. The monitor feed screen uses
**URP/Unlit** with `_BaseMap` (the RenderTexture is assigned at runtime).

## Colliders

Do **not** use mesh colliders on moving instruments. Keep the primitive colliders from the placeholders:
box on the handle, capsule on the shaft, trigger sphere on `TipPoint` (tag component `InstrumentTip`). For the
patient, simple boxes/capsules are enough; the tissue logic uses the `TissueInteractionZone` trigger and the
surface transform, not the mesh.

## Step by step: replacing the needle holder

1. Model the needle holder in Blender with origin at the handle/shaft junction, shaft along the axis that
   your test export showed becomes Unity +Z. Separate the **upper jaw** as its own object, origin
   at the hinge.
2. Export `SM_NeedleHolder.fbx` into `Assets/SurgiTwinXR/Art/Models/Instruments/`.
3. Open `Prefabs/Instruments/Instrument_NeedleHolder.prefab`.
4. Delete the `PH_…` children under `Handle`, `Shaft` and `Jaws`; drop the handle/shaft mesh under `Handle`,
   the fixed jaw under `Jaws`, the moving jaw under `Jaws/JawUpperPivot` (zero local transform).
5. Check that `TipPoint` and `GraspPoint` still sit at the jaw tip / jaw centre. Move them if needed.
6. Save the prefab and run **SurgiTwin → Phase 1 → Validate Scene**.

The same procedure applies to the grasper (two jaw pivots), scissors, NPC (head mesh under `Head`, hands under
`LeftHand` / `RightHand`; the `CameraMount` must stay at eye level) and the patient regions.

## Do not regenerate after hand-editing

`SurgiTwin → Phase 1 → Build Operating Theatre` **overwrites** the generated prefabs, materials and the scene.
Once you start replacing art by hand, stop using the builder (or copy the prefabs to a new folder first).
