# Eye-tracking architecture

This document describes how experiment inputs become named gaze targets, how Tobii Pro samples enter the Unity application, and how classified samples are written to XML or optional output adapters.

## End-to-end overview

```mermaid
flowchart LR
    Input["Experiment input files"] --> GM["GameManager"]
    GM --> BM["BoardManager"]
    BM --> Item["Generated Knapsack item"]
    Item --> Collider["Collider2D"]
    Item --> Target["GazeTarget<br/>item_2_weight"]

    Device["Tobii device"] --> SDK["Tobii Research SDK"]
    SDK --> Tracker["EyeTracker"]
    Tracker --> Sample["IGazeData sample"]
    Sample --> Classifier["Gaze hit classifier"]
    Classifier --> Collider
    Collider --> Target
    Target --> Record["GazeSampleRecord"]
    Record --> XML["Local XML"]
    Record --> Sink["Optional additional sink<br/>for example, a future DHive adapter"]
```

The input side creates and names targets. The Tobii side produces gaze samples. The classifier connects the two.

## 1. Tobii Pro setup and application interface

The Tobii-provided integration is principally under `Assets/TobiiPro`. The custom runtime bootstrap ensures the required services exist before the first Unity scene loads.

### Startup sequence

```mermaid
sequenceDiagram
    autonumber
    participant Unity
    participant Bootstrap as EyeTrackingRuntimeBootstrap
    participant Runtime as EyeTrackingRuntime
    participant Tracker as EyeTracker
    participant Base as EyeTrackerBase
    participant SDK as Tobii Research SDK
    participant Device as Tobii device

    Unity->>Bootstrap: BeforeSceneLoad
    Bootstrap->>Runtime: Find or create persistent GameObject
    Bootstrap->>Runtime: DontDestroyOnLoad

    Bootstrap->>Runtime: Add ExperimentPauseService
    Bootstrap->>Runtime: Add EyeTracker
    Bootstrap->>Runtime: Add Calibration
    Bootstrap->>Runtime: Add collider debug overlay

    Tracker->>Base: Start
    Base->>Base: Check mouse simulation setting

    alt Mouse simulation enabled
        Base-->>Tracker: Do not start native Tobii connection
    else Hardware mode on Windows
        Base->>SDK: Start device discovery
        loop Until tracker found
            SDK->>Device: FindAllEyeTrackers
            Device-->>SDK: Device details
        end
        SDK-->>Tracker: Connected IEyeTracker
        Tracker->>SDK: Subscribe to GazeDataReceived
    end
```

The runtime is created in [`EyeTrackingRuntimeBootstrap.cs`](Assets/EyeTracking/EyeTrackingRuntimeBootstrap.cs). The device lifecycle is implemented in [`EyeTrackerBase.cs`](Assets/TobiiPro/Common/Scripts/EyeTrackerBase.cs) and [`EyeTracker.cs`](Assets/TobiiPro/ScreenBased/Scripts/EyeTracker.cs).

### Gaze-sample sequence

```mermaid
sequenceDiagram
    autonumber
    participant Device as Tobii device
    participant SDK as Tobii SDK
    participant Callback as GazeDataReceivedCallback
    participant RawQueue as Raw event queue
    participant Tracker as EyeTracker
    participant DataQueue as IGazeData queue
    participant Saver as ScreenBasedSaveData

    Device->>SDK: New eye measurement
    SDK->>Callback: GazeDataReceived(eventArgs)
    Callback->>RawQueue: Enqueue raw event

    loop Every Unity update
        Tracker->>RawQueue: Read pending event
        Tracker->>Tracker: new GazeData(eventArgs)
        Tracker->>DataQueue: Enqueue IGazeData
    end

    loop While samples remain
        Saver->>Tracker: NextData
        Tracker-->>Saver: One IGazeData sample
        Saver->>Saver: Classify and save exact sample
    end
```

`IGazeData` is the common format understood by the remainder of the application. It carries left-eye and right-eye data, validity flags, normalized display coordinates, the Tobii timestamp, and the original raw event. `MouseGazeData` implements the same interface, so simulated and hardware input use the same classifier and writer.

Calibration is a parallel control path: `Ctrl+Shift+C` pauses task input, runs calibration, records the outcome, and resumes the task. A failed calibration offers **Re-calibrate** and **Ignore and continue**.

## 2. Vendor, adapted, and custom logic

