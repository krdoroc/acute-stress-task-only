# Eye-tracking implementation plan

## Chunk 1 — establish contracts and configuration

1. Add `GazeTarget`, `GazeHit`, `GazeRaySource`, `IGazeHitClassifier`, and `IGazeDataSink` types in a task-owned eye-tracking folder.
2. Give each target a stable inspector ID, falling back visibly to its hierarchy path during migration.
3. Update the calibration prefab and runtime defaults to nine points and one `C` hotkey path.

**Verification:** compile/static review; ensure target and sink code has no `GameManager` dependency.

## Chunk 2 — correct per-sample hit classification

1. Build a screen gaze ray from each dequeued sample: binocular average, then monocular fallback.
2. Query 2D colliders first and legacy 3D colliders second.
3. Resolve stable `GazeTarget` metadata and serialize the hit for that exact sample.
4. Add schema/session/scene/trial fields and ray-source validity to XML.

**Verification:** pure coordinate helper tests where possible; inspect XML attribute contract; confirm `ScreenBasedSaveData` no longer reads `GazeTrail.LatestHitObject`.

## Chunk 3 — local output plus DHive-ready seam

1. Keep local XML enabled by default but make it an explicit inspector option.
2. Use/create a writable persistent output directory, with an optional legacy relative folder.
3. Publish every immutable sample record to additional `MonoBehaviour` sinks implementing `IGazeDataSink`; a future DHive component can implement this interface.
4. Isolate sink failures so local recording can continue and report errors clearly.

**Verification:** directory/path review, local-off path review, and lifecycle close/flush checks.

## Chunk 4 — safe calibration feedback and task pause

1. Consolidate all calibration terminal paths into cleanup and callback behavior.
2. Temporarily pause task time/input, use realtime waits, and restore prior state after success or Ignore.
3. On failure, keep the task paused and show Retry / Ignore and continue. Record calibration outcome and operator decision.
4. Make the feedback UI self-contained so existing binary scenes do not need manual UI wiring.

**Verification:** reason through success, compute failure, worker-start failure, retry, ignore, disable, and already-running paths. Hardware validation remains pending.

## Chunk 5 — documentation and verification

1. Expand README with essential/custom/vendor files, setup, nine-point calibration flow, target setup, output schema, local-only/DHive extension, and device-free limitations.
2. Run repository static checks and `git diff --check`.
3. Record Unity-editor/manual checks that cannot be performed here: Force Text reserialization, target collider overlay review at deployment resolution, simulated gaze play-mode test, and Tobii Pro 300 Hz hardware soak test.

## Explicitly deferred

- A concrete DHive transport, authentication, retry, and session protocol, pending the user’s DHive instructions.
- Automated mutation of binary scene/resource files.
- Changing the legacy XML container format or adding background I/O before downstream compatibility and loss behavior are agreed.
