# Universal Rig: architecture

Target: Unity **6000.6.5f1**, URP **17.6.0**, Input System **1.20.0**, OpenXR **1.18.0**, PCVR first (Quest later). XRI **3.6.1** is installed, but only its simulated HMD/controllers are used, and only in Play Mode tests. The rig owns its own movement and grabbing. Do not add an XR Origin, a CharacterController or an XRI move provider.

This is original code. It is BONEWORKS-*inspired*, not Stress Level Zero code and not a finished BONEWORKS equivalent. Test evidence is in `RIG_VALIDATION.md`.

## Quick start

1. Clone with Git LFS. The soldier is `Assets/Solder.fbx` (GUID `115d875de3f8af84bb820e10da65ef25`; formerly `Ch15_nonPBR@T-Pose.fbx`, renamed with the same GUID). It is about 115 MB.
2. Open `Assets/UniversalRig/Generated/RigPhysicsLab.unity` and enter Play Mode with a PCVR OpenXR runtime running.
3. If anything is missing, run **Tools > Universal Rig > Create Soldier Test Scene**. It refits the avatar, rebuilds `SoldierRig.prefab` and the lab scene, and reuses the existing asset GUIDs.

Controls:
- Left stick: move, head-relative.
- Right stick: turn (smooth or snap, set in settings).
- Right primary button: jump.
- Left primary button: recalibrate standing height. Stand upright when you press it.
- Grip: grab items, or grip a `Climbable` to climb.
- Trigger: the value is exposed on `PhysicsHand.Trigger`. Nothing fires yet.

## One fixed avatar per game

`RigSettings` (a ScriptableObject) is the per-game avatar profile. To use another avatar in a future game:
1. Assign `avatarModel` to a Humanoid model.
2. Select the settings asset and run **Tools > Universal Rig > Fit Avatar (selected Rig Settings)**.
3. Rebuild the rig.

The fitter measures these from the skeleton:
- Eyes, head bone, shoulders, hips and arm length.
- The OpenXR grip-frame wrist offsets and hand-bone rotations.
- The hand collider.

It also generates a headless first-person mesh. Avatars cannot be switched at runtime, by design.

## Components (each thing has exactly one owner)

| Component | Owns / does |
|---|---|
| `RigInput` | Tracked head/hand transforms from the action asset. Tracking state. Floor-origin request, with a fallback device height. Buffered button presses. `OriginReady`. Recenter requests from `XRInputSubsystem.trackingOriginUpdated`. |
| `PhysicsRig` | The tracking space and the body Rigidbody. Floating capsule with its origin at the feet: a SphereCast ground probe plus a bounded ride-height spring, so steps under `stepHeight` (0.3 m) are walked over. Head-relative stick walking, room-scale following and turning. Crouch by shrinking the capsule, with a ceiling check before standing. Slopes, jump, climbing and recentering. Render-interpolation offset applied in `LateUpdate`. Comfort-fade inputs. Held objects ignore collisions with the body. |
| `PhysicsHand` | The hand Rigidbody. A bounded force/torque PID drives it toward the controller grip pose. A reach joint (ConfigurableJoint, a linear limit of `armLength` from the avatar shoulder) means the physical hand can never leave the visible arm. Equal and opposite force goes into the body when the hand is loaded. Grab joints for `PhysicsGrabbable`. World/kinematic anchor joints for `Climbable`. Tracking loss releases the grip and damps the hand. It is never teleported. |
| `PhysicsGrabbable` | One- or two-hand ownership. The primary hand controls rotation and the second hand adds position. Handover goes to the remaining hand. |
| `Climbable` | Marker component. Static or kinematic colliders under it can be gripped and climbed. |
| `AvatarPoseDriver` | The only writer of bones. The Animator is disabled. Places the root and neck/head, shifts the hips so the head bone sits under the camera, solves two-bone arm IK to the *physical* hands, raycasts the feet to the ground, and curls fingers from the grip and trigger values. |
| `HeadSafetyFade` | Black fade when the head is inside geometry, when there is a wall between the top of the body capsule and the head, when head tracking is lost, or when head–body separation exceeds `maximumHeadSeparation`. |
| `RigCollisionFilter` | Ignores collision pairs within the local rig. It does not change the global collision matrix. |
| `RigBuilder`, `RigAvatarFitter`, `RigValidator` (Editor) | Fit, generate and validate. |