```mermaid
flowchart TB
    subgraph Vendor["Tobii-provided foundation"]
        EyeTrackerBase["EyeTrackerBase<br/>device lifecycle"]
        EyeTracker["EyeTracker<br/>sample queues"]
        GazeData["GazeData and IGazeData<br/>sample representation"]
        CalibrationOriginal["Calibration foundation"]
        GazeTrail["GazeTrail<br/>visualization only"]
    end

    subgraph Adapted["Tobii-origin files adapted for this experiment"]
        Calibration["Calibration<br/>global pause and failure prompt"]
        SaveData["ScreenBasedSaveData<br/>classification and output"]
        BaseChanges["EyeTrackerBase<br/>singleton and platform handling"]
    end

    subgraph Custom["Custom eye-tracking layer"]
        Bootstrap["EyeTrackingRuntimeBootstrap"]
        Pause["ExperimentPauseService"]
        Settings["EyeTrackingSettings"]
        Mouse["MouseGazeData"]
        Target["GazeTarget"]
        Pipeline["GazePipeline"]
        Paths["StudyDataPaths"]
        Overlay["GazeColliderDebugOverlay"]
    end

    subgraph Study["Existing experiment code adapted for gaze tracking"]
        GameManager["GameManager"]
        BoardManager["BoardManager"]
    end

    Vendor --> Adapted
    Adapted --> Custom
    Study --> Custom
```

There is no `GazeItem` class in this project. The component is called `GazeTarget`, defined in [`GazeTarget.cs`](Assets/EyeTracking/GazeTarget.cs). It is custom experiment code, not part of the Tobii SDK.

```mermaid
flowchart LR
    Tobii["Tobii knows:<br/>the participant looked at screen coordinate x,y"]
    Custom["Custom code knows:<br/>coordinate x,y intersects item_2_weight"]
    Experiment["Experiment knows:<br/>item_2_weight belongs to a Knapsack item"]

    Tobii --> Custom --> Experiment
```

Tobii provides measurements. It cannot infer that a screen region represents a weight, value, answer button, or other experimental area of interest. The application supplies those semantics.

`GazeTrail` is for visualization only. It is not the source of saved target identities.

## 3. Turning an object into a gaze target

A gaze target requires a visible object, physics geometry, and a semantic identity.

```mermaid
flowchart LR
    Visual["Visible GameObject<br/>sprite or UI element"]
    Physics["Collider2D or Collider<br/>physical boundary"]
    Identity["GazeTarget component<br/>stable target ID"]

    Visual --> Complete["Complete gaze target"]
    Physics --> Complete
    Identity --> Complete

    Complete --> Example["Example:<br/>item_2_weight"]
```

A sprite by itself cannot be hit by a physics ray. A collider can be hit, but it does not explain what the collider means. `GazeTarget` supplies that meaning.

### Target-configuration control flow

```mermaid
flowchart TD
    Start["ConfigureGazeTarget<br/>(colliderOwner, targetId)"] --> Owner{"Does the GameObject exist?"}

    Owner -- No --> Missing["Log error:<br/>collider owner missing"]
    Owner -- Yes --> Count["Count Collider2D and Collider components"]

    Count --> HasCollider{"How many colliders?"}
    HasCollider -- "0" --> None["Log error:<br/>object has no collider"]
    HasCollider -- "More than 1" --> Ambiguous["Log error:<br/>AOI collider is ambiguous"]
    HasCollider -- "Exactly 1" --> Existing{"Already has GazeTarget?"}

    Existing -- No --> Add["Add GazeTarget component"]
    Existing -- Yes --> Reuse["Reuse existing component"]

    Add --> Configure["Configure stable target ID"]
    Reuse --> Configure
    Configure --> Ready["Collider resolves to canonical ID"]
```

The dynamic configuration helper is in [`BoardManager.cs`](Assets/Scripts/BoardManager.cs). For a static scene object, the equivalent editor workflow is:

```mermaid
flowchart LR
    Select["Select GameObject"] --> AddCollider["Add and size collider"]
    AddCollider --> AddTarget["Add GazeTarget component"]
    AddTarget --> EnterID["Enter stable target ID"]
    EnterID --> Verify["Enable collider overlay<br/>and verify bounds"]
```

Target IDs should describe stable experimental meaning:

- Good: `item_2_weight`
- Good: `item_2_value`
- Good: `answer_yes`
- Avoid: `Rectangle(Clone)`
- Avoid: `$25`
- Avoid: `MainCanvas/Panel/Image`

