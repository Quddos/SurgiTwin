# SurgiTwin-XR — Phase 1.1 Quest 3S Headset Validation

Status: **D1 and D2 fixed and verified on the headset. D3 fixed; its fix has not yet been run for the full 30 minutes. All
interaction tests in §3 PASS. Trajectory JSON retrieval over ADB is blocked by the headset's multi-user setup.**
Only results that were actually observed are marked PASS or FAIL. Raw logs: `SurgiTwinAutomation/device/` (not under version control).

## 1. Connection and installation (2026-11-05)

| Item | Result |
|---|---|
| ADB | Unity 6000.3.15f1 bundled `platform-tools/adb.exe`, version 36.0.0 |
| Device | `340YC20G7Q0CR1`, model **Quest 3S** (`panther`), USB, authorised |
| OS | Android 14 (SDK 34), Meta Horizon OS build 207, OVR OS API 160 |
| Battery / temp at start | 45 %, charging, 32.0 °C |
| APK install (`SurgiTwinXR_Phase1.apk`, 17:33 build) | **PASS** — `adb install -r` → Success |
| App launch (17:33 build, template package name) | **FAIL** — endless loading dots (defect D1) |
| APK install (2026-11-06 10:23 build, `com.bdnaturetech.surgitwinxr`) | **PASS** — `adb install -r` → Success |
| App launch (10:23 build) | **PASS** — engine passes `Company Name`, OpenXR starts, app submits frames (`logcat_fixed_launch.txt`) |

## 2. Defects

### D1 — App never leaves the loading dots (reproduced 6×, release build)

**Symptom.** The headset shows the system loading dots forever. The Unity process stays alive at 0 % CPU
(0.13–0.17 s total CPU, ~160 MB resident). Unity's last log line is
`Device Model 'Oculus Quest 3S'…`; the engine never logs `Company Name`, never initialises OpenXR, never
submits a frame.

**Reproduced with:** `adb am start`, the LAUNCHER intent, and a manual launch from *App Library → Unknown
Sources*; Vulkan and `-force-gles`; Quest Link connected and disconnected.

**Ruled out (with evidence):**

| Hypothesis | Evidence |
|---|---|
| Headset / runtime state | Another Unity 6 GameActivity app on the same headset (`VRMotionLearning.TennisEsports`, Unity 6000.0.78) started normally minutes later and reached OpenXR init. |
| GameActivity entry point | The installed Meta XR Core SDK 207 *requires* GameActivity on Unity 2023.2+ (`Editor/Rules/AndroidCompatibility.cs`), so it was kept. |
| Quest Link | Link was active at first (`com.oculus.xrstreamingclient`); after disabling it the hang was unchanged. |
| Manifest | Merged manifest has `MAIN`/`LAUNCHER`/`com.oculus.intent.category.VR` and `focusaware=true`. |
| Missing data / OBB | `assets/bin/Data` (globalgamemanagers, metadata) is inside the APK; no split binary. |
| Permission dialog | No runtime-permission activity appeared; `RECORD_AUDIO` is requested but not prompted. |
| Meta Runtime Optimizer plugin | Initialised from C# — only after engine init, which is never reached. |

**Root cause (diagnosed with a Development build, `SurgiTwinXR_diag_dev.apk`).** The development player
logs what the release player does not:

```
Determine if il2cpp resources need to extracted:
    Missing '/storage/emulated/10/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/il2cpp/unity.ver'
    Extracting il2cpp resources
  il2cpp failed to extract resources ('assets/bin/Data/Managed' => '/storage/emulated/10/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/il2cpp')
```

and then stalls exactly where the release build stalls. The project still uses the **URP template's
default Android package name** `com.UnityTechnologies.com.unity.template.urpblank`. That name had already
been used on this headset by another Unity project: its external storage holds `files/Recordings`
(August 2026) and was last modified 17 September 2026, before SurgiTwin existed. Because the APKs are
signed with the same local debug keystore, `adb install -r` silently *replaced* that app and kept its
storage, and IL2CPP cannot extract into it.

