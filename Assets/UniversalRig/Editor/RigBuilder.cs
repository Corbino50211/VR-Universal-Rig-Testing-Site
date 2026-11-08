using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UniversalRig.Editor
{
    public static class RigBuilder
    {
        private const string Output = "Assets/UniversalRig/Generated";
        private const string SoldierPath = "Assets/Ch15_nonPBR@T-Pose.fbx";
        private const string PistolPath = "Assets/Low Poly Weapons VOL.1/Models/M1911.fbx";

        [MenuItem("Tools/Universal Rig/Create Soldier Test Scene")]
        public static void CreateSoldierTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating the rig.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for compilation before generating the rig.");
            EnsureRealAsset(SoldierPath);
            GameObject soldierAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPath);
            if (!soldierAsset) throw new InvalidOperationException("Soldier FBX did not import. Inspect the Unity Console and LFS checkout.");
            Animator sourceAnimator = soldierAsset.GetComponent<Animator>();
            if (!sourceAnimator || !sourceAnimator.avatar || !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman)
                throw new InvalidOperationException("Soldier must import as a valid Humanoid before rig generation.");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader fadeShader = Shader.Find("UniversalRig/ComfortFade");
            if (!lit || !fadeShader) throw new InvalidOperationException("URP or the rig fade shader has not imported yet.");
            // New outputs always use unique paths. Save existing scene edits before switching scenes.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Output);
            int physicsLayer = EnsureLayer("UniversalRigPhysics");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var settings = ScriptableObject.CreateInstance<RigSettings>();
            settings.environmentMask = ~(1 << physicsLayer);
            settings.grabbableMask = ~(1 << physicsLayer);
            string settingsPath = Unique("SoldierSettings.asset");
            AssetDatabase.CreateAsset(settings, settingsPath);

            Material floorMaterial = MakeMaterial("Floor", lit, new Color(0.17f, 0.21f, 0.25f));
            Material obstacleMaterial = MakeMaterial("Obstacles", lit, new Color(0.36f, 0.42f, 0.46f));
            Material leftMaterial = MakeMaterial("LeftHand", lit, new Color(0.1f, 0.65f, 0.9f));
            Material rightMaterial = MakeMaterial("RightHand", lit, new Color(0.95f, 0.5f, 0.12f));
            Material propMaterial = MakeMaterial("Props", lit, new Color(0.75f, 0.65f, 0.23f));
            Material fadeMaterial = MakeMaterial("ComfortFade", fadeShader, Color.clear);
            var bodyMaterial = new PhysicsMaterial("Rig low friction")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(bodyMaterial, Unique("Body.physicMaterial"));

            GameObject root = new GameObject("Universal Rig - Soldier");
            root.AddComponent<RigCollisionFilter>();
            RigInput input = root.AddComponent<RigInput>();
            input.actionsAsset = RigInputAssetFactory.Create(Unique("SoldierInput.inputactions"));
            input.trackingSpace = Child(root.transform, "Tracking Space").transform;
            input.head = Child(input.trackingSpace, "Head Camera").transform;
            input.head.localPosition = Vector3.up * settings.standingEyeHeight;
            Camera camera = input.head.gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 150f;
            input.head.gameObject.AddComponent<AudioListener>();
            input.leftTarget = Child(input.trackingSpace, "Left Tracking Target").transform;
            input.rightTarget = Child(input.trackingSpace, "Right Tracking Target").transform;
            input.leftTarget.localPosition = new Vector3(-0.25f, 1.2f, 0.35f);
            input.rightTarget.localPosition = new Vector3(0.25f, 1.2f, 0.35f);

            GameObject bodyObject = Child(root.transform, "Physics Body");
            bodyObject.layer = physicsLayer;
            bodyObject.transform.localPosition = Vector3.up * 0.02f;
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.mass = settings.bodyMass;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            CapsuleCollider capsule = bodyObject.AddComponent<CapsuleCollider>();
            capsule.radius = settings.bodyRadius;
            capsule.height = settings.standingEyeHeight + settings.headClearance;
            capsule.center = Vector3.up * (capsule.height * 0.5f);
            capsule.sharedMaterial = bodyMaterial;
            PhysicsRig rig = bodyObject.AddComponent<PhysicsRig>();
            rig.settings = settings;
            rig.input = input;
            PhysicsHand left = MakeHand(root.transform, input, settings, true, physicsLayer, leftMaterial, bodyMaterial);
            PhysicsHand right = MakeHand(root.transform, input, settings, false, physicsLayer, rightMaterial, bodyMaterial);
            rig.leftHand = left;
            rig.rightHand = right;

            GameObject soldier = (GameObject)PrefabUtility.InstantiatePrefab(soldierAsset, root.transform);
            soldier.name = "Soldier Visual";
            soldier.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            soldier.transform.localScale = Vector3.one;
            Animator animator = soldier.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.enabled = false;
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone)
            {
                settings.standingEyeHeight = soldier.transform.InverseTransformPoint(headBone.position).y + 0.08f;
                settings.maximumHeight = Mathf.Max(2.1f, settings.standingEyeHeight + 0.2f);
                input.head.localPosition = Vector3.up * settings.standingEyeHeight;
                capsule.height = settings.standingEyeHeight + settings.headClearance;
                capsule.center = Vector3.up * (capsule.height * 0.5f);
            }
            AvatarPoseDriver pose = soldier.AddComponent<AvatarPoseDriver>();
            pose.avatar = animator;
            pose.rig = rig;
            pose.input = input;
            pose.leftHand = left;
            pose.rightHand = right;
            // Wrist offsets and first-person head visibility need the real mesh/controller
            // inspection step. Keep the visual available but opt-in until Claude fits it.
            soldier.SetActive(false);

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
            string prefabPath = Unique("SoldierRig.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);

            Cube("Floor", new Vector3(0f, -0.1f, 2f), new Vector3(12f, 0.2f, 12f), floorMaterial);
            Cube("Wall - hand collision test", new Vector3(0f, 1.2f, 2.5f), new Vector3(3f, 2.4f, 0.2f), obstacleMaterial);
            Cube("Low ceiling - crouch test", new Vector3(-3f, 1.1f, 1f), new Vector3(2f, 0.15f, 2f), obstacleMaterial);
            Cube("Table", new Vector3(1.4f, 0.8f, 0.7f), new Vector3(1.8f, 0.15f, 1f), obstacleMaterial);
            for (int i = 0; i < 4; ++i)
                Cube("Step " + (i + 1), new Vector3(3.5f, (i + 1) * 0.075f, 1.5f + i * 0.4f),
                    new Vector3(1.2f, (i + 1) * 0.15f, 0.4f), obstacleMaterial);
            GameObject ramp = Cube("Ramp", new Vector3(-1.5f, 0.2f, 4f), new Vector3(1.5f, 0.2f, 2f), obstacleMaterial);
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            MakeProp("Light cube - 0.5 kg", new Vector3(1f, 1.05f, 0.6f), new Vector3(0.16f, 0.16f, 0.16f), 0.5f, propMaterial);
            MakeProp("Heavy cube - 5 kg", new Vector3(1.5f, 1.05f, 0.6f), new Vector3(0.2f, 0.2f, 0.2f), 5f, propMaterial);
            MakeProp("Two-hand bar", new Vector3(1.4f, 1.15f, 0.9f), new Vector3(0.8f, 0.07f, 0.07f), 2f, propMaterial);
            TryMakePistolProp(new Vector3(1.9f, 1.05f, 0.6f));
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            string scenePath = Unique("RigPhysicsLab.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = root;
            Debug.Log($"Universal Rig generated: {scenePath}\nPrefab: {prefabPath}\nSettings: {settingsPath}\n" +
                "Next: validate OpenXR, set 90 Hz physics, run tests, fit/enable Soldier Visual and test in headset. " +
                "Pistol is a grabbable prop; firing/reloading and full-body physics are later milestones.");
        }

        [MenuItem("Tools/Universal Rig/Set PCVR Physics to 90 Hz")]
        public static void SetPhysicsRate()
        {
            var time = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            time.FindProperty("Fixed Timestep").floatValue = 1f / 90f;
            time.FindProperty("Maximum Allowed Timestep").floatValue = 0.1f;
            time.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Physics set to 90 Hz. Profile CPU cost and adjust for the target headset; this is not a frame-rate lock.");
        }

        private static PhysicsHand MakeHand(Transform parent, RigInput input, RigSettings settings,
            bool left, int layer, Material material, PhysicsMaterial physicsMaterial)
        {
            GameObject hand = Child(parent, left ? "Left Physical Hand" : "Right Physical Hand");
            hand.layer = layer;
            hand.transform.position = (left ? input.leftTarget : input.rightTarget).position;
            Rigidbody body = hand.AddComponent<Rigidbody>();
            body.mass = settings.handMass;
            body.useGravity = false;
            BoxCollider collider = hand.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.075f, 0.055f, 0.12f);
            collider.sharedMaterial = physicsMaterial;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Debug Hand Visual";
            visual.transform.SetParent(hand.transform, false);
            visual.transform.localScale = collider.size;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;
            PhysicsHand result = hand.AddComponent<PhysicsHand>();
            result.settings = settings;
            result.input = input;
            result.isLeft = left;
            return result;
        }

        private static void TryMakePistolProp(Vector3 position)
        {
            if (!File.Exists(PistolPath)) return;
            try { EnsureRealAsset(PistolPath); }
            catch (InvalidOperationException exception) { Debug.LogWarning(exception.Message); return; }
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPath);
            if (!asset) return;
            GameObject prop = new GameObject("M1911 - grab prototype (no firing yet)");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, prop.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { UnityEngine.Object.DestroyImmediate(prop); return; }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest <= 0.001f) { UnityEngine.Object.DestroyImmediate(prop); return; }
            // Temporary prop dimensions only; exact grip/slide/magazine fitting is a Unity task.
            model.transform.localScale *= 0.22f / longest;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            BoxCollider collider = prop.AddComponent<BoxCollider>();
            collider.center = prop.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            prop.AddComponent<PhysicsGrabbable>();
            prop.transform.position = position;
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

        private static string Unique(string name) => AssetDatabase.GenerateUniqueAssetPath(Output + "/" + name);

        private static Material MakeMaterial(string name, Shader shader, Color color)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Unique(name + ".mat"));
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
