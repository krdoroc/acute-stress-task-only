# Eye-tracking discovery issues

This list is ordered from small/foundational changes to changes that depend on them. Line numbers describe the repository as discovered; they may move during implementation.

## 1. The documented calibration profile does not match the study

- The study requires nine calibration points, but `[Calibration].prefab` serializes five (`Assets/TobiiPro/ScreenBased/Prefabs/[Calibration].prefab:142-147`).
- The prefab hotkey is `KeyCode.None` (`:141`) even though the README says `C` starts calibration.
- `Calibration.Update` calls `StartCalibration` twice for one key press (`Assets/TobiiPro/ScreenBased/Scripts/Utility/Calibration.cs:249-258`); the second call can only report "already performing calibration."

**Fix first:** define a validated nine-point default, make `C` the fallback operator key, and make one start call.

## 2. Output paths are fragile and local saving is not an explicit mode

- Writers target `Application.dataPath + /DATAinf/Output/` (`Assets/ScreenBasedSaveData.cs:139`, `Assets/Scripts/GameManager.cs:104,314,348,365`). Builds may not be writable.
- Directory creation is commented out (`Assets/ScreenBasedSaveData.cs:121-125`), requiring manual setup described in `README.md:12-25`.
- `ScreenBasedSaveData` owns an `XmlWriter` directly and has no output interface (`Assets/ScreenBasedSaveData.cs:58-59,128-139`).

**Fix:** default gaze output to `Application.persistentDataPath`, create directories, retain an explicit local-only switch, and expose a sink interface so a DHive adapter can be added later without changing acquisition/hit code.

## 3. Sample metadata and hit identity are ambiguous

- Hit identity is a mutable Unity object name or the magic value `Nothing` (`Assets/ScreenBasedSaveData.cs:174-175`). Names are not stable or necessarily unique.
- Participant, scene, block and trial are mostly encoded in filenames; trial attributes are commented out (`:172,193-196`).
- The supplied XML/CSV contains hit-position fields that no checked-in writer emits (`Example/P014006_18 November, 2022, 17-06_End__Trial_8.xml:3`, `Example/P014006_Trial_8.csv:1`), so the example schema is not reproducible from this commit.

**Fix:** introduce a gaze target marker with a stable ID and record schema/session/scene/block/trial plus explicit hit validity on every sample. Treat the Example conversion as legacy until its converter is supplied.

## 4. The hit test uses the wrong physics system

- Task layout reads `BoxCollider2D` from item visuals (`Assets/Scripts/BoardManager.cs:254-257`).
- Gaze hit testing uses `Physics.Raycast`, which only queries 3D colliders (`Assets/TobiiPro/Common/Scripts/GazeTrailBase.cs:160`).

This directly produces false `Nothing` values.

**Fix:** classify screen-based gaze with `Physics2D.GetRayIntersection`, with a 3D fallback only for legacy targets. Resolve the stable target marker from the hit object or its parent.

## 5. Hit classification is not associated with the gaze sample that produced it

- `GazeTrail` asks for `LatestGazeData` once per Unity frame (`Assets/TobiiPro/ScreenBased/Scripts/Utility/GazeTrail.cs:40-42`) and stores one mutable latest hit (`Assets/TobiiPro/Common/Scripts/GazeTrailBase.cs:98,156-167`).
- `ScreenBasedSaveData` drains all queued 300 Hz samples, then labels all of them with that one frame-level hit (`Assets/ScreenBasedSaveData.cs:102-106,174-175`). Script update order can also make the label one frame old.

**Fix:** run a pure classifier for each dequeued `IGazeData` immediately before writing its record. Keep `GazeTrail` visualization-only.

## 6. Valid monocular samples are discarded

- A combined ray is valid only when both eyes are valid (`Assets/TobiiPro/ScreenBased/Scripts/Data/GazeData.cs:21-35`). One temporarily invalid eye turns a usable sample into a miss.

**Fix:** average two valid gaze points, fall back to the valid eye, and record whether the ray was binocular, left-only, right-only, or invalid.

## 7. Failed calibration is logged but not actionable

- Calibration completion only saves a timestamp (`Calibration.cs:249-252`); there is no failure UI.
- Failure to start the worker exits without invoking the callback (`:163-169`), while enter/leave statuses are ignored (`:174-177,208-212`).
- Disable/cancellation does not fully restore UI/state (`:232-242`).

**Fix:** centralize completion and cleanup, show a blocking Retry / Ignore and continue prompt, and log both the result and operator choice.

## 8. Pause/recalibration does not preserve the task

- Pause input is checked only on rest scenes (`Assets/Scripts/GameManager.cs:257-262`).
- Unpause calls `errorInScene("Pause")`, saves an error, and advances the trial (`:276-280,838-845`).
- Calibration uses scaled waits (`Calibration.cs:124,160,187`) and its point animation uses scaled `Time.time` (`CalibrationPoint.cs:36,45`), so calibration can stall at `Time.timeScale == 0`.
- Calibration overlay does not stop `GameManager.startTimer()` (`GameManager.cs:252-255`) or task keyboard input.

**Fix:** let calibration own a reversible task pause, use realtime waits/unscaled animation, restore the exact previous time scale/input state, and never treat operator pause as a trial error.

## 9. Collider/layout accuracy is not verifiable

- Layout assumes 800x600 (`Assets/Scripts/BoardManager.cs:17-18`) while gaze uses runtime `Screen.width/height` (`GazeData.cs:24,74`).
- Items are positioned with world `Transform.position`, while gaze is mapped through `Camera.main`; Canvas scaling/aspect ratio can desynchronize pixels, visuals, and colliders.
- Task scenes and resource prefabs are binary, preventing review of Canvas modes, layers and collider bounds.

**Next editor task:** set Asset Serialization to Force Text, verify at target resolution, and add an editor/runtime validator that compares each gaze target collider bounds with its rendered bounds. This cannot be safely completed from the current binary assets without opening Unity.

## 10. Acquisition can silently lose samples

- Processing is capped at 20 events per pass (`Assets/TobiiPro/ScreenBased/Scripts/EyeTracker.cs:133-150`).
- Queues cap at 130 and silently evict the oldest entry (`Assets/TobiiPro/VR/Scripts/Utility/VRUtility.cs:269-272`), only about 0.43 seconds at 300 Hz.
- XML is indented and synchronously written on the Unity main thread, with flush only on close (`Assets/ScreenBasedSaveData.cs:128-158`).

**Later performance fix:** add drop counters, bounded background batching, periodic flush, and a crash-tolerant format/sink. Validate this with a synthetic 300 Hz source before changing the study’s canonical format.

## 11. Portability and testing documentation are missing

- The README only points to broad Tobii folders and the saver (`README.md:128-130`). It does not explain required components, coordinate conventions, target IDs, output schema, no-device development, or vendor-versus-custom boundaries.
- There are no project-owned test assemblies or synthetic gaze fixtures.

**Fix throughout:** document architecture and porting, keep task dependencies behind interfaces, and add pure/testable coordinate and classification helpers. Hardware validation remains required before data collection.