Hierarchy:
- The root is a stationary container.
- Tracking Space, Physics Body, both hands and Avatar Visual are its siblings.
- Never parent Tracking Space or the hands to the moving body.

First-person rendering:
- A headless copy of the skinned mesh renders for the local view.
- The original mesh is set to ShadowsOnly, so the head still casts a shadow.

Physics runs at 90 Hz (`Time.fixedDeltaTime = 1/90`, maximum timestep 0.1).

## Firearms

Guns live in `Assets/UniversalRig/Weapons` (`UniversalRig.Weapons`), composed onto `PhysicsGrabbable`. They do not subclass it and they do not change locomotion.

- `GripPoint` (runtime) is an optional pose. `PhysicsHand.TryGrab` prefers it over the closest collider point and aligns the hand rigidbody to that transform before creating the hold joint. The closest-point fallback is unchanged. `AvatarPoseDriver` is still the only bone writer; a grip point keeps the index finger on `PhysicsHand.Trigger`.
- One `Firearm` reads `FirearmProfile` (a ScriptableObject). The same component covers the M1911 (closed bolt, semi), Uzi (open bolt, semi/auto), AK74 and M4 (closed bolt, semi/auto) and the Benelli M4 (pump, 8 pellets, 5° cone).
- `ActionPart` is the slide, bolt or pump: its own rigidbody on a prismatic `ConfigurableJoint`, not parented to the gun. `MagazineSocket` seats a magazine with a `ConfigurableJoint`, or absorbs a shell into the tube. Neither parents the round into the gun.
- Hits are **hitscan**. The ray starts `muzzleBackoff` behind the muzzle. `RigMath.SelectShotHit` ignores the gun, the local rig (`PhysicsRig.IgnoresBody` and the rig hierarchy) and keeps a buried muzzle from hitting anything past the contact. A collider is damaged only when it has `IHitReceiver`. The lab target is `HitTarget`.
- Recoil is a clamped impulse on the gun rigidbody (`RigMath.BoundedImpulse`). A second hand on the gun, or a hand on the action, scales it by 0.45. Velocity added by that impulse is capped (`maxRecoilSpeed`, 0.4 m/s before the brace scale).
- Haptics use Input System 1.20 `SendHapticImpulseCommand` via `RigInput.Pulse`. The project has no firearm audio. Shots, dry fires and mechanism sounds are synthesized clips whose names start with `TEMPORARY`. The shotgun shell is a red box named `TEMPORARY shell` because `Bullets.fbx` has no shotshell.
- Rebuild with **Tools > Universal Rig > Build Firearms**, or with **Create Soldier Test Scene**, which calls the same builder. Original weapon prefabs and models are not modified.

## Known limits

- **No articulated active ragdoll.** Elbows, torso and legs are visual IK only. The physical arm is a reach constraint plus load transfer, not jointed limb bodies.
- **No procedural gait.** Feet are ground-projected.
- **Mantling** relies on climbing's room-follow. There is no dedicated vault.
- **Snap turn** rotates the tracking space, and the hands catch up physically, so held objects swing.
- **Avatar support:** single skinned-mesh avatars only (`ponytail:` note in `RigBuilder.SetupFirstPersonRenderers`).
- **Spectator views** (a mirror, a second camera) see the headless mesh.
- **Wrist and grip offsets** are computed from the skeleton. They still need headset tuning per controller via `PhysicsHand.positionOffset` / `rotationOffset`.
- **Stereo mode:** URP is in MultiPass. Single Pass Instanced is untested with the fade shader.
- **Multiplayer:** collision filtering and input ownership are for one local player.