**Fix (F1).** Give SurgiTwin its own Android application id **`com.bdnaturetech.surgitwinxr`**
(organisation `bdnaturetech` from the project's cloud settings), set via
`PlayerSettings.SetApplicationIdentifier` (menu *SurgiTwin → Phase 1 → Set Unique Android Package Name*,
automation command `set-android-id`). Nothing on the headset is deleted; the other project's app and data
are left as they are. No script or scene functionality changes.

Status: **fixed and verified.** Release build 2026-11-06 10:23 (0 errors, APK 95.5 MB) was installed and
launched at 11:06. Unity logs `ApplicationInfo 'com.bdnaturetech.surgitwinxr'`, continues past `Company Name`
into OpenXR initialisation (`MetaXRFeature is enabled`), and the app's own process submits frames at 72 Hz.

### D2 — Frame hitch on every session export (B)

**Observed.** Each export is followed, one VrApi sample later, by a dropped-frame second: 11:21:59.9 export → 19 Hz
(App 31 ms, 72 stale frames); 11:30:47 / :51 / :55 exports → 60, 39 and 54 Hz. The rest of the session holds 72 Hz.

**Cause (from the code).** `SurgicalSessionManager.ExportSession()` builds the whole JSON (every trajectory sample) and
writes it to disk synchronously on the main thread, inside `SessionInputShortcuts.Update()`. The 11:22 export
serialised the full 120 000-sample buffer.

**Fix (F2).** On the main thread, `ExportSession` now only copies the session state and the recorded samples into
a plain-data snapshot (`InstrumentTrajectoryRecorder.CaptureSnapshot`, a single `Array.Copy` into a reused
array). The JSON is then built and written on a worker thread (`Task.Run`). The file goes to `<name>.json.tmp`
and is then moved into place, so readers never see a half-written file. If B is pressed while an export is
still running, one more export is queued and runs when the current one finishes. On pause or quit the export
runs synchronously so the file is complete before the process stops.

Status: **fixed and verified.** Build 2026-11-06 13:03, session `ST_20261106_085947`
(`device/logcat_d2d3_retest.txt`). The export log line now comes from a thread-pool thread (tids 22929, 22977
and 22978), not Unity's main thread (22567).

| Export (log time) | Samples written | VrApi sample before | VrApi samples after |
|---|---|---|---|
| 14:30:27.4 | 6 005 | 72 Hz | 73, 72, 73 Hz — 0–1 stale frames |
| 14:31:16.7 | 13 375 | 73 Hz | 72, 73 Hz — 0 stale frames |
| 14:31:18.3 | 13 615 | 73 Hz | 72 Hz — 0 stale frames |
| 14:31:19.0 (0.75 s after the previous one, so it went through the queue) | 13 725 | 72 Hz | 73, 72, 73 Hz — 0 stale frames |

Before the fix, every export was followed by a 19, 60, 39 or 54 Hz second. **Limitation:** those earlier exports
wrote the full 120 000-sample buffer, while this retest wrote at most 13 725 samples. The work left on the main
thread is the snapshot copy, which grows with the sample count. It has not been measured with a full 30-minute
buffer (270 000 samples).

### D3 — Trajectory buffer full after ~13 minutes

**Observed.** `[SurgiTwin] Trajectory buffer full; further samples are dropped.` at 11:19:42, 13 min 21 s into the
session. Samples after that point are lost.

**Cause.** `InstrumentTrajectoryRecorder.capacity = 120000`, documented as "~16 min for 4 instruments at 30 Hz",
but the scene has **5** instruments (needle holder, 2 graspers, scissors, needle): 120 000 / (5 × 30 Hz) = 800 s
= 13.3 min, which matches the observation.

**Fix (F3).** The fixed `capacity` field was replaced by `capacityMinutes` (default 30). When recording starts,
the buffer is sized to minutes × 60 × `sampleRateHz` × the number of active instruments. For this scene that is
30 × 60 × 30 × 5 = 270 000 samples. If a session runs past that, the warning still appears once and tells you
to raise `capacityMinutes`.

Status: **fixed; not verified on a long session.** In the 1 min 40 s retest session, the recorder logged 13 725
samples in about 92 s, which matches 5 instruments × 30 Hz. No `buffer full` warning appeared. The session
was not run past the old 13 min 21 s limit.

### Note — session JSON not retrievable over ADB on this headset (environment, not an app defect)

The headset has three Android users; the signed-in account is user 10 (`am get-current-user` → 10), but `adb shell`
runs as user 0. Every path to user 10's storage (`/storage/emulated/10/...`, `/data/media/10/...`,
`/mnt/user/*/emulated/10/...`) returns `Permission denied`. `/sdcard` resolves to user 0, which has no SurgiTwin
data. `run-as` is refused because the release build is not debuggable. The export itself is confirmed by the app's
own log line.

## 3. Test results

Performed 2026-11-06 11:06–11:31 on the 10:23 build by the user in the headset. PASS = the user observed the expected behaviour; log evidence is noted where it exists.

