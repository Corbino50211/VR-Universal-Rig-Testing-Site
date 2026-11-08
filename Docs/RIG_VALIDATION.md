# Universal Rig: validation record

Last updated 2026-11-08. Rig foundation by Claude Code (Opus 5.5). Firearm milestone in the same editor: EditMode 28/28, rig PlayMode 17/17, firearm PlayMode 3/7 (details in section 4). Headset testing has still not been run.

**Headset testing has NOT been run.** Everything below comes from either automated tests or simulated XR devices in the editor. Results are kept in three separate sections: automated, simulated and headset.

## Environment

| Item | Value |
|---|---|
| Editor | Unity 6000.6.5f1 (Windows 11) |
| Render pipeline | URP 17.6.0, XR stereo mode MultiPass |
| Input / XR | Input System 1.20.0, OpenXR 1.18.0 (Standalone loader, InitManagerOnStart), XR Management |
| XRI | 3.6.1. Used only for `XRSimulatedHMD` / `XRSimulatedController` in Play Mode tests. |
| Physics | 90 Hz fixed step (`fixedDeltaTime` 0.01111), maximum timestep 0.1 |
| Active OpenXR runtime on test PC | SteamVR, but it was not running during the tests. `OVRServer` was running, so the headset is probably a Quest via Link. **Not confirmed.** |

## Paths

| Asset | Path |
|---|---|
| Avatar model | `Assets/Solder.fbx` (GUID `115d875de3f8af84bb820e10da65ef25`; renamed by the user from `Ch15_nonPBR@T-Pose.fbx`) |
| Avatar profile / tuning | `Assets/UniversalRig/Generated/SoldierSettings.asset` |
| Player prefab | `Assets/UniversalRig/Generated/SoldierRig.prefab` |
| Test scene | `Assets/UniversalRig/Generated/RigPhysicsLab.unity` |
| Input actions | `Assets/UniversalRig/Generated/SoldierInput.inputactions` |
| First-person (headless) mesh | `Assets/UniversalRig/Generated/Soldier_FirstPerson.asset` (Git LFS) |
| Runtime code | `Assets/UniversalRig/Runtime/` |
| Editor tools | `Assets/UniversalRig/Editor/` |
| Tests | `Assets/UniversalRig/Tests/EditMode/`, `Assets/UniversalRig/Tests/PlayMode/` |

## Controls and bindings (`SoldierInput.inputactions`)

| Action | Binding |
|---|---|
| Head pose / tracked / state | `<XRHMD>/centerEyePosition`, `centerEyeRotation`, `isTracked`, `trackingState` |
| Hand poses | `<XRController>{LeftHand\|RightHand}/devicePosition`, `deviceRotation` (the OpenXR grip pose), `isTracked`, `trackingState` |
| Move | Left `primary2DAxis` |
| Turn | Right `primary2DAxis` (smooth by default; `snapTurn` / `snapAngle` in settings) |
| Grab / climb | `grip` (press threshold `gripPress`, release threshold `gripRelease`) |
| Trigger | `trigger`. Exposed as `PhysicsHand.Trigger`. Nothing uses it yet. |
| Jump | Right `primaryButton` |
| Recalibrate standing height | Left `primaryButton` |

## Fitted soldier values (from the skeleton, by `RigAvatarFitter`)

- Eye: (0, 1.669, 0.111).
- Head bone: (0, 1.558, −0.019).
- Shoulders: (±0.195, 1.429, −0.047).
- Arm length: 0.498 m.
- Hip height: 0.954 m.
- Wrist offsets in the grip frame: left (−0.033, 0.083, 0), right (0.036, 0.090, 0).
- Hand collider: center (0, 0.033, 0), size (0.056, 0.111, 0.094).

These come from geometry only. Expect to tune the grip feel on the headset with `PhysicsHand.positionOffset` / `rotationOffset`.

## Files changed in this integration

- **Runtime:**
  - `RigSettings.cs`: avatar profile, head-derived geometry helpers, body, climb and comfort settings.
  - `PhysicsRig.cs`: rewritten. Floating capsule, steps, room-scale booking, climbing, recentering, render offset, fade inputs, held-object collision ignore.
  - `PhysicsHand.cs`: rewritten. Reach joint, load reaction, Climbable anchoring.
  - `AvatarPoseDriver.cs`: rewritten. Sole bone owner, head/hips/arms/feet/fingers.
  - `RigInput.cs`: origin-ready state and recenter events.
  - `RigMath.cs`: `Yaw`, `GroundSpring`, `ClimbAcceleration`.
  - `HeadSafetyFade.cs`.
  - New `Climbable.cs`.
