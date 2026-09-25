# Habitat Shift — Unity v4 Runtime

Open `Assets/Scenes/SampleScene.unity` in Unity 6000.6.2f1 and press Play. The bootstrap loads the immutable production catalog from `Assets/StreamingAssets/HabitatShift/` and fails closed when its SHA-256 identity does not match the supplied v4 handoff.

The runtime uses continuous pointer motion, float board coordinates, queue-based Elevators, movement constraints, Undo, Restart, v2 save migration, per-level assist charges and the Home/Level Select/Gameplay/Pause/Result/Settings flow. The level catalog must never be edited in place; replace it only through an approved handoff with matching validation updates.

Run `Habitat Shift > Validate Production Catalog v4` in Unity to verify the catalog before play. The existing Android target remains portrait and ARM64; install Unity Android Build Support before building an AAB.