| Test | Result |
|---|---|
| Head tracking | **PASS** — world stable and smooth when turning and leaning |
| Floor-level tracking | **PASS** — floor matches the real floor, table at waist height, user beside the patient |
| Left controller tracking | **PASS** |
| Right controller tracking | **PASS** |
| Instrument grab (grip) | **PASS** — needle holder follows the hand while grip is held |
| Instrument release | **PASS** — drops when grip is released |
| Instrument rotation | **PASS** — turns with the wrist |
| Trigger-driven jaw movement | **PASS** — upper jaw closes and opens with the index trigger |
| Needle grasping | **PASS** — needle attaches to the needle holder jaws |
| Needle hand-off | **PASS** — needle transfers from needle holder to grasper |
| Ring detection | **PASS** — ring turns green, next ring cyan, monitor count advances |
| Task completion | **PASS** — all 4 rings, monitor shows COMPLETED |
| Reset (Y) | **PASS** — count back to 0, loose instruments return to the tray |
| Haptic feedback | **PASS** — short vibration on pad contact; pad tints orange/red |
| Monitor shows NPC_POV feed + task status at start | **PASS** |
| Feed cycle (left thumbstick click) | **PASS** — label and feed change on a firm stick click (a first, lighter attempt did not register; not a defect) |
| Cameras TRAINEE_POV / NPC_POV / OVERHEAD on device | **PASS** — each shows a sensible live image; NPC_POV shows the instructor-side view, not the headset view |
| Session export (B) | **PASS** — log: `Session exported: /storage/emulated/10/Android/data/com.bdnaturetech.surgitwinxr/files/SurgiTwinLogs/ST_20261106_053621.json` (4×) |
| Trajectory JSON retrievable over ADB | **NOT TESTED — blocked**: file is in user 10 storage, which `adb shell` (user 0) cannot read (see §2 note) |
| Frame rate / frame time / GPU / memory / thermal | **PASS** at 72 Hz (§3.1, §3.2). The export hitches (D2) are fixed in the 13:03 build (§3.3) |
| Session export without a frame drop (13:03 build) | **PASS** — 4 exports, 72 Hz and 0 stale frames after each (§2 D2) |
| Regression check on the 13:03 build (grab, jaws) | **PASS (user-reported)** — the user grabbed the needle holder and closed the jaws, then reported the step done with no problem; there is no separate log check for this |
| Trajectory buffer lasts 30 min (13:03 build) | **NOT TESTED** at full length; capacity computed as 270 000 samples (§2 D3) |
| Interaction latency | NOT TESTED |

### 3.1 Idle performance baseline

App pid 8561, 2026-11-06 11:06:38–42, right after launch, no interaction. Five 1-second VrApi samples:

| Metric | Observed |
|---|---|
| Frame rate | 67–73 / 72 Hz (one 67 sample right after startup, then 72–73) |
| App GPU time | 4.45–4.87 ms (72 Hz budget 13.9 ms) |
| CPU&GPU time | 7.73–8.98 ms |
| GPU utilisation | 39–41 % |
| CPU / GPU level | 2 / 2 |
| Stale frames | 7 in the first sample, then 0 |
| Free memory | 3622 MB |
| Temperature | 33.0 °C |

### 3.2 Performance over the interactive session

App pid 8561, 1460 one-second VrApi samples, 2026-11-06 11:06:38–11:33:14, covering all the tests in §3.

| Metric | Observed |
|---|---|
| Samples at ≥ 71 / 72 Hz | 1453 / 1460 (99.5 %) |
| App GPU time | avg 4.99 ms, max 5.90 ms (budget 13.9 ms) |
| CPU&GPU time | avg 8.36 ms, max 9.90 ms |
| GPU utilisation | avg 43 %, max 54 % |
| Temperature | 33.0 → 35.0 °C max |
| Free memory | min 3422 MB |

Of the 7 samples below 71 Hz, 2 were at startup (67, 68), 1 read 70, and 4 came exactly one second after a session export (19, 60, 39, 54 Hz; see D2).

### 3.3 Retest of the 13:03 build (D2/D3 fixes)

App pid 22517, 2026-11-06 14:29:44–14:31:33, 109 one-second VrApi samples. 106 were at ≥ 71 Hz. The three
lower ones were 69 Hz at the first frame, 62 Hz during startup at 14:29:50, and 70 Hz at 14:30:25, two seconds
*before* the first export. App GPU time was 4.0–5.9 ms and CPU&GPU time 6.6–9.6 ms, the same as §3.2. The
temperature was 28 °C and free memory was 3645 MB.

Note: early `VrApi FPS=90/90` lines in the log came from Horizon OS's own process (pid 25915), not from
SurgiTwin, and are not SurgiTwin performance data.

## 4. Test-harness notes

* The headset must be worn (or the proximity sensor overridden) to launch apps; otherwise it sleeps or
  shows `SensorLockActivity`.
* With the Meta Horizon Link app running on the PC, Quest Link reconnects automatically over USB. Disable
  it in the headset under *Settings → System → Quest Link* for standalone testing.
* The default 256 KB logcat main buffer rotates Unity's startup lines out within seconds; it was raised to
  16 MB with `adb logcat -G 16M` (resets on reboot).
* Diagnostic APKs built for the bisect (`Builds/Android/SurgiTwinXR_diag_*.apk`, not shipped):
  `dev` (Development + script debugging), `gles` (GLES3 only), `sample` (template SampleScene).