- **Editor:**
  - New `RigAvatarFitter.cs`.
  - `RigBuilder.cs`: rewritten. Fit, rig, lab scene, M1911 prop, 90 Hz.
  - `RigValidator.cs`: one-owner and fitting checks.
- **Tests:**
  - `EditMode/RigMathTests.cs`: new cases.
  - New `PlayMode/` assembly with 17 tests.
- **Generated:** `Assets/UniversalRig/Generated/*` (settings, prefab, scene, input, headless mesh, materials, physic material).
- **Project:**
  - `ProjectSettings/TagManager.asset`: layer 8 `UniversalRigPhysics`.
  - `ProjectSettings/TimeManager.asset`: 90 Hz.
  - `.gitattributes`: the headless mesh goes through LFS.
  - User's FBX rename (`Ch15_nonPBR@T-Pose.fbx` → `Solder.fbx`, GUID unchanged).

## 1. Automated checks (no XR devices)

| Check | Result |
|---|---|
| Compile, all assemblies | Pass. 0 errors, 0 warnings from rig code. |
| `Tools/Universal Rig/Validate Active Rig` on RigPhysicsLab | Pass |
| EditMode tests (`UniversalRig.EditModeTests`) | **25/25 pass**. RigMath: bend solver, rotation error, room-scale booking, ground spring, climb bounds, yaw at ±90° pitch, rest-pose geometry. Grab ownership. |

## 2. Simulated-input checks (Play Mode, XRI simulated HMD + controllers through the real `SoldierInput` bindings)

**17/17 pass** (48.4 s). These drive the actual generated `SoldierRig.prefab` with simulated device state. They show that the physics logic behaves as asserted. They do **not** show how it feels, how tracking noise or latency behaves, or how a real runtime behaves.

| Test | What it asserts |
|---|---|
| StandingIsStillAndAvatarEyesMatchCamera | Body still (< 2 cm, < 0.05 m/s). Eye height matches the calibration. Avatar head bone within 1 cm of the camera-derived position. Visible wrists within 2 cm of the physical hands. No fade. |
| HandReachIsLimitedToTheAvatarArm | A target 1.5 m away: the hand stays within armLength + 3 cm of the shoulder and is never teleported. The visible arm matches. |
| WalksUpStairsWithoutLaunching | 4 × 15 cm steps walked onto a 0.6 m landing (±5 cm). Peak rise speed < 2.5 m/s. |
| WalkableRampClimbsAndSteepRampDoesNot | 15° ramp: climbs, and standing on it doesn't slide. 55° ramp: can't be climbed. |
| CrouchUnderCeilingAndBlockedStandUpFades | Capsule shrinks under the 1.1 m ceiling and won't grow into it. Body isn't shoved through it. Head pushed through it fades. |
| GrabCarryReleaseAndThrow | Grab, lift (0.3 m ± 6 cm), throw keeps the hand's velocity, release adds no energy. |
| HeldObjectCannotLiftThePlayer | Standing on and pushing down a held object doesn't raise the body (< 5 cm). |
| ClimbIsBoundedHoldsStillAndReleases | Ladder grip anchors the hand. Pulling down lifts the body 0.6 m. Speed ≤ `maximumClimbSpeed`. Hanging drifts < 2 cm. Release speed is bounded and the body lands grounded. |
| ControllerTrackingLossReleasesThenRecovers | Lost controller: releases the held item and the hand damps in place (< 0.2 m/s). Reacquired: the hand returns (< 5 cm). |
| HeadTrackingLossFadesAndReacquisitionRecenters | Lost HMD: fades and the body stays grounded. Reacquired with a 1 m pose jump: recenters (< 5 cm) and the body doesn't chase the jump. |
| FastSwingIntoWallIsBlockedAndPushesBodyBack | A fast hand swing doesn't pass through the wall. Pushing on the wall moves the body away, never into it. |
| RoomScaleWalkIntoWallIsBlockedAndFades | Walking into the wall in room-scale: body stops outside it, the head fades, stepping back reunites head and body. |
| RoomScaleAndJoystickCombineWithoutDrift | Room-scale walking opposite to and perpendicular to the stick: head–body separation < 0.25 m, settles to < 5 cm with no drift. |
| JumpRisesAboutJumpHeightAndLandsSoftly | Peak about `jumpHeight` (± 0.15 m). Lands grounded at floor height (± 3 cm). |
| DestroyingAHeldObjectReleasesCleanly | Destroying the held item releases the hand and it recovers. |
| TwoHandHandoverAndTurningKeepsGrip | Two-hand grip survives turning. Releasing the primary hand promotes the other hand. |
| SteadyStateTiming | Editor, simulated input: `FixedBehaviourUpdate` avg **0.013 ms**, `Physics.Simulate` avg **0.022 ms**. GC allocated **≈ 8.7 KB/frame for the whole editor frame**. That figure is not isolated to the rig, so it doesn't prove zero rig allocations. Use the headset build profiler for that. |

