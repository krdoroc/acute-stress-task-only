# Collider alignment summary

## The core issue

Unity determines a gaze hit by testing the gaze coordinate against an invisible collider. It does not inspect the visible pixels of an item.

The hit-detection algorithm can correctly answer whether gaze intersected a collider, but it cannot determine whether that collider accurately covers the item participants see. If a collider is too small, too large, or offset, Unity can produce a technically correct collision result that is experimentally wrong.

Examples:

- A collider that is too small records valid gaze on the visible item as no hit.
- An offset collider records gaze on the item as no hit and may record gaze on empty space as a hit.
- An oversized collider may incorrectly attribute gaze on a neighbouring item or empty space.

## Why this task is vulnerable

- Item value and weight regions are scaled dynamically by `BoardManager`.
- Board layout assumes an 800×600 area, while gaze conversion uses the actual runtime screen size.
- Items are displayed on a Unity Canvas, but existing placement and hit testing involve world, camera, UI, and physics coordinate systems.
- Alignment can change with Canvas settings, camera projection, screen resolution, aspect ratio, parent transforms, and object scale.
- The original hit pipeline also queried 3D physics for task items using 2D colliders. That code issue has been corrected, but collider geometry still requires visual validation.

## How to check alignment

1. Open the item prefab, currently `KSItem3`, and inspect the `Bill`, `Weight`, and related child objects.
2. Select each `BoxCollider2D` and confirm its green outline covers the meaningful visible region without extending into another region.
3. Run a Trial scene in Play mode because items are positioned and resized dynamically.
4. Enable Gizmos and Unity’s 2D physics visualization so collider outlines are visible.
5. Set the Game view to the exact resolution and aspect ratio intended for data collection. Do not validate only with Free Aspect.
6. Inspect several generated items of different sizes and positions.

## Recommended device-free test

Use the mouse as a synthetic gaze coordinate and pass it through the same classifier used for Tobii samples. Display the simulated gaze point and detected stable target ID.

Test:

- The centre and edges of every value region
- The centre and edges of every weight region
- The boundary between value and weight
- Points immediately outside each item
- Gaps between neighbouring items

Expected results:

- Gaze inside a value region returns an ID such as `item_3_value`.
- Gaze inside its weight region returns `item_3_weight`.
- Gaze outside meaningful item regions returns no hit.

A debug overlay can also draw target collider bounds and stable IDs directly over the Game view. It should be disabled for participant-facing production builds.

## Fixing misalignment

For sprite-based targets, correct the `BoxCollider2D` size and offset, and ensure it receives the same dynamic transform and scale as the visual.

For screen-space Canvas targets, a more robust solution may be to test gaze screen coordinates directly against each target's `RectTransform` using Unity UI coordinate utilities. This avoids several camera, depth, and 2D-physics alignment problems and is well suited to rectangular value and weight panels.

## Pre-collection validation checklist

- Every important target has a unique, stable ID.
- Its collider covers the full meaningful visual region.
- Empty space outside the visual does not produce a hit.
- Adjacent target colliders do not unintentionally overlap.
- Alignment is correct after dynamic placement and scaling.
- Alignment is correct at the deployment resolution, aspect ratio, and full-screen configuration.
- Mouse-simulated hits match the visible display.
- A later Tobii hardware test produces target IDs consistent with the live gaze overlay.
- Saved records contain the same target ID shown by the debugger.

In short, the scientific area of interest and Unity's invisible hit area must describe the same screen region. Correct collision code alone cannot guarantee that alignment.
