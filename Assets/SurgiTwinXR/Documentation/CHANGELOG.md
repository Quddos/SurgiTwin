# Changelog

## 1.0.0 — first release

**Framework**
* Instrument control system: one `InstrumentController` per instrument, driven through `IInstrumentControlSource` by the trainee (Meta Interaction SDK), the NPC or another instrument (needle hand-off).
* Needle-passing task with four rings, patient state machine, tissue contact zone and haptic feedback.
* NPC instructor rig with a manual (non-autonomous) controller interface.
* Three observation cameras (trainee POV, NPC POV, overhead) with on-demand low-rate feeds and an in-theatre monitor.
* Session recording: 30 Hz trajectories of every instrument plus an event log, exported as JSON.
* Editor tools under *Tools → SurgiTwin XR*: build the demo theatre, validate the scene, capture reference screenshots, apply Quest render settings.

**Fixed during headset validation (Quest 3S)**
* Exporting a session no longer drops frames. The JSON is now written on a background thread.
* The trajectory buffer now holds 30 minutes for any number of instruments. It used to fill after about 13 minutes with 5 instruments.
* A needle left in the training pad no longer makes the patient state flicker every frame. It used to flood the event log.
* The event log now warns when its cap is reached and reports the number of dropped events (`eventsDropped`).
* Session files are also copied to the headset's shared `Download/SurgiTwinLogs/` folder, so they can be retrieved over USB from any Android user account.
