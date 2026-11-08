You are continuing Corbino's Universal Rig Unity project. Implement the next milestone in the actual project.

Repository: https://github.com/Corbino50211/VR-Universal-Rig-Testing-Site

Start from branch **`claude/rig-integration`**, at its tip commit (first commit after base `45432ec`; check with `git log -1`). Preserve any local work.

Before editing:
- Read the local or repository agent instructions, `Docs/RIG_FOUNDATION.md` (architecture), `Docs/RIG_VALIDATION.md` (evidence) and this handoff.
- Check which tools you actually have. Don't assume you have a Unity MCP. If you do, only one agent may drive the live editor at a time.

Target:
- Unity 6000.6.5f1, URP 17.6.0, PCVR first (Quest later).
- One fixed Mixamo soldier avatar: `Assets/Solder.fbx`, GUID `115d875de3f8af84bb820e10da65ef25`. The user renamed it from `Ch15_nonPBR@T-Pose.fbx` and the GUID is unchanged.
- The rig must stay reusable in other games. A different fixed avatar is configured in the editor through `RigSettings` + **Tools > Universal Rig > Fit Avatar**. No runtime avatar swapping.

## Current verified state

**Scene, prefab and settings:**
- `Assets/UniversalRig/Generated/RigPhysicsLab.unity`
- `SoldierRig.prefab`
- `SoldierSettings.asset`
- `SoldierInput.inputactions`
- `Soldier_FirstPerson.asset` (headless mesh, Git LFS)

The scene includes: floor, wall, a 1.1 m ceiling, a table, 4 × 15 cm stairs plus a landing, 15° and 55° ramps, a ladder (`Climbable`) to a 2.4 m platform, 0.5/5/40 kg props and a two-hand bar. It also has the user's **`Assets/Weapons/Prefabs/M1911.prefab`**, unmodified and at real scale, at (1.9, 1.05, 0.6). It currently works only as a prop: a 1.1 kg Rigidbody with `PhysicsGrabbable`, plus box colliders that `RigBuilder` added on its Slide and Magazin parts.

Regenerate everything with **Tools > Universal Rig > Create Soldier Test Scene**. It keeps GUIDs, so don't hand-edit generated files you can regenerate. If you add weapon setup to the lab, add it in `RigBuilder.BuildLab` or in your own builder.

**Input:**
- Input System actions with OpenXR bindings:
  - `<XRHMD>` center eye.
  - `<XRController>{LeftHand|RightHand}` `devicePosition` / `deviceRotation` (grip pose).
  - `grip` and `trigger`.
  - `primary2DAxis` (move left, turn right).
  - Right `primaryButton` = jump, left `primaryButton` = recalibrate.
- **`RigInput` is the only reader of input.** Read values from it, or from `PhysicsHand.Grip` / `PhysicsHand.Trigger`. Don't create a second action asset or player input.
- Haptics are **not** implemented yet. Add them through the Input System's XR haptic command API, and verify that the API exists in Input System 1.20.0.
- Device on the user's PC: the active OpenXR runtime is SteamVR, and Oculus `OVRServer` was running, so it is probably a Quest via Link. **Not confirmed.**

**Core classes. Extend these, don't duplicate them. Each one has a single owner:**
- `PhysicsHand`:
  - Its Rigidbody is driven by a bounded PID toward the controller.
  - A reach joint ties it to the avatar shoulder.
  - Exposes `HeldObject`, `Holding`, `Anchored`, `Grip`, `Trigger`, `isLeft`, `Body`, `TrackingError`.
  - `TryGrab` currently attaches at the **closest collider point** with a locked ConfigurableJoint. The first hand locks rotation; the second hand is position-only.
  - Weapons need **authored grip poses**. Add an optional grip-point component that `TryGrab` prefers, so the joint is created with the hand aligned to the grip. Keep the existing fallback.
- `PhysicsGrabbable` (sealed):
  - Handles ownership: `TryHold`, `Release`, `PrimaryHand`, `HoldingHands`, `maximumHands`, `Body`.
  - Compose weapon behaviour as a **separate component** on the same Rigidbody. Do not subclass it or fork it.
- `PhysicsRig`:
  - Owns the body and tracking space.
  - `IgnoreHeldCollisions` / `ReleaseHeldCollisions` / `IgnoresBody(Collider)` already stop held objects from colliding with or lifting the player. Reuse them for magazines inserted into a held gun, and for excluding the player's own colliders from shots.
  - The body Rigidbody is `PhysicsRig.Body`.
- `RigCollisionFilter`: local-rig collision pairs. Call `RefreshPairs()` if you add rig colliders at runtime.
- `AvatarPoseDriver`:
  - The only bone writer.
  - Fingers curl from grip and trigger, and fully when `Holding`.
  - If you add per-weapon hand poses, add them **here**, not in a second bone writer.
- `RigMath`: pure, testable math. Put new math here, with EditMode tests.

