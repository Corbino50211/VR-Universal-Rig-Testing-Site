# Claude: integrate and complete the PCVR rig through Unity MCP

You are the next engineer on Corbino's reusable physics VR rig. You have the user's local Unity project and Unity MCP. Implement and verify the next stage; do not only return advice or a design.

Repository: https://github.com/Corbino50211/VR-Universal-Rig-Testing-Site

Starting branch: `codex/rig-foundation`. Fetch it and inspect the diff from `main`. If the changes have already been merged or local work exists, preserve that work and reconcile the actual current state. Do not reset, clean, overwrite, or stash unrelated edits automatically. Use a clean worktree or coordinate a safe integration if needed. Only one agent may operate the live Unity project at a time.

## Goal and constraints

- BONEWORKS-inspired physically grounded PCVR player. Unity 6000.6.5f1 URP; standalone Quest is later.
- One fixed avatar per game. Tune the current soldier precisely; keep avatar dimensions/bindings/configuration replaceable in the editor for future games. Do not implement runtime avatar switching.
- The current model is **`Assets/Ch15_nonPBR@T-Pose.fbx`**. Do not use older model names from earlier chats.
- Use the existing imported Low Poly Weapons VOL.1 and VOL.2 art for the later weapons. Pistols, SMGs, shotguns and rifles are the intended families.
- There was no existing player controller or BIMOS source in the inspected base. Do not assume a hidden rig exists or invent APIs. Inspect the live project first.
- Preserve existing assets, GUIDs, scenes and the original imported packages. Use additive components/prefabs and scoped edits. Do not upgrade/downgrade Unity or replace package versions casually.
- Original implementation only; no claim of reproducing private SLZ source.

## Read first

Read `Docs/RIG_FOUNDATION.md`, this file, `Docs/CURSOR_HANDOFF_TEMPLATE.md`, and the complete code under `Assets/UniversalRig`. Inspect any repository/local `AGENTS.md` instructions. Review the current Console, package manifest, OpenXR settings, model import mapping, and existing scene hierarchy through MCP before editing.

The code is a **foundation awaiting Unity validation**, not a finished rig. Its author had no Unity Editor or headset. Do not assume a compile pass or a generated prefab exists. The soldier visual starts disabled deliberately. The current body is an upright dynamic capsule with two independent physical hands, not an active ragdoll. A grabbable M1911 prop does not implement a gun.

The branch includes the user's newer test-map and weapon-prefab commit. Preserve `Assets/Testing Scene.unity` and inspect `Assets/Weapons/Prefabs` as the starting place for weapon setup. The M1911 prefab text contains named Hammer and Trigger mesh objects; validate all moving parts in Unity before assigning behavior.

## 1. Import and generate

1. Confirm all required Git LFS assets have actual bytes. If models are LFS pointers, download the objects with the project's Git tooling before importing; never substitute a pointer file as a model.
2. Compile the new code. Read actual errors through MCP, fix them, and repeat until clear. Confirm `RigInput` uses the serialized generated input asset; avoid creating unregistered OpenXR actions after the session starts.
3. Run `Tools/Universal Rig/Create Soldier Test Scene` (public method `UniversalRig.Editor.RigBuilder.CreateSoldierTestScene`). Save the user's pending scene work before switching scenes. The generator creates unique output paths under `Assets/UniversalRig/Generated`.
4. Run `Tools/Universal Rig/Set PCVR Physics to 90 Hz` and `Tools/Universal Rig/Validate Active Rig`. Treat 90 Hz as an initial CPU budget to profile, not a universal requirement.
5. Verify Standalone OpenXR is active, the actual controller interaction profile is enabled, and exactly one system owns each tracked transform and locomotion. The original project already has OpenXR and XRI; don't add a second XR Origin/CharacterController/XRI move provider over the custom body.
6. Check the generated scene, materials, collider placement, camera, AudioListener, settings references, prefab persistence and action bindings. Run `UniversalRig.EditModeTests` in Test Runner and fix failures.

## 2. Fit the soldier and complete core physical behavior

Use MCP to inspect actual skeleton/world-space dimensions and live poses. Fit eye height, shoulders, limbs, hand collider/grip origins, wrist rotation offsets and arm reach to the soldier. Activate the soldier after it is fitted. Replace the colored debug hand rendering with the soldier hands. Make local first-person head/helmet rendering safe without breaking the visible body or shadow rendering. Do not hide the entire body as the final solution.

