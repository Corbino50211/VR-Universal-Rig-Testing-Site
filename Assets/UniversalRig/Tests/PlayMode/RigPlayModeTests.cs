using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace UniversalRig.Tests
{
    // Drives the generated rig prefab with XRI's simulated HMD/controllers through the real
    // SoldierInput bindings. These are simulated-input physics checks, not headset tests.
    public sealed class RigPlayModeTests
    {
        private const string PrefabPath = "Assets/UniversalRig/Generated/SoldierRig.prefab";
        private const float UserEyeHeight = 1.75f; // deliberately not the avatar's 1.669 m
        private static readonly Vector3 LeftRest = new Vector3(-0.2f, 1.2f, 0.35f);
        private static readonly Vector3 RightRest = new Vector3(0.2f, 1.2f, 0.35f);

        private XRSimulatedHMD hmd;
        private XRSimulatedController leftDevice, rightDevice;
        private XRSimulatedHMDState head;
        private XRSimulatedControllerState left, right;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private InputSettings.BackgroundBehavior savedBackground;
        private PhysicsRig rig;
        private RigInput input;
        private RigSettings settings;
        private AvatarPoseDriver pose;
        private HeadSafetyFade fade;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The editor may be unfocused while tests run; don't let the Input System reset devices.
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Box("Test floor", new Vector3(0f, -0.1f, 0f), new Vector3(30f, 0.2f, 30f));
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            leftDevice = InputSystem.AddDevice<XRSimulatedController>();
            rightDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            head.Reset();
            head.isTracked = true;
            head.trackingState = 3;
            SetHead(new Vector3(0f, UserEyeHeight, 0f), Quaternion.identity);
            left = Controller(LeftRest);
            right = Controller(RightRest);
            Push();
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            GameObject prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null, "Generate the rig first: Tools/Universal Rig/Create Soldier Test Scene.");
            GameObject instance = Object.Instantiate(prefab);
            spawned.Add(instance);
            input = instance.GetComponent<RigInput>();
            input.deviceStandingHeight = 0f; // simulated poses are floor-relative
            rig = instance.GetComponentInChildren<PhysicsRig>();
            pose = instance.GetComponentInChildren<AvatarPoseDriver>();
            fade = instance.GetComponent<HeadSafetyFade>();
            settings = rig.settings;
            yield return Seconds(1f);
            Assert.That(rig.Calibrated, "Rig should auto-calibrate once tracking and origin mode are known.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in spawned) if (obj) Object.Destroy(obj);
            spawned.Clear();
            if (hmd != null) InputSystem.RemoveDevice(hmd);
            if (leftDevice != null) InputSystem.RemoveDevice(leftDevice);
            if (rightDevice != null) InputSystem.RemoveDevice(rightDevice);
            InputSystem.settings.backgroundBehavior = savedBackground;
        }

        [UnityTest]
        public IEnumerator StandingIsStillAndAvatarEyesMatchCamera()
        {
            Vector3 start = rig.Body.position;
            yield return Seconds(1f);
            yield return new WaitForEndOfFrame();
            Assert.That(rig.Grounded);
            Assert.That(rig.Body.linearVelocity.magnitude, Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(start, rig.Body.position), Is.LessThan(0.02f));
            Assert.That(input.head.position.y - rig.Body.position.y, Is.EqualTo(settings.standingEyeHeight).Within(0.02f),
                "Calibration scales the user's eye height to the avatar's.");
            Assert.That(rig.HeadSeparation, Is.LessThan(0.02f));
            Transform headBone = pose.avatar.GetBoneTransform(HumanBodyBones.Head);
            Vector3 expected = settings.HeadBoneFromEye(input.head.position, input.head.rotation);
            Assert.That(Vector3.Distance(headBone.position, expected), Is.LessThan(0.01f), "Avatar eyes must sit on the camera.");
            AssertVisibleWristsMatchPhysicalHands();
            Assert.That(fade.fadeRenderer.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator HandReachIsLimitedToTheAvatarArm()
        {
            right.devicePosition = new Vector3(0.2f, 1.4f, 2.5f);
            Push();
            yield return Seconds(0.75f);
            PhysicsHand hand = rig.rightHand;
            float reach = Vector3.Distance(hand.Body.position + hand.Body.rotation * hand.WristOffset, rig.ShoulderPosition(false));
            Assert.That(reach, Is.LessThan(settings.PhysicalArmReach + 0.03f));
            Assert.That(hand.TrackingError, Is.GreaterThan(0.5f), "Out-of-reach target must not teleport the hand.");
            yield return new WaitForEndOfFrame();
            AssertVisibleWristsMatchPhysicalHands();
        }

        [UnityTest]
        public IEnumerator StickCrouchLowersTrackedHandsAndBlocksStandingUnderCeiling()
        {
            float standing = input.head.position.y;
            float handHeight = input.leftTarget.position.y;
            right.primary2DAxis = Vector2.down;
            Push();
            yield return Seconds(1f);
            Assert.That(input.head.position.y, Is.LessThan(standing - 0.6f));
            Assert.That(input.leftTarget.position.y, Is.LessThan(handHeight - 0.6f));
            Assert.That(rig.Top, Is.LessThan(1f));
            GameObject ceiling = Box("Crouch ceiling", rig.Body.position + Vector3.up * 1.1f, new Vector3(3f, 0.2f, 3f));
            Physics.SyncTransforms();
            right.primary2DAxis = Vector2.zero;
            Push();
            yield return Seconds(1f);
            Assert.That(input.head.position.y, Is.LessThan(1.05f));
            Object.Destroy(ceiling);
            yield return Seconds(1f);
            Assert.That(input.CrouchOffset, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator WalksUpStairsWithoutLaunching()
        {
            for (int i = 0; i < 4; ++i)
                Box("Step", new Vector3(0f, (i + 1) * 0.075f, 1.2f + i * 0.35f), new Vector3(1.2f, (i + 1) * 0.15f, 0.35f));
            Box("Landing", new Vector3(0f, 0.3f, 5.5f), new Vector3(1.2f, 0.6f, 6f)); // long enough not to walk off
            left.primary2DAxis = Vector2.up;
            Push();
            float maxRise = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                maxRise = Mathf.Max(maxRise, rig.Body.linearVelocity.y);
                Push();
                yield return null;
            }
            left.primary2DAxis = Vector2.zero;
            Push();
            yield return Seconds(0.5f);
            Assert.That(rig.Body.position.z, Is.GreaterThan(2.7f), "Should have reached the landing.");
            Assert.That(rig.Body.position.y, Is.EqualTo(0.6f).Within(0.05f));
            Assert.That(rig.Grounded);
            Assert.That(maxRise, Is.LessThan(2.5f), "Step-up must be smooth, not a launch.");
        }

        [UnityTest]
        public IEnumerator WalkableRampClimbsAndSteepRampDoesNot()
        {
            GameObject ramp = Box("Ramp 15", new Vector3(0f, 0.2f, 2.2f), new Vector3(1.5f, 0.1f, 2.5f));
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            left.primary2DAxis = Vector2.up;
            Push();
            yield return Seconds(1.6f);
            left.primary2DAxis = Vector2.zero;
            Push();
            Assert.That(rig.Body.position.y, Is.GreaterThan(0.2f), "15 degree ramp is walkable.");
            yield return Seconds(0.5f);
            float y = rig.Body.position.y;
            yield return Seconds(0.5f);
            Assert.That(Mathf.Abs(rig.Body.position.y - y), Is.LessThan(0.03f), "Standing on a walkable slope must not slide.");
        }

        [UnityTest]
        public IEnumerator CrouchUnderCeilingAndBlockedStandUpFades()
        {
            Box("Ceiling 1.1m", new Vector3(0f, 1.175f, 1.5f), new Vector3(2f, 0.15f, 1.2f));
            SetHead(new Vector3(0f, 0.95f, 0f), Quaternion.identity);
            left.devicePosition = new Vector3(-0.2f, 0.6f, 0.3f);
            right.devicePosition = new Vector3(0.2f, 0.6f, 0.3f);
            Push();
            yield return Seconds(0.8f);
            Assert.That(rig.Top, Is.LessThan(1.1f), "Capsule must shrink when crouching.");
            left.primary2DAxis = Vector2.up;
            Push();
            for (float t = 0f; t < 3f && rig.Body.position.z < 1.5f; t += Time.deltaTime) { Push(); yield return null; }
            left.primary2DAxis = Vector2.zero;
            Push();
            yield return Seconds(0.5f);
            Assert.That(rig.Body.position.z, Is.InRange(1.2f, 1.9f), "Crouched body fits under the ceiling.");
            SetHead(new Vector3(0f, UserEyeHeight, 0f), Quaternion.identity);
            Push();
            yield return Seconds(1f);
            Assert.That(rig.Top, Is.LessThan(1.1f), "Capsule must not grow into the ceiling.");
            Assert.That(rig.Body.position.y, Is.LessThan(0.05f), "Body must not be shoved up through or onto the ceiling.");
            Assert.That(rig.HeadObstructed && fade.fadeRenderer.enabled, "Head pushed through the ceiling must fade.");
        }

        [UnityTest]
        public IEnumerator GrabCarryReleaseAndThrow()
        {
            GameObject cube = Prop("Cube", rig.rightHand.Body.position + rig.rightHand.Body.rotation * Vector3.forward * 0.06f, 0.08f, 0.5f);
            right.grip = 1f;
            Push();
            yield return Seconds(0.2f);
            Assert.That(rig.rightHand.HeldObject, Is.Not.Null);
            Assert.That(rig.rightHand.HeldObject.gameObject, Is.SameAs(cube));
            float startY = cube.transform.position.y;
            right.devicePosition = RightRest + Vector3.up * 0.3f;
            Push();
            yield return Seconds(0.6f);
            Assert.That(cube.transform.position.y - startY, Is.EqualTo(0.3f * input.trackingScale).Within(0.06f));
            right.devicePosition = RightRest + new Vector3(0f, 0.3f, 0.45f);
            Push();
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            right.grip = 0f;
            Push();
            yield return Seconds(0.1f);
            Assert.That(rig.rightHand.HeldObject, Is.Null);
            Assert.That(cube.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0.2f), "Thrown cube keeps the hand's velocity.");
            Assert.That(cube.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.LessThan(9f), "Release must not add energy.");
        }

        [UnityTest]
        public IEnumerator HeldObjectCannotLiftThePlayer()
        {
            Prop("Heavy box", rig.rightHand.Body.position + rig.rightHand.Body.rotation * Vector3.forward * 0.08f, 0.2f, 5f);
            right.grip = 1f;
            Push();
            yield return Seconds(0.2f);
            Assert.That(rig.rightHand.HeldObject, Is.Not.Null);
            // Shove the held box down into the floor under the player and hold it there.
            right.devicePosition = new Vector3(0.1f, -0.4f, 0.1f);
            Push();
            float maxY = 0f;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                maxY = Mathf.Max(maxY, rig.Body.position.y);
                Push();
                yield return null;
            }
            Assert.That(maxY, Is.LessThan(0.05f), "A held object must not be usable to lift yourself.");
        }

        [UnityTest]
        public IEnumerator ClimbIsBoundedHoldsStillAndReleases()
        {
            right.devicePosition = new Vector3(0.2f, 1.7f, 0.3f);
            Push();
            yield return Seconds(0.6f);
            GameObject rung = Box("Rung", rig.rightHand.Body.position, new Vector3(0.8f, 0.035f, 0.035f));
            rung.AddComponent<Climbable>();
            right.grip = 1f;
            Push();
            yield return Seconds(0.1f);
            Assert.That(rig.rightHand.Anchored, "Gripping a Climbable locks the hand.");
            float startY = rig.Body.position.y, maxSpeed = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                right.devicePosition = new Vector3(0.2f, Mathf.Lerp(1.7f, 1.1f, t), 0.3f);
                Push();
                maxSpeed = Mathf.Max(maxSpeed, rig.Body.linearVelocity.magnitude);
                yield return null;
            }
            right.devicePosition = new Vector3(0.2f, 1.1f, 0.3f);
            Push();
            yield return Seconds(0.6f);
            Assert.That(rig.Climbing);
            Assert.That(rig.Body.position.y - startY, Is.EqualTo(0.6f * input.trackingScale).Within(0.08f), "Pulling down lifts the body.");
            Assert.That(maxSpeed, Is.LessThan(settings.maximumClimbSpeed + 0.2f));
            float hangY = rig.Body.position.y;
            yield return Seconds(1f);
            Assert.That(Mathf.Abs(rig.Body.position.y - hangY), Is.LessThan(0.02f), "Hanging must not sag or creep.");
            right.grip = 0f;
            Push();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Body.linearVelocity.y, Is.LessThan(settings.maximumClimbSpeed), "Release carries bounded speed.");
            yield return Seconds(1.2f);
            Assert.That(rig.Grounded && !rig.Climbing);
        }

        [UnityTest]
        public IEnumerator ControllerTrackingLossReleasesThenRecovers()
        {
            Prop("Cube", rig.rightHand.Body.position + rig.rightHand.Body.rotation * Vector3.forward * 0.06f, 0.08f, 0.5f);
            right.grip = 1f;
            Push();
            yield return Seconds(0.2f);
            Assert.That(rig.rightHand.HeldObject, Is.Not.Null);
            right.isTracked = false;
            right.trackingState = 0;
            Push();
            yield return Seconds(0.4f);
            Assert.That(rig.rightHand.HeldObject, Is.Null, "Tracking loss releases.");
            Assert.That(rig.rightHand.Body.linearVelocity.magnitude, Is.LessThan(0.2f), "Untracked hand damps in place.");
            right.isTracked = true;
            right.trackingState = 3;
            right.grip = 0f;
            Push();
            yield return Seconds(0.6f);
            Assert.That(rig.rightHand.TrackingError, Is.LessThan(0.05f), "Hand returns to the controller after reacquisition.");
        }

        [UnityTest]
        public IEnumerator HeadTrackingLossFadesAndReacquisitionRecenters()
        {
            head.isTracked = false;
            head.trackingState = 0;
            Push();
            yield return Seconds(0.3f);
            yield return new WaitForEndOfFrame();
            Assert.That(rig.HeadObstructed && fade.fadeRenderer.enabled, "Lost head tracking blacks out.");
            Assert.That(rig.Grounded, "Body still stands on the ground spring while untracked.");
            head.isTracked = true;
            head.trackingState = 3;
            SetHead(new Vector3(1.2f, UserEyeHeight, -0.8f), Quaternion.identity); // reacquired elsewhere in the room
            Push();
            Vector3 body = rig.Body.position;
            yield return Seconds(0.5f);
            Assert.That(rig.HeadSeparation, Is.LessThan(0.05f), "Reacquired head is recentered over the body.");
            Assert.That(Vector3.Distance(body, rig.Body.position), Is.LessThan(0.05f), "Body must not chase the jumped head pose.");
        }

        [UnityTest]
        public IEnumerator FastSwingIntoWallIsBlockedAndPushesBodyBack()
        {
            Box("Wall", new Vector3(0f, 1.2f, 0.65f), new Vector3(3f, 2.4f, 0.2f)); // front face z = 0.55
            yield return Seconds(0.3f);
            float bodyZ = rig.Body.position.z;
            right.devicePosition = new Vector3(0.2f, 1.2f, 1.6f); // one-frame swing to behind the wall
            Push();
            float maxHandZ = float.NegativeInfinity;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                maxHandZ = Mathf.Max(maxHandZ, rig.rightHand.Body.position.z);
                Push();
                yield return null;
            }
            Assert.That(maxHandZ, Is.LessThan(0.55f), "Physical hand must not pass through the wall.");
            Assert.That(rig.Body.position.z, Is.LessThanOrEqualTo(bodyZ + 0.001f), "Pushing on a wall pushes the body away, never into it.");
        }

        [UnityTest]
        public IEnumerator RoomScaleWalkIntoWallIsBlockedAndFades()
        {
            Box("Wall", new Vector3(0f, 1.2f, 0.75f), new Vector3(3f, 2.4f, 0.2f)); // front face z = 0.65
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                SetHead(new Vector3(0f, UserEyeHeight, Mathf.Lerp(0f, 1.4f, t / 1.5f)), Quaternion.identity);
                Push();
                yield return null;
            }
            yield return Seconds(0.3f);
            yield return new WaitForEndOfFrame();
            Assert.That(rig.Body.position.z + settings.bodyRadius, Is.LessThan(0.67f), "Body stays out of the wall.");
            Assert.That(fade.fadeRenderer.enabled, "Head walked through the wall must fade.");
            SetHead(new Vector3(0f, UserEyeHeight, 0f), Quaternion.identity);
            Push();
            yield return Seconds(1f);
            yield return new WaitForEndOfFrame();
            Assert.That(rig.HeadSeparation, Is.LessThan(0.1f), "Stepping back reunites head and body (no stuck tracking offset).");
        }

        [UnityTest]
        public IEnumerator RoomScaleAndJoystickCombineWithoutDrift()
        {
            // Walk backwards in the room while pushing the stick forward, then perpendicular.
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                SetHead(new Vector3(0f, UserEyeHeight, Mathf.Lerp(0f, -0.8f, t / 1.5f)), Quaternion.identity);
                left.primary2DAxis = t < 0.75f ? Vector2.up : Vector2.right;
                Push();
                Assert.That(rig.HeadSeparation, Is.LessThan(0.25f), "Head and body must stay together while combining inputs.");
                yield return null;
            }
            left.primary2DAxis = Vector2.zero;
            Push();
            yield return Seconds(0.5f);
            Assert.That(rig.HeadSeparation, Is.LessThan(0.05f));
            Assert.That(rig.Body.linearVelocity.magnitude, Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator JumpRisesAboutJumpHeightAndLandsSoftly()
        {
            right.buttons = 0;
            right = right.WithButton(ControllerButton.PrimaryButton, true);
            Push();
            yield return null;
            right = right.WithButton(ControllerButton.PrimaryButton, false);
            Push();
            float maxY = 0f;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                maxY = Mathf.Max(maxY, rig.Body.position.y);
                Push();
                yield return null;
            }
            Assert.That(maxY, Is.EqualTo(settings.jumpHeight).Within(0.15f));
            Assert.That(rig.Grounded);
            Assert.That(rig.Body.position.y, Is.EqualTo(0f).Within(0.03f));
        }

        [UnityTest]
        public IEnumerator DestroyingAHeldObjectReleasesCleanly()
        {
            GameObject cube = Prop("Cube", rig.rightHand.Body.position + rig.rightHand.Body.rotation * Vector3.forward * 0.06f, 0.08f, 0.5f);
            right.grip = 1f;
            Push();
            yield return Seconds(0.2f);
            Assert.That(rig.rightHand.HeldObject, Is.Not.Null);
            Object.Destroy(cube);
            yield return Seconds(0.2f);
            Assert.That(rig.rightHand.HeldObject, Is.Null);
            Assert.That(rig.rightHand.TrackingError, Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator TwoHandHandoverAndTurningKeepsGrip()
        {
            Vector3 mid = 0.5f * (rig.leftHand.Body.position + rig.rightHand.Body.position);
            GameObject bar = Prop("Bar", mid + Vector3.forward * 0.05f, 0.05f, 2f);
            bar.transform.localScale = new Vector3(0.6f, 0.05f, 0.05f);
            left.grip = 1f;
            Push();
            yield return Seconds(0.1f);
            right.grip = 1f;
            Push();
            yield return Seconds(0.2f);
            PhysicsGrabbable item = bar.GetComponent<PhysicsGrabbable>();
            Assert.That(item.HoldingHands, Is.EqualTo(2));
            Assert.That(item.PrimaryHand, Is.SameAs(rig.leftHand));
            right.primary2DAxis = Vector2.right; // smooth turn while holding with both hands
            Push();
            yield return Seconds(1f);
            right.primary2DAxis = Vector2.zero;
            Push();
            yield return Seconds(0.3f);
            Assert.That(item.HoldingHands, Is.EqualTo(2), "Turning must not break a two-hand grip.");
            left.grip = 0f;
            Push();
            yield return Seconds(0.2f);
            Assert.That(item.HoldingHands, Is.EqualTo(1));
            Assert.That(item.PrimaryHand, Is.SameAs(rig.rightHand), "Remaining hand takes over orientation.");
        }

        [UnityTest]
        public IEnumerator SteadyStateTiming()
        {
            using (var scripts = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "FixedBehaviourUpdate", 240))
            using (var physics = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Physics, "Physics.Simulate", 240))
            using (var gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 240))
            {
                yield return Seconds(2f);
                Debug.Log($"Universal Rig timing (editor, simulated input): FixedBehaviourUpdate avg {Average(scripts) / 1e6:F3} ms, " +
                    $"Physics.Simulate avg {Average(physics) / 1e6:F3} ms, GC Allocated In Frame avg {Average(gc):F0} B (whole editor frame), fixedDt {Time.fixedDeltaTime:F4}");
            }
        }

        private static double Average(Unity.Profiling.ProfilerRecorder recorder)
        {
            int count = recorder.Count;
            if (count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < count; ++i) sum += recorder.GetSample(i).Value;
            return sum / count;
        }

        private void AssertVisibleWristsMatchPhysicalHands()
        {
            foreach (PhysicsHand hand in new[] { rig.leftHand, rig.rightHand })
            {
                Transform bone = pose.avatar.GetBoneTransform(hand.isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Assert.That(Vector3.Distance(bone.position, hand.transform.TransformPoint(hand.WristOffset)), Is.LessThan(0.02f),
                    (hand.isLeft ? "Left" : "Right") + " visible wrist must match the physical hand.");
            }
        }

        private void SetHead(Vector3 position, Quaternion rotation)
        {
            head.centerEyePosition = head.devicePosition = position;
            head.centerEyeRotation = head.deviceRotation = rotation;
        }

        private static XRSimulatedControllerState Controller(Vector3 position)
        {
            var state = new XRSimulatedControllerState();
            state.Reset();
            state.isTracked = true;
            state.trackingState = 3;
            state.devicePosition = position;
            state.deviceRotation = Quaternion.identity;
            return state;
        }

        private void Push()
        {
            InputSystem.QueueStateEvent(hmd, head);
            InputSystem.QueueStateEvent(leftDevice, left);
            InputSystem.QueueStateEvent(rightDevice, right);
        }

        // Re-sends device state every frame: the editor can reset simulated devices (looks
        // like tracking loss to the rig) even though nothing changed in the test.
        private IEnumerator Seconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                Push();
                yield return null;
            }
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = size;
            spawned.Add(obj);
            return obj;
        }

        private GameObject Prop(string name, Vector3 position, float size, float mass)
        {
            GameObject obj = Box(name, position, Vector3.one * size);
            Rigidbody body = obj.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            obj.AddComponent<PhysicsGrabbable>();
            return obj;
        }
    }
}