### Other editor checks

- **Cold start without a headset:** RigPhysicsLab entered Play Mode with SteamVR not running. The OpenXR loader loaded with no active runtime. No exceptions. Exactly one rig, idling grounded.
- **Repeated Play Mode entry/exit:** 3 cycles on RigPhysicsLab, with domain reload disabled. 0 errors and 0 warnings in the console. Caveat: the unfocused editor did not advance game time during these MCP cycles, so this checks lifecycle (enable/disable/static state) only, not behaviour over time. The test runner's own enter/exit passed as well.
- **First-person head rendering:** the headless mesh renders for the local camera, and the original mesh is ShadowsOnly. Checked by inspecting the renderer setup, not by looking through the headset.

### Not covered by automation

- Joint break under force (`grabBreakForce`). There is a code path (`OnJointBreak`) but no test.
- Disabling a held object while it is held. Destroying it is tested. The EditMode test only shows that a disabled item rejects new grabs.
- Gripping through a thin wall. The linecast rejection exists but has no test.
- A range of object masses beyond the lab props (0.5 kg, 5 kg, 40 kg crate, two-hand bar).
- Snap turn with held objects.

## 3. Headset checks: NOT RUN

None of these have been run. Each needs a person in the headset.

1. Cold start **with** the headset on SteamVR and on Oculus/Meta OpenXR. Confirm the floor origin, standing calibration, and that the camera sits at the soldier's eyes.
2. Both eyes: comfort fade under the low ceiling, inside the wall and on tracking loss, with fast head motion. URP is MultiPass.
3. Hand grip alignment for the actual controllers. Do the soldier's hands sit naturally around the controller grip? Tune `positionOffset` / `rotationOffset`.
4. Feel of the hand PID: lag, overshoot, jitter at 90 Hz and at the headset's real refresh rate (72/80/120 Hz).
5. Room-scale walking in a real play space, combined with the stick, plus recenter from the runtime's own recenter button.
6. Climbing the ladder and mantling onto the 2.4 m platform. Check for nausea or overshoot, and for release-throw energy.
7. Pushing on walls and tables: does the load transfer into the body feel right, or is it too pushy (`handReaction`)?
8. Crouch, stairs, ramps and jump comfort.
9. Visual: elbows, hip shift, foot placement and finger curl on the actual soldier; head and helmet clipping into the view.
10. Runtime CPU/GPU timing and GC in a headset build (development build profiler).

## 4. Firearms (simulated input only)

Headset checks in section 3 are still **not run**. Nothing in this section is a headset result.

### Part inventory (editor, 2026-11-08)

Every named part below is its own `MeshFilter` with its own transform. No fused part, so no model variant was made. Original imports and `Assets/Weapons/Prefabs` are unchanged. Prefab variants are under `Assets/UniversalRig/Weapons/Generated/Prefabs/`.

| Prefab | Part | Pivot |
|---|---|---|
| M1911 | Slide, Magazin, Hammer, Trigger | Separate. The prefab root is a Cinema 4D node rotated about 51° yaw; the builder places grips from the mesh bounds, not from that root. Magazin pivot is at the top of the mag (local X rotation about 339°). Slide pivot is at the rear of the slide. |
| Uzi | Bolt, Magazin, Stock, Stock_01, Trigger | Separate. Magazin pivot is at the top of the stick. Bolt pivot is on the bolt. |
| AK74 | Bolt, Mag, Trigger | Separate meshes. Mag and Trigger pivots sit at the weapon origin, not on the part. The physical magazine recenters the mesh bounds; the import was not edited. |
| M4_8 | Bolt, Mag, Sight, Rear_Sight, Trigger | Separate. Bolt pivot is at the charging handle. Mag pivot is at the weapon origin (same treatment as the AK). |
| Bennelli_M4 | Pump, Trigger | Separate. No tube or shell mesh in the import. Shells are a red box named `TEMPORARY shell` (`Bullets.fbx` has no shotshell). |

