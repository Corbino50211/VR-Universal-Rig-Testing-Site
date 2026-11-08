# Claude: turn this into the final Cursor prompt after your work

This is a template, not a claim that the listed features already exist. Populate it from the actual final repository and Unity tests. Save the completed version as `Docs/CURSOR_HANDOFF.md` and give it to the user.

---

You are continuing Corbino's Universal Rig Unity project. Implement the next milestone in the actual project.

Repository: https://github.com/Corbino50211/VR-Universal-Rig-Testing-Site

Start from branch **[ACTUAL PUSHED BRANCH]**, commit **[ACTUAL COMMIT]**. Preserve any local work. Read local/repository agent instructions, `Docs/RIG_FOUNDATION.md`, `Docs/RIG_VALIDATION.md`, and this handoff. Inspect the code and available Unity MCP tools before editing.

Target: Unity 6000.6.5f1 URP, PCVR first, one fixed Mixamo soldier avatar (`Assets/Ch15_nonPBR@T-Pose.fbx`). The rig must remain reusable in other games, with another fixed avatar configured in the editor later. No runtime avatar swapping.

Current verified state:

- Scene/prefab/settings: [EXACT PATHS]
- Input/controller setup: [EXACT DEVICE, BINDINGS AND OWNERSHIP]
- Core classes/interfaces you must extend: [EXACT NAMES AND RESPONSIBILITIES]
- Unity compile and automated tests: [OBSERVED RESULTS]
- Human headset tests: [OBSERVED RESULTS OR NOT RUN]
- Outstanding blockers: [SPECIFIC REPRO STEPS, EXPECTED/ACTUAL BEHAVIOR]
- Areas you may edit and concurrent work to avoid: [ACTUAL SCOPE]

Your task:

1. Resolve the listed core blockers first. Keep the verified physics rig functional and use its existing physical grabbing and input ownership.
2. Start with the user's authored prefabs under `Assets/Weapons/Prefabs`, then inspect their source weapon models in `Assets/Low Poly Weapons VOL.1` and `Assets/LowPoly/Low Poly Weapons VOL.2`. Record which magazines, slides, bolts, pumps and triggers are separate mesh objects. Prepare only necessary model variants if something is fused. Preserve original imported models.
3. Build the first complete physical M1911: primary/support grips, stable collisions, controller trigger input, physical magazine insertion/removal, slide operation, explicit chamber/ammunition state, firing only when a valid round is chambered, slide lock/empty behavior, recoil applied through the physical weapon, and visible/audio/haptic feedback using verified available assets/APIs.
4. Keep weapon logic separate from game-specific damage via a documented interface/event. Choose and document a practical projectile/hit detection model; handle muzzle obstruction and exclude the local player's own colliders correctly. Do not damage arbitrary objects by type-name guessing.
5. After the pistol passes testing, extend the shared architecture to SMGs, rifles and shotguns with their appropriate fire modes, reload/chamber actions, two-hand handling and shot spread. Keep configuration in data assets. Do not create four unrelated copies of the same controller.
6. Use physics constraints for held weapons. Avoid transform parenting that bypasses mass/collision response. Keep recoil and two-hand forces bounded and test whether the player can unintentionally propel themselves.
7. Save finished prefabs and a test range with ammunition and clear controls. Verify persistence after reopening the scene and restarting Play Mode.

Validation must cover physical grab/release, hand swapping, tracking loss, wall obstruction, one/two-hand recoil, dropping a loaded weapon, empty firing, partial reload interruption, magazine swaps, ammo accounting, shotgun loading/cycling, frame-rate variation and zero Console errors. Distinguish simulated checks from actual headset checks. Do not claim tests you could not run.

Preserve existing work, GUIDs and assets. Use the installed package APIs. If a required sound or model part is unavailable, report it precisely and provide an explicitly labeled temporary substitute where appropriate. Finish with a pushed commit/PR, complete files, updated validation notes and a concise explanation of what the user can test.