Display text and hierarchy names can change. The analysis identity should remain stable.

## 4. Knapsack item naming

Each generated Knapsack item begins with four sibling collider objects in the item prefab.

```mermaid
flowchart TB
    Root["KSItem3 prefab"]
    Bill["Bill<br/>BoxCollider2D"]
    Weight["Weight<br/>BoxCollider2D"]
    TopHanger["TopHanger<br/>BoxCollider2D"]
    Hanger["Hanger<br/>BoxCollider2D"]

    Root --> Bill
    Root --> Weight
    Root --> TopHanger
    Root --> Hanger
```

### Generation and naming sequence

```mermaid
sequenceDiagram
    autonumber
    participant Trial as Trial setup
    participant Board as BoardManager
    participant Prefab as KSItem3 prefab
    participant Item as Generated item
    participant Helper as ConfigureGazeTarget

    Trial->>Board: generateItem(itemNumber = 2)
    Board->>Prefab: Instantiate
    Prefab-->>Board: New GameObject

    Board->>Item: Rename root to KP Item (2)
    Board->>Item: Find Bill
    Board->>Item: Find Weight
    Board->>Item: Find TopHanger
    Board->>Item: Find Hanger

    Board->>Item: Set Bill text from values[2]
    Board->>Item: Set Weight text from weights[2]

    Board->>Item: Rename TopHanger to KP BILL (2)
    Board->>Item: Rename Hanger to KP WEIGHT (2)

    Board->>Helper: Bill, item_2_value
    Board->>Helper: KP BILL (2), item_2_value
    Board->>Helper: Weight, item_2_weight
    Board->>Helper: KP WEIGHT (2), item_2_weight
```

The resulting mapping is:

```mermaid
flowchart TB
    Item["KP Item (2)"]

    Bill["Unity name: Bill<br/>Target ID: item_2_value"]
    Top["Unity name: KP BILL (2)<br/>Target ID: item_2_value"]
    Weight["Unity name: Weight<br/>Target ID: item_2_weight"]
    Hanger["Unity name: KP WEIGHT (2)<br/>Target ID: item_2_weight"]

    Item --> Bill
    Item --> Top
    Item --> Weight
    Item --> Hanger

    Bill -. "same analysis identity" .- Top
    Weight -. "same analysis identity" .- Hanger
```

Object names and canonical IDs serve different purposes:

```mermaid
flowchart LR
    UnityName["Unity object name<br/>KP WEIGHT (2)"]
    TargetID["Scientific target ID<br/>item_2_weight"]

    UnityName --> Diagnostic["XML HitObject<br/>developer diagnostic"]
    TargetID --> Analysis["XML HitTargetId<br/>analysis identity"]
```

The object name helps developers inspect the Unity hierarchy. The canonical ID is intended for analysis.

The target names are not randomized. `item_2_weight` means item index 2 inside the selected problem arrays. Its screen position can be randomized, but its identity remains `item_2_weight`.

## 5. Gaze classification, data sinks, and output paths

### Hit-classification control flow

```mermaid
flowchart TD
    Sample["One IGazeData sample"] --> ValidEyes{"Which gaze data is valid?"}

    ValidEyes -- "Both eyes" --> Average["Average left and right coordinates"]
    ValidEyes -- "Left only" --> Left["Use left-eye coordinate"]
    ValidEyes -- "Right only" --> Right["Use right-eye coordinate"]
    ValidEyes -- "Neither" --> NoRay["Return no hit"]
    ValidEyes -- "Mouse simulation" --> Mouse["Use mouse screen position"]

    Average --> Ray["Camera.ScreenPointToRay"]
    Left --> Ray
    Right --> Ray
    Mouse --> Ray

    Ray --> Ray2D["Physics2D.GetRayIntersection"]
    Ray2D --> Hit2D{"2D collider hit?"}

    Hit2D -- Yes --> Resolve["Get GazeTarget from collider or parent"]
    Hit2D -- No --> Ray3D["Physics.Raycast"]
    Ray3D --> Hit3D{"3D collider hit?"}

    Hit3D -- Yes --> Resolve
    Hit3D -- No --> NoHit["GazeHit.None"]

    Resolve --> HasTarget{"GazeTarget found?"}
    HasTarget -- Yes --> Canonical["Use stable TargetId"]
    HasTarget -- No --> Fallback["Current fallback:<br/>use hierarchy path"]

    Canonical --> Result["Create GazeHit"]
    Fallback --> Result
```

