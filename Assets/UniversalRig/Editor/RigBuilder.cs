using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UniversalRig.Editor
{
    // Generates the rig prefab and physics lab from Generated/SoldierSettings.asset. All output
    // lives in Assets/UniversalRig/Generated at fixed paths and is updated in place (GUIDs kept);
    // the settings asset is reused so tuning survives regeneration. Nothing outside that
    // folder is modified except the UniversalRigPhysics layer.
    public static class RigBuilder
    {
        private const string Output = "Assets/UniversalRig/Generated";
        private const string SettingsPath = Output + "/SoldierSettings.asset";
        private const string PrefabPath = Output + "/SoldierRig.prefab";
        private const string ScenePath = Output + "/RigPhysicsLab.unity";
        private const string FirstPersonMeshPath = Output + "/Soldier_FirstPerson.asset";
        private const string InputPath = Output + "/SoldierInput.inputactions";
        // Default fixed avatar (the Mixamo soldier) by GUID, so renaming the FBX doesn't break it.
        private const string DefaultAvatarGuid = "115d875de3f8af84bb820e10da65ef25";
        private const string PistolPrefabPath = "Assets/Weapons/Prefabs/M1911.prefab";

        [MenuItem("Tools/Universal Rig/Create Soldier Test Scene")]
        public static void CreateSoldierTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating the rig.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for compilation before generating the rig.");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader fadeShader = Shader.Find("UniversalRig/ComfortFade");
            if (!lit || !fadeShader) throw new InvalidOperationException("URP or the rig fade shader has not imported yet.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Output);
            RigSettings settings = LoadOrCreateSettings();
            EnsureRealAsset(AssetDatabase.GetAssetPath(settings.avatarModel));
            RigAvatarFitter.Fit(settings);
            int physicsLayer = EnsureLayer("UniversalRigPhysics");
            settings.environmentMask = ~(1 << physicsLayer);
            settings.grabbableMask = ~(1 << physicsLayer);
            EditorUtility.SetDirty(settings);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMaterial = LoadOrCreateMaterial("Floor", lit, new Color(0.17f, 0.21f, 0.25f));
            Material obstacleMaterial = LoadOrCreateMaterial("Obstacles", lit, new Color(0.36f, 0.42f, 0.46f));
            Material leftMaterial = LoadOrCreateMaterial("LeftHand", lit, new Color(0.1f, 0.65f, 0.9f));
            Material rightMaterial = LoadOrCreateMaterial("RightHand", lit, new Color(0.95f, 0.5f, 0.12f));
            Material propMaterial = LoadOrCreateMaterial("Props", lit, new Color(0.75f, 0.65f, 0.23f));
            Material climbMaterial = LoadOrCreateMaterial("Climbable", lit, new Color(0.2f, 0.55f, 0.3f));
            Material fadeMaterial = LoadOrCreateMaterial("ComfortFade", fadeShader, Color.clear);
            PhysicsMaterial bodyMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Output + "/Body.physicMaterial");
            if (!bodyMaterial)
            {
                bodyMaterial = new PhysicsMaterial("Rig low friction")
                {
                    dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(bodyMaterial, Output + "/Body.physicMaterial");
            }

            GameObject root = new GameObject("Universal Rig - Soldier");
            root.AddComponent<RigCollisionFilter>();
            RigInput input = root.AddComponent<RigInput>();
            input.actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputPath);
            if (!input.actionsAsset) input.actionsAsset = RigInputAssetFactory.Create(InputPath);
            input.trackingSpace = Child(root.transform, "Tracking Space").transform;
            input.head = Child(input.trackingSpace, "Head Camera").transform;
            input.head.localPosition = settings.eyePosition;
            Camera camera = input.head.gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 150f;
            input.head.gameObject.AddComponent<AudioListener>();
            input.leftTarget = Child(input.trackingSpace, "Left Tracking Target").transform;
            input.rightTarget = Child(input.trackingSpace, "Right Tracking Target").transform;
            input.leftTarget.localPosition = new Vector3(-0.22f, settings.standingEyeHeight - 0.55f, 0.3f);
            input.rightTarget.localPosition = new Vector3(0.22f, settings.standingEyeHeight - 0.55f, 0.3f);

            GameObject bodyObject = Child(root.transform, "Physics Body");
            bodyObject.layer = physicsLayer;
            bodyObject.transform.localPosition = Vector3.up * 0.02f;
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.mass = settings.bodyMass;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            CapsuleCollider capsule = bodyObject.AddComponent<CapsuleCollider>();
            capsule.radius = settings.bodyRadius;
            capsule.height = settings.standingEyeHeight + settings.headClearance - settings.stepHeight;
            capsule.center = Vector3.up * (settings.stepHeight + capsule.height * 0.5f);
            capsule.sharedMaterial = bodyMaterial;
            PhysicsRig rig = bodyObject.AddComponent<PhysicsRig>();
            rig.settings = settings;
            rig.input = input;
            rig.leftHand = MakeHand(root.transform, rig, true, physicsLayer, leftMaterial, bodyMaterial);
            rig.rightHand = MakeHand(root.transform, rig, false, physicsLayer, rightMaterial, bodyMaterial);

            GameObject soldier = (GameObject)PrefabUtility.InstantiatePrefab(settings.avatarModel, root.transform);
            soldier.name = "Avatar Visual";
            soldier.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = soldier.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.enabled = false;
            SetupFirstPersonRenderers(soldier, animator);
            AvatarPoseDriver pose = soldier.AddComponent<AvatarPoseDriver>();
            pose.avatar = animator;
            pose.rig = rig;
            pose.input = input;
            pose.leftHand = rig.leftHand;
            pose.rightHand = rig.rightHand;

            GameObject fade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fade.name = "Head Obstruction Fade";
            fade.transform.SetParent(input.head, false);
            fade.transform.localScale = Vector3.one * 0.5f;
            UnityEngine.Object.DestroyImmediate(fade.GetComponent<Collider>());
            Renderer fadeRenderer = fade.GetComponent<Renderer>();
            fadeRenderer.sharedMaterial = fadeMaterial;
            fadeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fadeRenderer.receiveShadows = false;
            fadeRenderer.enabled = false;
            HeadSafetyFade safety = root.AddComponent<HeadSafetyFade>();
            safety.rig = rig;
            safety.input = input;
            safety.fadeRenderer = fadeRenderer;
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);

            BuildLab(floorMaterial, obstacleMaterial, propMaterial, climbMaterial);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            Debug.Log($"Universal Rig generated: {ScenePath}\nPrefab: {PrefabPath}\nSettings: {SettingsPath}\n" +
                "Pistol is a grabbable prop; firing/reloading are the weapon milestone.");
        }

        [MenuItem("Tools/Universal Rig/Set PCVR Physics to 90 Hz")]
        public static void SetPhysicsRate()
        {
            // Unity 6.6 stores the timestep as a rational; the Time API writes it correctly.
            Time.fixedDeltaTime = 1f / 90f;
            Time.maximumDeltaTime = 0.1f;
            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            AssetDatabase.SaveAssets();
            Debug.Log($"Physics timestep {Time.fixedDeltaTime:F5} s ({1f / Time.fixedDeltaTime:F1} Hz). Profile CPU cost for the target headset.");
        }

        private static RigSettings LoadOrCreateSettings()
        {
            RigSettings settings = AssetDatabase.LoadAssetAtPath<RigSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<RigSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            if (!settings.avatarModel)
                settings.avatarModel = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(DefaultAvatarGuid));
            if (!settings.avatarModel)
                throw new InvalidOperationException("Assign RigSettings.avatarModel (a Humanoid model) in " + SettingsPath);
            return settings;
        }

        // Local player sees a headless copy (no head/helmet clipping into the camera); the full
        // mesh keeps rendering as shadows-only so the head shadow remains.
        private static void SetupFirstPersonRenderers(GameObject soldier, Animator animator)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            foreach (SkinnedMeshRenderer source in soldier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = RigAvatarFitter.SaveFirstPersonMesh(source, head, FirstPersonMeshPath);
                var copy = new GameObject(source.name + " (first person)");
                copy.transform.SetParent(source.transform.parent, false);
                copy.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
                copy.transform.localScale = source.transform.localScale;
                SkinnedMeshRenderer view = copy.AddComponent<SkinnedMeshRenderer>();
                view.sharedMesh = mesh;
                view.bones = source.bones;
                view.rootBone = source.rootBone;
                view.sharedMaterials = source.sharedMaterials;
                view.localBounds = source.localBounds;
                view.shadowCastingMode = ShadowCastingMode.Off;
                source.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                break; // ponytail: single-renderer avatars only (the soldier); loop per renderer if a future avatar is split.
            }
        }

        private static PhysicsHand MakeHand(Transform parent, PhysicsRig rig, bool left, int layer,
            Material material, PhysicsMaterial physicsMaterial)
        {
            RigSettings settings = rig.settings;
            RigInput input = rig.input;
            GameObject hand = Child(parent, left ? "Left Physical Hand" : "Right Physical Hand");
            hand.layer = layer;
            hand.transform.position = (left ? input.leftTarget : input.rightTarget).position;
            Rigidbody body = hand.AddComponent<Rigidbody>();
            body.mass = settings.handMass;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider collider = hand.AddComponent<BoxCollider>();
            collider.center = settings.handColliderCenter;
            collider.size = settings.handColliderSize;
            collider.sharedMaterial = physicsMaterial;
            // Collider preview for debugging; the soldier's own hands are the visible hands.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Debug Hand Collider (renderer off)";
            visual.transform.SetParent(hand.transform, false);
            visual.transform.localPosition = collider.center;
            visual.transform.localScale = collider.size;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            PhysicsHand result = hand.AddComponent<PhysicsHand>();
            result.settings = settings;
            result.input = input;
            result.rig = rig;
            result.isLeft = left;
            return result;
        }

        private static void BuildLab(Material floor, Material obstacle, Material prop, Material climb)
        {
            Cube("Floor", new Vector3(0f, -0.1f, 2f), new Vector3(12f, 0.2f, 12f), floor);
            Cube("Wall - hand collision test", new Vector3(0f, 1.2f, 2.5f), new Vector3(3f, 2.4f, 0.2f), obstacle);
            Cube("Low ceiling - crouch test (1.1 m)", new Vector3(-3f, 1.175f, 1f), new Vector3(2f, 0.15f, 2f), obstacle);
            Cube("Table", new Vector3(1.4f, 0.8f, 0.7f), new Vector3(1.8f, 0.15f, 1f), obstacle);
            for (int i = 0; i < 4; ++i)
                Cube("Step " + (i + 1) + " (15 cm rise)", new Vector3(3.5f, (i + 1) * 0.075f, 1.5f + i * 0.4f),
                    new Vector3(1.2f, (i + 1) * 0.15f, 0.4f), obstacle);
            Cube("Step landing", new Vector3(3.5f, 0.3f, 3.6f), new Vector3(1.2f, 0.6f, 1.4f), obstacle);
            GameObject ramp = Cube("Ramp (15 deg)", new Vector3(-1.5f, 0.2f, 4f), new Vector3(1.5f, 0.2f, 2f), obstacle);
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            Cube("Steep ramp (55 deg, not walkable)", new Vector3(1.5f, 0.5f, 5.5f), new Vector3(1.5f, 0.1f, 2f), obstacle)
                .transform.rotation = Quaternion.Euler(-55f, 0f, 0f);

            // Ladder to a 2.4 m platform; rails/rungs extend above the landing like a real ladder.
            Cube("Climb platform (2.4 m)", new Vector3(-4.5f, 1.2f, 5.4f), new Vector3(2f, 2.4f, 2f), obstacle);
            GameObject ladder = new GameObject("Ladder (Climbable)");
            ladder.AddComponent<Climbable>();
            ladder.transform.position = new Vector3(-4.5f, 0f, 4.25f);
            for (int side = -1; side <= 1; side += 2)
                Cube("Rail", new Vector3(-4.5f + side * 0.3f, 1.7f, 4.25f), new Vector3(0.05f, 3.4f, 0.05f), climb).transform.SetParent(ladder.transform, true);
            for (float y = 0.3f; y < 3.35f; y += 0.3f)
                Cube("Rung", new Vector3(-4.5f, y, 4.25f), new Vector3(0.6f, 0.035f, 0.035f), climb).transform.SetParent(ladder.transform, true);

            MakeProp("Light cube - 0.5 kg", new Vector3(1f, 1.05f, 0.6f), new Vector3(0.16f, 0.16f, 0.16f), 0.5f, prop);
            MakeProp("Heavy cube - 5 kg", new Vector3(1.5f, 1.05f, 0.6f), new Vector3(0.2f, 0.2f, 0.2f), 5f, prop);
            MakeProp("Very heavy crate - 40 kg", new Vector3(-1.5f, 0.25f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), 40f, prop);
            MakeProp("Two-hand bar", new Vector3(1.4f, 1.15f, 0.9f), new Vector3(0.8f, 0.07f, 0.07f), 2f, prop);
            TryMakePistolProp(new Vector3(1.9f, 1.05f, 0.6f));
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);
        }

        // Uses the user's authored M1911 prefab unmodified (real scale) under a physics root.
        // Colliders follow the separate Slide and Magazin parts; weapon behaviour is not here.
        private static void TryMakePistolProp(Vector3 position)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPrefabPath);
            if (!asset) { Debug.LogWarning("Universal Rig: M1911 prefab not found at " + PistolPrefabPath); return; }
            GameObject prop = new GameObject("M1911 - grab prop (no firing yet)");
            prop.transform.position = position;
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, prop.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (MeshFilter part in model.GetComponentsInChildren<MeshFilter>())
            {
                if (part.name != "Slide" && part.name != "Magazin") continue;
                GameObject colliderObject = new GameObject(part.name + " Collider");
                colliderObject.transform.SetParent(prop.transform, false);
                colliderObject.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
                BoxCollider box = colliderObject.AddComponent<BoxCollider>();
                Bounds bounds = part.sharedMesh.bounds;
                box.center = bounds.center;
                // Grip collider covers the frame around the magazine.
                box.size = part.name == "Magazin" ? Vector3.Max(bounds.size, new Vector3(0.03f, 0f, 0.035f)) : bounds.size;
            }
            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            prop.AddComponent<PhysicsGrabbable>();
        }

        private static GameObject MakeProp(string name, Vector3 position, Vector3 size, float mass, Material material)
        {
            GameObject prop = Cube(name, position, size, material);
            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            prop.AddComponent<PhysicsGrabbable>();
            return prop;
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        private static GameObject Child(Transform parent, string name)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Material LoadOrCreateMaterial(string name, Shader shader, Color color)
        {
            string path = Output + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; ++i)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = name;
                tags.ApplyModifiedProperties();
                return i;
            }
            throw new InvalidOperationException("No free user layer for UniversalRigPhysics.");
        }

        private static void EnsureRealAsset(string path)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Missing asset: " + path);
            using (var stream = File.OpenRead(path))
            {
                byte[] prefix = new byte[64];
                int length = stream.Read(prefix, 0, prefix.Length);
                if (System.Text.Encoding.ASCII.GetString(prefix, 0, length).StartsWith("version https://git-lfs.github.com/spec/v1"))
                    throw new InvalidOperationException("Git LFS pointer instead of model bytes: " + path + ". Download LFS objects before opening Unity.");
            }
        }
    }
}
