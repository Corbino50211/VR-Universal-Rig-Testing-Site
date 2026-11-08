# Universal Rig: first implementation

Target: Unity **6000.6.5f1**, URP **17.6.0**, Input System **1.20.0**, OpenXR **1.18.0**, PCVR first. The repository also contains XRI **3.6.1**; the new rig owns its movement/grabbing directly and does not need an XRI locomotion stack.

The branch also preserves the user's newer `40905d3e28a1f57c0be4871df1a8ce421a2d174a` commit: updated test map/materials, ProBuilder, URP resources and weapon prefabs under `Assets/Weapons/Prefabs`. Inspect those authored prefabs before preparing weapon variants.

The initially inspected base is `fa6a79a9bbbb622137c816dfc5b13855b93e8541`. It contains no existing player rig or BIMOS scripts. This is an original foundation, not Stress Level Zero source code or a finished BONEWORKS-equivalent rig.

## Quick start in Unity

1. Checkout `codex/rig-foundation` with all Git LFS objects present. Keep local work before switching. The soldier model is approximately 115 MB and cannot be replaced by its 134-byte LFS pointer.
2. Open the project with its recorded editor version. Wait for package import and compilation; fix any errors before invoking the builder.
3. Run **Tools > Universal Rig > Create Soldier Test Scene**. This saves existing scene edits through Unity's normal save prompt, opens a new scene, allocates an unused `UniversalRigPhysics` user layer, and creates unique assets under `Assets/UniversalRig/Generated`. It does not overwrite existing scenes, models, settings profiles, or generated prefabs.
4. Run **Tools > Universal Rig > Set PCVR Physics to 90 Hz**. This explicitly changes the project timestep and maximum allowed timestep; it does not force headset refresh rate or guarantee performance.
5. Run **Tools > Universal Rig > Validate Active Rig**, then the `UniversalRig.EditModeTests` tests in Test Runner.
6. Verify OpenXR Standalone startup and the actual controller's interaction profile. Use a running PCVR runtime and a connected headset before testing tracked input.
7. Enter Play Mode. Stand upright for initial calibration. Move with left stick, turn with right stick, jump with right primary button, recalibrate with left primary button while standing still, and hold/release grip to grab/drop. Trigger values are exposed for the next weapon milestone but do not fire anything yet.

## What the code does

| Component | Responsibility |
|---|---|
| `RigInput` | Owns tracked transforms, reads a serialized action asset, requests floor origin, falls back to a configurable device height, buffers button presses until physics consumes them. |
| `PhysicsRig` | Dynamic upright capsule; bounded acceleration; head-relative walking; smooth or snap turning; real crouching; ceiling-tested expansion; ground slope checks; jumping; standing calibration. |
| `PhysicsHand` | Dynamic collider follows controller targets with bounded force and inertia-aware torque. Tracking loss releases grips and damps velocity. It never snaps a blocked hand through a wall to catch up. |
| `PhysicsGrabbable` | One/two-hand ownership and cleanup. First hand controls orientation; second controls position; remaining hand becomes primary when the first releases. |
| `AvatarPoseDriver` | Initial Humanoid visual IK with preserved limb lengths and configurable wrist offsets. No physical limb joints yet. |
| `HeadSafetyFade` | Black overlay on head obstruction, tracking loss, or excessive horizontal head/body separation; must be checked in both eyes in PCVR. |
| `RigCollisionFilter` | Ignores collision pairs within this local rig without changing the global collision matrix. |
| `RigBuilder` / `RigValidator` | Generates the test room/prefab/action asset/settings and checks scene references. |

The root remains a stationary container. Tracking Space, Physics Body, both hands, and the soldier are siblings underneath it. Do not parent Tracking Space or the hands to the moving Rigidbody. The capsule origin is at the feet. Root and physics transforms use unit scale. Tracking calibration scales local tracked positions, not rigidbody transforms or the mesh.

The body moves toward the head's horizontal position for room-scale walking. The tracking origin receives body displacement after subtracting only the room-scale component actually completed. This is an initial controller and needs combined room-scale/joystick/obstacle testing; its axis allocation is not a full locomotion constraint solver.

## Generated content

- Soldier source: `Assets/Ch15_nonPBR@T-Pose.fbx` (Humanoid mapping already committed).
- `Soldier Visual` is intentionally inactive until its actual wrist orientation, eye offset, and first-person head rendering are fitted in Unity. It is already wired to the visual driver. Enable it after those checks, then hide the colored debug hand meshes.
- The M1911 is included as a **grabbable prop**, if the model is available. Its temporary box collider and 22 cm longest-dimension scale are starting values. Inspect moving mesh parts, real orientation and collision geometry before weapon implementation.
- Floor, collision wall, low ceiling, ramp, stair obstacles, light/heavy cubes, and a two-hand bar.
- Stairs are deliberately test obstacles: step assistance is **not implemented** yet. Jumping may be needed until Claude completes locomotion.

## Known limits / next work

- No full-body active ragdoll, physical elbows/torso/head/legs, arm reach constraints or load transfer into the body yet. Hands can reach beyond the fixed avatar's limbs; visual IK clamps its own limb lengths. Claude must complete physical reach/body integration rather than hide the discrepancy by stretching bones.
- No climbing, mantling, moving-platform support, virtual crouch, seated mode, finger posing, holsters, damage, networking or saved user calibration.
- No working firearm, magazine/socket/chamber simulation, recoil or firing audio. These belong after the core is verified; all four requested weapon families remain goals.
- No automatic physics-hand teleport on tracking reacquisition or snap turns. Forces remain bounded; inspect recovery/lag and constrained two-hand objects in VR.
- Camera locomotion follows fixed-step body movement. Interpolated presentation and refresh-rate-specific jitter checks remain necessary.
- Floor/device origin switching and runtime recentering require live runtime tests. The device fallback assumes the user supplies a suitable standing height.
- The avatar's feet are an initial visual ground projection, not a procedural gait or physical stepping system.
- Pair filtering is for the current local rig. Reapply/replace filtering if colliders are enabled or added at runtime; multiplayer collision rules are future work.
- `RigInput` uses one serialized asset per local player. A multiplayer spectator/local-player architecture must assign input ownership explicitly.

## Validation status

No Unity Editor, Unity MCP, or headset is available to the authoring agent. Generated scenes/prefabs, shader rendering, model geometry, Unity compilation, EditMode test execution, and physical feel must be verified on the user's machine. Source syntax/metadata/diff checks are useful but are not Unity compilation or Play Mode evidence.

See `CLAUDE_HANDOFF.md` for the integration work and `CURSOR_HANDOFF_TEMPLATE.md` for the evidence-based next handoff.