### Hit model

Hitscan. The ray starts `muzzleBackoff` (0.08 m) behind the muzzle. `RigMath.SelectShotHit` skips the gun, a seated magazine, the action handle, and the local rig (`IgnoresBody` plus anything under the rig root). A hit at or behind the muzzle is the wall the barrel is buried in; the ray does not continue. Damage is only `IHitReceiver.ReceiveHit`. The lab board is a `HitTarget`. Colliders without that component get a spark and nothing else.

### Simulated results (XRI devices, real `SoldierInput` bindings)

EditMode `UniversalRig.EditModeTests`: **28/28** (the previous 25, plus bounded impulse, cone direction, and shot selection).

Rig PlayMode `RigPlayModeTests`, rerun after the grip and firearm changes: **17/17**.

Firearm PlayMode `FirearmPlayModeTests`, one complete run after the slide-rail fix: **3 pass / 4 fail**.

| Test | Result |
|---|---|
| GrabReleaseSwapAndTrackingLoss | Pass. Primary grip grab, release, other hand takes the gun, tracking loss drops it. |
| MuzzleWallStopsTheShot | Pass. A wall in front of the muzzle is hit. A target behind it is not. |
| OneClickFiresOneRoundWhenTheFrameRateChanges | Pass. One trigger click at `fixedDeltaTime` 1/30 and `captureFramerate` 20 fires one round and chambers the next. |
| EmptyFireReloadAccountingAndDrop | Fail. The support hand did not take the slide (hand stayed about 6 cm short of the handle). |
| RecoilIsWeakerBracedAndDoesNotPropelTheBody | Fail. The second hand grabbed the slide handle instead of the frame's support grip. |
| ShotgunLoadsFromThePortAndMustBePumped | Fail. The gun did fire (`ShotsFired == 1`, pump required before the next shot) but the plate 3 m downrange recorded no `HitTarget` hit. |
| UziChargesFromTheBoltAndTheRifleChangesFireMode | Fail. The bolt was grabbed. `Open01` stayed 0, so it did not cock. |

An earlier full PlayMode run in the same session (rig + firearms together) was 19 pass / 5 fail. All 17 rig tests passed in that run too. The firearm failures were the action-hand tests, not locomotion.

Haptics: `SendHapticImpulseCommand` exists on Input System 1.20.0 (`XRControllerWithRumble.SendImpulse` calls it). `RigInput.Pulse` sends that command. Simulated controllers do not prove the headset motor.

Audio: no firearm clip exists in the project. The three synthesized clips are named `TEMPORARY firearm shot`, `TEMPORARY firearm dry fire`, and `TEMPORARY firearm mechanism`.

### What to try in the headset

Open `Assets/UniversalRig/Generated/RigPhysicsLab.unity`. Section 3 is still the rig checklist. For the guns, turn around to the bench at the back (and the M1911 on the original table):

- Grip the pistol grip. The hand should sit on the authored grip, not on the nearest collider.
- Trigger fires only with a round in the chamber. An empty gun clicks.
- The other hand grabs the slide, bolt, or pump and racks it. Releasing a magazine into the well seats it with a joint, not by parenting. A partial insert (still in the hand, or pulled away) does not move rounds.
- Other hand's trigger on the small selector cube switches SEMI / AUTO on the Uzi, AK, and M4.
- Red boxes are temporary shells for the Benelli. Push one into the port, then pump.
- Firing or shoving the gun into the floor should not launch you.
- Sounds are temporary synthesized noises, not recordings.

## Unresolved issues / provisional decisions

- No articulated active ragdoll. Elbows, torso and legs are visual only, and the physical arm is a reach constraint.
- No procedural gait or physical feet.
- Snap turn swings held objects, because the hands catch up physically.
- Spectator or mirror cameras see the headless mesh.
- `RigBuilder.SetupFirstPersonRenderers` handles a single skinned mesh (`ponytail:` note).
- Interpolation offset: the tracking space is shifted by the body's render-interpolation delta in LateUpdate. It is smooth in the editor. Check judder on the headset.