**Unity compile and automated tests** (2026-11-08, run in the user's editor through the Unity MCP):
- Compile is clean and `Validate Active Rig` passes.
- EditMode: **25/25**.
- Play Mode with XRI simulated HMD + controllers through the real bindings: **17/17**, covering:
  - stand alignment, arm reach limit, stairs, ramps, crouch under a ceiling, blocked stand-up fade;
  - grab/carry/throw, held object can't lift the player, climb bounded/hang/release;
  - controller and head tracking loss and recovery, fast swing into a wall, room-scale into a wall;
  - room-scale + joystick combinations, jump, destroying a held object, two-hand handover while turning;
  - timing.
- Timing: `FixedBehaviourUpdate` 0.013 ms, `Physics.Simulate` 0.022 ms (editor).
- Cold start without a headset, and 3 Play Mode enter/exit cycles: 0 errors.
- Run the tests with Test Runner, or with `TestRunnerApi`. Run EditMode and PlayMode as **separate** executions; a combined `ExecutionSettings` only ran EditMode.

**Human headset tests: NOT RUN.** The full checklist is in `Docs/RIG_VALIDATION.md` §3.

**Outstanding blockers:** no failing automated checks. All remaining rig risks are headset-only:
- grip alignment and offsets per controller;
- PID feel and jitter at the real refresh rate;
- fade in both eyes (MultiPass);
- climb comfort;
- `handReaction` strength;
- render-interpolation judder.

Known design limits: no articulated active ragdoll; no gait; snap turn swings held objects; spectators see the headless mesh.

**What you may edit:**
- `Assets/UniversalRig/Runtime` (extend it), `Assets/UniversalRig/Editor`, `Assets/UniversalRig/Tests`.
- A new weapons code folder, such as `Assets/UniversalRig/Weapons/` (its own asmdef, referencing `UniversalRig.Runtime`).
- Prefab **variants** of the user's weapon prefabs.

**What to avoid:**
- Don't modify the user's original prefabs, imported models or `Assets/Testing Scene.unity`.
- Don't rewrite `PhysicsRig`'s locomotion.
- Don't add an XR Origin, a CharacterController or an XRI interactor/locomotion stack.

## Your task

1. **Rig blockers first.**
   - Ask the user to run the headset checklist in `RIG_VALIDATION.md` §3 and report back, or read any results already recorded there.
   - Fix any rig issue they report before weapon work. Keep the verified rig working: rerun both test suites after every rig change.
   - If no headset feedback is available, go ahead with weapons, and record that headset verification of the rig is still open.
2. **Inventory the weapons.**
   - Start with the user's authored prefabs in `Assets/Weapons/Prefabs`, then their source models in `Assets/Low Poly Weapons VOL.1` and `Assets/LowPoly/Low Poly Weapons VOL.2`.
   - Objects already found by name:

     | Prefab | Named objects |
     |---|---|
     | `M1911` | Slide, Magazin, Hammer, Trigger |
     | `Uzi` | Bolt, Magazin, Stock, Stock_01, Trigger |
     | `AK74` | Bolt, Mag, Trigger |
     | `M4_8` | Bolt, Mag, Sight, Rear_Sight, Trigger |
     | `Bennelli_M4` | Pump, Trigger |

   - Check in Unity that each one is a separately movable mesh with sensible pivots. Record the results in the validation doc.
   - Only make model variants where a part is fused. Keep the original imports.
3. **Build the first complete physical M1911**, as a prefab variant:
   - primary and support grips (grip points);
   - stable collisions;
   - controller trigger input through `PhysicsHand.Trigger`;
   - physical magazine insertion and removal (a socket with a constraint, not parenting);
   - slide operation by the support hand;
   - explicit chamber and ammunition state;
   - firing only when a valid round is chambered;
   - slide lock and empty behaviour;
   - recoil applied through the weapon's Rigidbody;
   - visual, audio and haptic feedback, using only assets and APIs you have verified exist.
4. **Keep weapon logic separate from game-specific damage**, using a documented interface or event (for example, a hit-receiver interface on the target). Choose and document a hit model (hitscan raycast or a physical projectile). Handle muzzle obstruction: no firing through a wall the muzzle is pressed into. Exclude the local player's own colliders with `PhysicsRig.IgnoresBody` and the rig's colliders. Never damage objects by guessing from type names.
5. **After the pistol passes**, extend the shared architecture to SMGs (Uzi), rifles (AK74, M4) and shotguns (Benelli M4):
   - fire modes; bolt, charging handle and pump actions; tube loading for the shotgun; two-hand handling; shot spread.
   - Keep configuration in ScriptableObject data assets. Use one shared controller with modules, **not** four copies.
6. **Constraints and forces:**
   - Hold weapons with physics constraints only; no transform parenting that bypasses mass and collision.
   - Keep recoil and two-hand forces bounded.
   - Add a Play Mode test showing that firing or bracing cannot propel the player. `HeldObjectCannotLiftThePlayer` is the pattern to follow.
7. **Save the results:**
   - the finished prefabs;
   - a test range, either a section of the lab or a new scene, with ammunition and clear control labels.
   - Then check that everything persists after reopening the scene and restarting Play Mode.

## Validation

Extend `Assets/UniversalRig/Tests/PlayMode/RigPlayModeTests.cs`, or a sibling weapons test file. It uses the simulated XRI devices through the real bindings, and `Seconds()` re-pushes device state every frame. Cover:
- physical grab and release, and hand swapping;
- tracking loss while holding a gun, and wall obstruction at the muzzle;
- one-hand and two-hand recoil;
- dropping a loaded weapon;
- empty firing and partial reload interruption;
- magazine swaps and ammunition accounting;
- shotgun loading and cycling;
- frame-rate variation (change `Time.fixedDeltaTime` and/or `captureFramerate`);
- zero Console errors.

Keep simulated and headset results separate in `Docs/RIG_VALIDATION.md`. Do not claim tests you could not run.

Preserve existing work, GUIDs and assets. Use the installed package APIs. If a required sound or model part is missing, report it precisely and use a substitute that is clearly labelled temporary. Commit `.meta` files. Never force-push.

Finish with:
- a pushed commit or PR on a clearly named branch;
- complete files;
- an updated `RIG_VALIDATION.md`;
- a short explanation of what the user can test in the headset.
