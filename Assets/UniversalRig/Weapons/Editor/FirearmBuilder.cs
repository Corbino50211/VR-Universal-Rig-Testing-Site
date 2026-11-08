using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniversalRig;

namespace UniversalRig.Weapons.Editor
{
    public static class FirearmBuilder
    {
        const string Root = "Assets/UniversalRig/Weapons/Generated";

        [MenuItem("Tools/Universal Rig/Build Firearms")]
        public static void BuildAllAndPlaceRange()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before building firearms.");
            BuildAssets();
            PlaceRange();
        }

        public static void BuildAssets()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Profiles");
            EnsureFolder(Root + "/Prefabs");
            FirearmProfile pistol = Profile("M1911", p =>
            {
                p.displayName = "M1911"; p.ammoId = "45acp"; p.cycle = FirearmCycle.SelfLoading;
                p.allowSemi = true; p.allowAuto = false; p.lockOpenOnEmpty = true;
                p.recoilImpulse = 0.4f; p.recoilImpulseMax = 0.7f; p.roundsPerMinute = 400f;
            });
            FirearmProfile uzi = Profile("Uzi", p =>
            {
                p.displayName = "Uzi"; p.ammoId = "9mm"; p.cycle = FirearmCycle.OpenBolt;
                p.allowSemi = true; p.allowAuto = true; p.lockOpenOnEmpty = false; p.roundsPerMinute = 600f;
                p.recoilImpulse = 0.28f; p.recoilImpulseMax = 0.5f;
            });
            FirearmProfile ak = Profile("AK74", p =>
            {
                p.displayName = "AK74"; p.ammoId = "545"; p.cycle = FirearmCycle.SelfLoading;
                p.allowSemi = true; p.allowAuto = true; p.roundsPerMinute = 600f;
                p.recoilImpulse = 0.45f; p.recoilImpulseMax = 0.75f;
            });
            FirearmProfile m4 = Profile("M4", p =>
            {
                p.displayName = "M4"; p.ammoId = "556"; p.cycle = FirearmCycle.SelfLoading;
                p.allowSemi = true; p.allowAuto = true; p.roundsPerMinute = 700f;
                p.recoilImpulse = 0.4f; p.recoilImpulseMax = 0.7f;
            });
            FirearmProfile shot = Profile("BenelliM4", p =>
            {
                p.displayName = "Benelli M4"; p.ammoId = "12ga"; p.cycle = FirearmCycle.Pump;
                p.allowSemi = true; p.allowAuto = false; p.lockOpenOnEmpty = false; p.tubeCapacity = 6;
                p.pellets = 8; p.spreadDegrees = 5f; p.recoilImpulse = 0.55f; p.recoilImpulseMax = 0.8f;
            });

            BuildMagazine("Mag_45", "Assets/Weapons/Prefabs/M1911.prefab", "Magazin", "45acp", 7);
            BuildMagazine("Mag_9", "Assets/Weapons/Prefabs/Uzi.prefab", "Magazin", "9mm", 32);
            BuildMagazine("Mag_545", "Assets/Weapons/Prefabs/AK74.prefab", "Mag", "545", 30);
            BuildMagazine("Mag_556", "Assets/Weapons/Prefabs/M4_8.prefab", "Mag", "556", 30);
            BuildShell();