The classifier is defined in [`GazePipeline.cs`](Assets/EyeTracking/GazePipeline.cs).

### Sample-to-sink sequence

```mermaid
sequenceDiagram
    autonumber
    participant Tracker as EyeTracker
    participant Saver as ScreenBasedSaveData
    participant Classifier as GazeHitClassifier
    participant Physics as Unity Physics
    participant Target as GazeTarget
    participant Record as GazeSampleRecord
    participant Local as Local XML writer
    participant Extra as Additional IGazeDataSink

    Saver->>Tracker: NextData
    Tracker-->>Saver: IGazeData sample

    Saver->>Classifier: Classify(sample)
    Classifier->>Classifier: Create screen ray
    Classifier->>Physics: Raycast

    alt Collider hit
        Physics-->>Classifier: Collider, hit point, bounds
        Classifier->>Target: GetComponentInParent
        Target-->>Classifier: TargetId
        Classifier-->>Saver: GazeHit
    else No collider
        Physics-->>Classifier: No hit
        Classifier-->>Saver: GazeHit.None
    end

    Saver->>Record: Combine gaze, hit, participant, scene, and trial

    opt Save Locally enabled
        Saver->>Local: WriteLocalRecord(record)
        Local->>Local: Write GazeData XML element
    end

    opt Additional sinks configured
        Saver->>Extra: Write(record)
    end
```

A sink is a destination that consumes the complete record.

```mermaid
flowchart LR
    Record["GazeSampleRecord"]

    Record --> Local["Built-in local XML writer"]
    Record --> Interface["IGazeDataSink interface"]

    Interface --> FutureDHive["Future DHive adapter"]
    Interface --> Other["Other future storage adapter"]

    Local --> XML["XML file on this computer"]
```

`IGazeDataSink` defines `Write(GazeSampleRecord record)` and `Close()`. No DHive transport is currently implemented; the interface is its extension point.

### XML field lineage

```mermaid
flowchart LR
    subgraph Sources["Record sources"]
        Gaze["IGazeData"]
        Hit["GazeHit"]
        Context["GameManager context"]
    end

    subgraph XMLFields["One GazeData XML element"]
        Timestamp["TimeStamp and SystemTimeUtc"]
        Eyes["Left, Right, and raw gaze"]
        RaySource["GazeRaySource"]
        HitFlag["Hit"]
        TargetID["HitTargetId"]
        Object["HitObject"]
        Point["HitPoint"]
        Bounds["HitBoundsMin and HitBoundsMax"]
        Trial["ParticipantId, Scene, Block, and Trial"]
    end

    Gaze --> Timestamp
    Gaze --> Eyes
    Gaze --> RaySource

    Hit --> HitFlag
    Hit --> TargetID
    Hit --> Object
    Hit --> Point
    Hit --> Bounds

    Context --> Trial
```

Example mapped hit:

```xml
<GazeData
  Hit="True"
  HitTargetId="item_2_weight"
  HitObject="KP WEIGHT (2)"
  HitPoint="(...)"
  HitBoundsMin="(...)"
  HitBoundsMax="(...)" />
```

`HitTargetId` is the analysis identity. `HitObject` is diagnostic context. XML creation and sink fan-out are handled by [`ScreenBasedSaveData.cs`](Assets/ScreenBasedSaveData.cs).

### Output path

```mermaid
flowchart TD
    Persistent["Application.persistentDataPath"]
    Study["StudyData"]
    Session["Session directory<br/>participant + date + Dec"]
    TaskFiles["TrialInfo.txt<br/>TimeStamps.txt<br/>InstancesInfo.txt"]
    EyeDir["EyeTracking"]
    XML["participant_scene_trial.xml"]

    Persistent --> Study
    Study --> Session
    Session --> TaskFiles
    Session --> EyeDir
    EyeDir --> XML
```

```text
Application.persistentDataPath/
└── StudyData/
    └── <session>/
        ├── TrialInfo.txt
        ├── TimeStamps.txt
        ├── InstancesInfo.txt
        └── EyeTracking/
            └── <identifier>_<scene>_<trial>.xml
```

`Application.persistentDataPath` is selected by Unity and differs by operating system. Path construction is centralized in [`StudyDataPaths.cs`](Assets/EyeTracking/StudyDataPaths.cs). Generated data is no longer written to `Assets/DATAinf/Output`.

## 6. Input loading and randomization