Extend this foundation into the fixed-avatar physical rig:

- Preserve fast HMD presentation while keeping the physical body, avatar eyes, and hands aligned. Fix world/room-scale feedback, interpolation and re-centering behavior based on real tests. Avoid feeding physics position corrections repeatedly back into tracked input.
- Add physical limb/body constraints and a controlled active ragdoll where needed. Use bounded drives, anatomical joint limits, appropriate mass distribution and solver settings. Connect the hands to the body with actual reach/force limits; do not merely stretch the visual arms or move dynamic bones with Transform writes.
- Ensure only one owner controls each bone/rigidbody. The prototype visual IK must not fight a new active ragdoll or Animator. Use explicit target versus physics versus render responsibilities.
- Finish crouching/standing clearance, slopes, stair traversal, jumping/landing and room-scale movement against obstacles. Verify a locked physical hand can transfer a controlled load into the body.
- Add stable climbing/pulling from intended climbable surfaces after that force path works. Prevent runaway energy or unlimited self-launching with held objects. Handle release and tracking loss predictably.
- Check grab ownership, two-hand constraint handover, throws, collisions, and reacquisition. No arbitrary teleportation of blocked hands or held objects to recover tracking. Any explicit reset/respawn needs collision-aware placement and a documented policy.
- Verify head obstruction fading in both eyes with the actual URP stereo mode, including fast head movement. A raw camera cannot be physically stopped by a virtual wall; use a deliberate comfort policy.

Make concrete changes and use MCP to save the resulting prefab and scene. If device-specific information is genuinely unavailable, ask the user for that detail; keep working on independent checks. Do not report live VR tests as passed without a connected headset and observed evidence.

## 3. Validation and commit

Maintain `Docs/RIG_VALIDATION.md` with exact editor/package versions, scene/prefab/settings paths, controls, changed files, test results, and each unresolved issue. Distinguish automated checks, simulated input and human headset tests.

Minimum test cases:

- Cold start with and without a connected headset; tracking loss/reacquisition; origin recenter; repeated Play Mode entry/exit.
- Stationary hands, fast swings, pressing against a wall, gripping at a wall, and different object masses.
- One/two-hand grab, either-hand release, broken constraint and object disable/destroy while held.
- Standing alignment, crouch under the low ceiling, attempted stand-up while blocked, floor/stairs/slopes and jumps.
- Room-scale walking alone, joystick movement alone, both in the same/opposite/perpendicular directions, turning while holding a two-hand object, and body blocked against a wall.
- Arm reach, elbows, hips, feet and head rendering with the actual soldier; physical hands and visible hands must agree.
- Climbing and release after implementing it; ensure no uncontrolled upward acceleration or growing joint error.
- Confirm zero runtime exceptions, no increasing allocations in the rig's steady-state physics loop, and record measured CPU/physics timing at the test refresh rate.

Commit on a clear integration branch and push it when ready. Keep imported binaries untouched unless a targeted fix is required. Include Unity `.meta` files and generated assets. Do not force-push over another agent's work. Report what is complete, what is still provisional, and what requires user headset feedback.

## 4. Give the user a real Cursor prompt

After your work, use `Docs/CURSOR_HANDOFF_TEMPLATE.md` to write **`Docs/CURSOR_HANDOFF.md`** populated with the actual branch, commit, file paths, working interfaces and test evidence. Print a complete copy-paste prompt for Cursor in your final response, with a link to your pushed work.

If core rig blockers remain, Cursor's first task is to fix those specific blockers. Otherwise assign the weapon milestone: inspect the actual model parts and implement a fully physical M1911 vertical slice, then the shared weapon architecture and the other requested families. Do not invent slider/magazine meshes or claim the assets have separate moving parts without inspection.

Tell Cursor exactly which existing components to extend, which assets/scenes to open, and which acceptance checks to run through its Unity MCP if available. Tell it to inspect its actual available tools rather than assume it has MCP. Make the handoff describe your final project state, not the original plan. Preserve this repository's reusable rig goal and require a clean, reviewable commit plus updated validation notes.