            BuildGun("M1911", "Assets/Weapons/Prefabs/M1911.prefab", pistol, 1.1f, "Slide", "Magazin", 0.042f);
            BuildGun("Uzi", "Assets/Weapons/Prefabs/Uzi.prefab", uzi, 1.7f, "Bolt", "Magazin", 0.04f);
            BuildGun("AK74", "Assets/Weapons/Prefabs/AK74.prefab", ak, 3.3f, "Bolt", "Mag", 0.038f);
            BuildGun("M4", "Assets/Weapons/Prefabs/M4_8.prefab", m4, 3.1f, "Bolt", "Mag", 0.04f);
            BuildGun("BenelliM4", "Assets/Weapons/Prefabs/Bennelli_M4.prefab", shot, 3.8f, "Pump", null, 0.07f);
            AssetDatabase.SaveAssets();
        }

        public static void PlaceRange()
        {
            Material benchMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UniversalRig/Generated/Obstacles.mat");
            GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Firearm bench";
            bench.transform.position = new Vector3(0f, 0.8f, -2.35f);
            bench.transform.localScale = new Vector3(7.2f, 0.12f, 1.3f);
            if (benchMat) bench.GetComponent<Renderer>().sharedMaterial = benchMat;

            PlaceGun("M1911", new Vector3(1.85f, 1.02f, 0.55f), Vector3.right);
            PlaceMagazine("Mag_45", new Vector3(1.45f, 1.0f, 0.85f), 7);
            PlaceMagazine("Mag_45", new Vector3(1.6f, 1.0f, 0.85f), 3);
            PlaceGun("Uzi", new Vector3(-2.4f, 0.98f, -2.35f), Vector3.back);
            PlaceMagazine("Mag_9", new Vector3(-2.4f, 0.98f, -1.85f), 32);
            PlaceGun("AK74", new Vector3(-0.6f, 0.98f, -2.35f), Vector3.back);
            PlaceMagazine("Mag_545", new Vector3(-0.6f, 0.98f, -1.85f), 30);
            PlaceGun("M4", new Vector3(1.1f, 0.98f, -2.35f), Vector3.back);
            PlaceMagazine("Mag_556", new Vector3(1.1f, 0.98f, -1.85f), 30);
            PlaceGun("BenelliM4", new Vector3(2.6f, 0.98f, -2.35f), Vector3.back);
            for (int i = 0; i < 6; ++i)
                PlaceShell(new Vector3(2.15f + i * 0.08f, 0.95f, -1.9f));

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Firearm target";
            target.transform.position = new Vector3(0f, 1.15f, -3.55f);
            target.transform.localScale = new Vector3(1.4f, 1.1f, 0.06f);
            target.AddComponent<HitTarget>();
            if (benchMat) target.GetComponent<Renderer>().sharedMaterial = benchMat;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Muzzle block";
            block.transform.position = new Vector3(-3.3f, 1.0f, -2.35f);
            block.transform.localScale = new Vector3(0.08f, 0.4f, 0.4f);
            Label(new Vector3(0f, 1.7f, -1.55f),
                "Firearms (behind you and on the right-hand table)\nGrip grabs. Trigger fires. Guns start empty.\nSeat a magazine and release it, then rack the slide, bolt, or pump.\nOther hand on the foregrip braces. Pull the moving part to cycle.\nShotgun: push a red TEMPORARY shell into the port, then pump.\nOther hand's trigger on the gold selector switches SEMI / AUTO.\nShot sounds are temporary synthesized clicks. No firearm audio in the project.\nHeadset rig checks (RIG_VALIDATION section 3) are still open.");
            Label(new Vector3(-3.3f, 1.35f, -2.35f), "Muzzle block");
            Label(new Vector3(0f, 1.8f, -3.55f), "Target");
        }

        static void PlaceGun(string name, Vector3 position, Vector3 forward)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");
            if (!prefab) { Debug.LogWarning("Missing firearm prefab " + name); return; }
            GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Firearm firearm = gun.GetComponent<Firearm>();
            Quaternion turn = Quaternion.FromToRotation(firearm.muzzle.forward, forward.normalized);
            gun.transform.rotation = turn * gun.transform.rotation;
            gun.transform.position += position - firearm.muzzle.position;
            gun.transform.position += Vector3.up * 0.05f;
        }

        static void PlaceMagazine(string name, Vector3 position, int rounds)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");
            if (!prefab) return;
            GameObject mag = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            mag.transform.position = position;
            mag.GetComponent<Magazine>().rounds = rounds;
        }

        static void PlaceShell(Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/TEMPORARY_Shell.prefab");
            if (!prefab) return;
            GameObject shell = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            shell.transform.position = position;
        }

        static void Label(Vector3 position, string text)
        {
            var go = new GameObject("Label");
            go.transform.position = position;
            WorldText(go, text, 0.09f, Color.white, new Vector2(3.4f, 1.6f));
        }

        static TextMeshPro WorldText(GameObject go, string text, float size, Color color, Vector2 rect)
        {
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.font = LabelFont();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = rect;
            return tmp;
        }

        static TMP_FontAsset LabelFont()
        {
            const string path = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (!font) throw new System.InvalidOperationException("Missing TextMesh Pro font at " + path);
            return font;
        }

        // Swaps leftover legacy TextMesh signs in the open lab for TextMeshPro.
        public static void ReplaceSceneLabels()
        {
            TMP_FontAsset font = LabelFont();
            TextMesh[] legacy = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include);
            foreach (TextMesh old in legacy)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(old)) continue;
                GameObject go = old.gameObject;
                string text = old.text;
                Color color = old.color;
                Vector3 position = go.transform.position;
                Quaternion rotation = go.transform.rotation;
                Transform parent = go.transform.parent;
                Object.DestroyImmediate(old);
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer) Object.DestroyImmediate(renderer);
                MeshFilter filter = go.GetComponent<MeshFilter>();
                if (filter) Object.DestroyImmediate(filter);
                TextMeshPro tmp = WorldText(go, text, go.name == "Fire mode" ? 0.045f : 0.09f, color, go.name == "Fire mode" ? new Vector2(0.4f, 0.12f) : new Vector2(3.4f, 1.6f));
                tmp.font = font;
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(position, rotation);
            }
        }

        static FirearmProfile Profile(string name, System.Action<FirearmProfile> fill)
        {
            string path = Root + "/Profiles/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<FirearmProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<FirearmProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            fill(profile);
            profile.maxRecoilSpeed = 0.4f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void BuildMagazine(string name, string gunPath, string part, string ammo, int capacity)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
            Transform meshTransform = FindPart(source, part);
            if (!meshTransform) throw new System.InvalidOperationException(gunPath + " has no mesh named " + part);
            Mesh mesh = meshTransform.GetComponent<MeshFilter>().sharedMesh;
            LogPart(gunPath, meshTransform);
            var root = new GameObject(name);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
            var visual = new GameObject("Mesh");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = -mesh.bounds.center;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer sourceRenderer = meshTransform.GetComponent<MeshRenderer>();
            visual.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer ? sourceRenderer.sharedMaterials : new Material[0];
            root.AddComponent<PhysicsGrabbable>();
            Magazine magazine = root.AddComponent<Magazine>();
            magazine.ammoId = ammo;
            magazine.capacity = capacity;
            magazine.rounds = capacity;
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static Material ShellMaterial()
        {
            string path = Root + "/TEMPORARY_Shell.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.72f, 0.16f, 0.12f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void BuildShell()
        {
            // ponytail: no shotshell mesh in the project. A labelled box stands in for 12 gauge.
            var root = new GameObject("TEMPORARY shell (no shotshell mesh)");
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.02f, 0.02f, 0.06f);
            root.transform.localScale = Vector3.one;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "TEMPORARY shell mesh";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = box.size;
            visual.GetComponent<Renderer>().sharedMaterial = ShellMaterial();
            root.AddComponent<PhysicsGrabbable>();
            Magazine magazine = root.AddComponent<Magazine>();
            magazine.ammoId = "12ga";
            magazine.capacity = 1;
            magazine.rounds = 1;
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/TEMPORARY_Shell.prefab");
            Object.DestroyImmediate(root);
        }

        static void BuildGun(string name, string sourcePath, FirearmProfile profile, float mass, string actionPart,
            string magazinePart, float travel)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) throw new System.InvalidOperationException("Missing weapon prefab " + sourcePath);
            GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(source);
            gun.name = name;
            Transform actionMesh = FindPart(gun, actionPart);
            Transform magazineMesh = string.IsNullOrEmpty(magazinePart) ? null : FindPart(gun, magazinePart);
            Transform trigger = FindPart(gun, "Trigger");
            if (!actionMesh || !trigger)
                throw new System.InvalidOperationException(sourcePath + " needs separate " + actionPart + " and Trigger meshes.");
            LogPart(sourcePath, actionMesh);
            if (magazineMesh) LogPart(sourcePath, magazineMesh);
            LogPart(sourcePath, trigger);

            Vector3 barrel = Barrel(actionMesh, trigger);
            Vector3 gunUp = GunUp(barrel, actionMesh, magazineMesh, trigger);
            // OpenXR grip frame from GripPoint: +Z along the barrel, +Y back toward the wrist.
            Quaternion gripRot = Quaternion.LookRotation(barrel, -gunUp);

            Rigidbody body = gun.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            PhysicsGrabbable grabbable = gun.AddComponent<PhysicsGrabbable>();
            grabbable.maximumHands = 2;
            AddBox(gun.transform, actionMesh, actionPart + " collider");
            Transform frame = FrameMesh(gun, actionMesh, magazineMesh, trigger);
            if (frame && frame != actionMesh) AddBox(gun.transform, frame, "Frame collider");

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun.transform, true);
            muzzle.SetPositionAndRotation(MuzzlePoint(gun, barrel, magazineMesh), Quaternion.LookRotation(barrel, gunUp));

            Transform primary = Grip(gun.transform, "Primary grip", trigger.position - barrel * 0.04f - gunUp * 0.03f, gripRot, GripPoint.Role.Primary);
            Vector3 bracePos = (magazineMesh ? WorldCenter(magazineMesh) : trigger.position) + barrel * (travel > 0.05f ? 0.16f : 0.08f);
            Grip(gun.transform, "Support grip", bracePos, gripRot, GripPoint.Role.Support);

            Transform marker = new GameObject(actionPart + " handle marker").transform;
            marker.SetParent(gun.transform, true);
            marker.SetPositionAndRotation(HandlePoint(actionMesh, barrel), gripRot);

            Transform selector = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            selector.name = "Fire mode selector";
            selector.SetParent(gun.transform, true);
            selector.position = primary.position - barrel * 0.05f + gunUp * 0.035f;
            selector.localScale = Vector3.one * 0.018f;

            Light light = new GameObject("Muzzle flash").AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.4f;
            light.intensity = 2.5f;
            light.enabled = false;
            light.transform.SetParent(muzzle, false);

            TextMeshPro mode = WorldText(new GameObject("Fire mode"), "SEMI", 0.045f, new Color(1f, 0.85f, 0.2f), new Vector2(0.4f, 0.12f));
            mode.transform.SetParent(gun.transform, true);
            mode.transform.position = primary.position + gunUp * 0.06f;

            Firearm firearm = gun.AddComponent<Firearm>();
            firearm.profile = profile;
            firearm.muzzle = muzzle;
            firearm.triggerVisual = trigger;
            firearm.hammerVisual = FindPart(gun, "Hammer");
            firearm.selector = selector;
            firearm.modeLabel = mode;
            firearm.muzzleLight = light;

            ActionPart action = gun.AddComponent<ActionPart>();
            action.firearm = firearm;
            action.visual = actionMesh;
            action.marker = marker;
            action.openAxisLocal = gun.transform.InverseTransformDirection(-barrel);
            action.travel = travel;
            action.threshold = profile.actionThreshold;
            firearm.action = action;

            if (magazineMesh)
            {
                Transform seat = new GameObject("Magazine seat").transform;
                seat.SetParent(gun.transform, true);
                seat.SetPositionAndRotation(WorldCenter(magazineMesh), magazineMesh.rotation);
                MagazineSocket socket = gun.AddComponent<MagazineSocket>();
                socket.firearm = firearm;
                socket.seat = seat;
                socket.ammoId = profile.ammoId;
                socket.angle = 40f;
                firearm.socket = socket;
                MeshRenderer magazineRenderer = magazineMesh.GetComponent<MeshRenderer>();
                if (magazineRenderer) magazineRenderer.enabled = false;
            }
            else if (profile.tubeCapacity > 0)
            {
                Transform seat = new GameObject("Shell port").transform;
                seat.SetParent(gun.transform, true);
                seat.SetPositionAndRotation(trigger.position + barrel * 0.08f - gunUp * 0.045f, Quaternion.LookRotation(barrel, gunUp));
                MagazineSocket socket = gun.AddComponent<MagazineSocket>();
                socket.firearm = firearm;
                socket.seat = seat;
                socket.ammoId = profile.ammoId;
                socket.loadsTube = true;
                socket.distance = 0.05f;
                socket.angle = 170f;
                firearm.socket = socket;
            }

            Renderer[] renderers = gun.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; ++i) bounds.Encapsulate(renderers[i].bounds);
                body.centerOfMass = gun.transform.InverseTransformPoint(bounds.center);
            }

            string path = Root + "/Prefabs/" + name + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(gun, path);
            Debug.Log("Universal Rig firearm " + name + " saved as " + PrefabUtility.GetPrefabAssetType(saved) + " at " + path);
            Object.DestroyImmediate(gun);
        }

        static Transform Grip(Transform parent, string objectName, Vector3 position, Quaternion rotation, GripPoint.Role role)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            GripPoint grip = go.AddComponent<GripPoint>();
            grip.role = role;
            grip.fingerCurl = 1f;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.04f, 0.05f, 0.04f);
            return go.transform;
        }

        static void AddBox(Transform parent, Transform meshTransform, string objectName)
        {
            Mesh mesh = meshTransform.GetComponent<MeshFilter>().sharedMesh;
            if (!mesh) return;
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(meshTransform.position, meshTransform.rotation);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
        }

        static Transform FrameMesh(GameObject gun, Transform action, Transform magazine, Transform trigger)
        {
            foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.transform == action || filter.transform == magazine || filter.transform == trigger) continue;
                if (filter.sharedMesh && filter.sharedMesh.bounds.size.magnitude > 0.05f) return filter.transform;
            }
            return null;
        }

        static Vector3 Barrel(Transform action, Transform trigger)
        {
            Bounds bounds = action.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 extents = bounds.extents;
            Vector3 axis = Vector3.forward;
            float extent = extents.z;
            if (extents.x > extent) { axis = Vector3.right; extent = extents.x; }
            if (extents.y > extent) axis = Vector3.up;
            Vector3 world = action.TransformDirection(axis).normalized;
            Vector3 center = action.TransformPoint(bounds.center);
            Vector3 endA = center + world * extent;
            Vector3 endB = center - world * extent;
            return (endA - trigger.position).sqrMagnitude > (endB - trigger.position).sqrMagnitude
                ? (endA - center).normalized : (endB - center).normalized;
        }

        static Vector3 GunUp(Vector3 barrel, Transform action, Transform magazine, Transform trigger)
        {
            Vector3 from = magazine ? WorldCenter(magazine) : trigger.position;
            Vector3 hint = action.TransformPoint(action.GetComponent<MeshFilter>().sharedMesh.bounds.center) - from;
            Vector3 up = Vector3.ProjectOnPlane(hint, barrel);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.ProjectOnPlane(Vector3.up, barrel);
            return up.normalized;
        }

        static Vector3 HandlePoint(Transform action, Vector3 barrel)
        {
            Vector3 center = WorldCenter(action);
            return center - barrel * 0.02f;
        }

        static Vector3 MuzzlePoint(GameObject gun, Vector3 barrel, Transform magazine)
        {
            float max = 0.05f;
            Vector3 origin = gun.transform.position;
            foreach (Renderer renderer in gun.GetComponentsInChildren<Renderer>())
            {
                if (magazine && renderer.transform == magazine) continue;
                Bounds bounds = renderer.bounds;
                Vector3 ext = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = bounds.center + Vector3.Scale(ext, new Vector3(x, y, z));
                            max = Mathf.Max(max, Vector3.Dot(corner - origin, barrel));
                        }
            }
            return origin + barrel * (max + 0.015f);
        }

        static Vector3 WorldCenter(Transform meshTransform)
        {
            Mesh mesh = meshTransform.GetComponent<MeshFilter>().sharedMesh;
            return meshTransform.TransformPoint(mesh.bounds.center);
        }

        static Transform FindPart(GameObject root, string part)
        {
            if (!root) return null;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == part && transform.GetComponent<MeshFilter>()) return transform;
            return null;
        }

        static void LogPart(string path, Transform part)
        {
            Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
            Debug.Log("Firearm inventory " + path + " / " + part.name
                + " separateMesh=true verts=" + mesh.vertexCount
                + " localPos=" + part.localPosition.ToString("F3")
                + " boundsCenter=" + mesh.bounds.center.ToString("F3")
                + " extents=" + mesh.bounds.extents.ToString("F3"));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