Inputs are read during the `SetUp` scene.

### Input-loading sequence

```mermaid
sequenceDiagram
    autonumber
    participant Setup as SetUp scene
    participant GM as GameManager
    participant Params as Parameter files
    participant Dict as Parameter dictionary
    participant Instances as KPInstances files
    participant Array as KSInstance array
    participant Board as BoardManager

    Setup->>GM: Awake and InitGame

    GM->>Params: Read layoutParam.txt
    Params-->>Dict: name:value entries

    GM->>Params: Read param.txt
    Params-->>Dict: name:value entries

    GM->>Params: Read param2.txt
    Params-->>Dict: name:value entries

    GM->>GM: assignVariables(dictionary)

    loop i1.txt through iN.txt
        GM->>Instances: Read instance file
        Instances-->>GM: weights, values, capacity, profit, solution, ID, and type
        GM->>Array: Store KSInstance
    end

    GM->>GM: Read instanceRandomization from parameters
    GM->>GM: Generate balanced button randomization
    GM->>Board: Set up participant-ID screen
```

The loaders are in [`GameManager.cs`](Assets/Scripts/GameManager.cs).

### Input structure

```mermaid
flowchart TB
    Root["Application.dataPath/DATAinf/Input"]

    Layout["layoutParam.txt<br/>grid and item-area settings"]
    Param["param.txt<br/>timings and experiment settings"]
    Param2["param2.txt<br/>trial counts and instance order"]
    KP["KPInstances"]
    I1["i1.txt"]
    I2["i2.txt"]
    IN["iN.txt"]

    Root --> Layout
    Root --> Param
    Root --> Param2
    Root --> KP
    KP --> I1
    KP --> I2
    KP --> IN
```

### Instance selection

```mermaid
flowchart TD
    Input["instanceRandomization:[3,1,2]"] --> Parse["Parse input integers"]
    Parse --> Convert["Subtract 1 for C# array indexing"]
    Convert --> Internal["Internal array: [2,0,1]"]

    Internal --> T1["Trial 1 selects ksinstances[2]<br/>which came from i3.txt"]
    Internal --> T2["Trial 2 selects ksinstances[0]<br/>which came from i1.txt"]
    Internal --> T3["Trial 3 selects ksinstances[1]<br/>which came from i2.txt"]
```

Despite its name, `instanceRandomization` is currently loaded from an input file. The call that would generate it randomly is commented out. The unused generator can also repeat instances, which is noted in the source.

### Runtime randomization

```mermaid
flowchart LR
    Setup["Experiment setup"]

    Setup --> InstanceOrder["Instance order<br/>supplied by param2.txt"]
    Setup --> Buttons["Yes/No sides<br/>balanced and shuffled"]
    Setup --> Positions["Item screen positions<br/>random valid grid cells"]
    Setup --> Rest["Rest duration<br/>random minimum to maximum"]
    Setup --> Likert["Likert starting value<br/>random"]

    InstanceOrder --> NotRuntime["Not currently generated randomly by the app"]
    Buttons --> Runtime["Randomized at runtime"]
    Positions --> Runtime
    Rest --> Runtime
    Likert --> Runtime
```

### From selected input to target names

```mermaid
flowchart TD
    InstanceFile["Selected instance file"]
    Arrays["weights[] and values[]"]
    Loop["BoardManager loops i = 0 to item count - 1"]
    Generate["generateItem(i)"]
    Value["item_i_value"]
    Weight["item_i_weight"]
    Position["RandomPosition()"]

    InstanceFile --> Arrays
    Arrays --> Loop
    Loop --> Generate

    Generate --> Value
    Generate --> Weight
    Generate --> Position

    Position --> Screen["Random location on screen"]
    Value --> Stable["Stable identity"]
    Weight --> Stable
```

The central rule is:

```text
Selected problem → defines the item arrays
Array index      → defines item_N_value and item_N_weight
Random placement → changes only where that named item appears
```

## Current classification limitations

The architecture above documents both the intended design and the current transitional behaviour:

1. A collider without a `GazeTarget` still produces a hierarchy-path fallback in `HitTargetId`.
2. The gaze layer mask currently includes every Unity physics layer.
3. A dedicated `GazeTarget` layer and strict canonical-ID-only output are planned but not yet implemented.
4. The local saver is currently scene-wired rather than part of the persistent runtime in every task-relevant scene.

These limitations should be removed before treating all `HitTargetId` values as canonical experimental identities.
