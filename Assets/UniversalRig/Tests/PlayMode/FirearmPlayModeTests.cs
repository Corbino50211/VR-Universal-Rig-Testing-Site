using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UniversalRig.Weapons;

namespace UniversalRig.Tests
{
    // Simulated XR only. These drive SoldierInput the same way as RigPlayModeTests.
    public sealed class FirearmPlayModeTests
    {
        const string RigPrefab = "Assets/UniversalRig/Generated/SoldierRig.prefab";
        const string GunFolder = "Assets/UniversalRig/Weapons/Generated/Prefabs/";
        const float Eye = 1.75f;
        static readonly Vector3 LeftRest = new Vector3(-0.2f, 1.2f, 0.35f);
        static readonly Vector3 RightRest = new Vector3(0.2f, 1.2f, 0.35f);

        XRSimulatedHMD hmd;
        XRSimulatedController leftDevice, rightDevice;
        XRSimulatedHMDState head;
        XRSimulatedControllerState left, right;
        readonly List<GameObject> spawned = new List<GameObject>();
        InputSettings.BackgroundBehavior savedBackground;
        PhysicsRig rig;
        RigInput input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Box("Firearm test floor", new Vector3(0f, -0.1f, 0f), new Vector3(30f, 0.2f, 30f));
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            leftDevice = InputSystem.AddDevice<XRSimulatedController>();
            rightDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            head.Reset();
            head.isTracked = true;
            head.trackingState = 3;
            head.centerEyePosition = head.devicePosition = new Vector3(0f, Eye, 0f);
            head.centerEyeRotation = head.deviceRotation = Quaternion.identity;
            left = Controller(LeftRest);
            right = Controller(RightRest);
            Push();
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
#else
            GameObject prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            spawned.Add(instance);
            input = instance.GetComponent<RigInput>();
            input.deviceStandingHeight = 0f;
            rig = instance.GetComponentInChildren<PhysicsRig>();
            yield return Seconds(1f);
            Assert.That(rig.Calibrated);
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
        public IEnumerator GrabReleaseSwapAndTrackingLoss()
        {
            Firearm gun = SpawnGun("M1911", rig.rightHand);
            yield return GrabPrimary(gun, false);
            Assert.That(gun.Grabbable.PrimaryHand, Is.SameAs(rig.rightHand));
            right.grip = 0f;
            Push();
            yield return Seconds(0.25f);
            Assert.That(gun.Grabbable.HoldingHands, Is.EqualTo(0));
            yield return GrabPrimary(gun, true);
            Assert.That(gun.Grabbable.PrimaryHand, Is.SameAs(rig.leftHand));
            int dry = gun.DryFires;
            left.isTracked = false;
            left.trackingState = 0;
            Push();
            yield return Seconds(0.35f);
            Assert.That(gun.Grabbable.HoldingHands, Is.EqualTo(0), "Tracking loss lets go of the gun.");
            Assert.That(gun.DryFires, Is.EqualTo(dry));
            Assert.That(gun.ShotsFired, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator EmptyFireReloadAccountingAndDrop()
        {
            Firearm gun = SpawnGun("M1911", rig.rightHand);
            yield return GrabPrimary(gun, false);
            yield return Click(false);
            Assert.That(gun.ShotsFired, Is.EqualTo(0));
            Assert.That(gun.DryFires, Is.EqualTo(1));
            Assert.That(gun.ChamberLoaded, Is.False);

            Magazine partial = SpawnMag("Mag_45", 3);
            yield return GrabLoose(partial.GetComponent<PhysicsGrabbable>(), true);
            Assert.That(partial.GetComponent<PhysicsGrabbable>().PrimaryHand, Is.SameAs(rig.leftHand));
            yield return Move(true, gun.socket.seat);
            yield return Seconds(0.25f);
            Assert.That(gun.Magazine, Is.Null, "A magazine still in the hand is not seated.");
            yield return Move(true, gun.socket.seat.position + Vector3.left * 0.4f, gun.socket.seat.rotation);
            left.grip = 0f;
            Push();
            yield return Seconds(0.35f);
            Assert.That(gun.Magazine, Is.Null, "Pulling the magazine away interrupts the reload.");
            Object.Destroy(partial.gameObject);

            Magazine full = SpawnMag("Mag_45", 7);
            yield return Seat(gun, full);
            Assert.That(gun.Magazine.rounds, Is.EqualTo(7));
            Assert.That(gun.ChamberLoaded, Is.False);
            yield return Cycle(gun);
            Assert.That(gun.ChamberLoaded, Is.True);
            Assert.That(gun.Magazine.rounds, Is.EqualTo(6), "Racking moves one round into the chamber.");
            yield return Click(false);
            Assert.That(gun.ShotsFired, Is.EqualTo(1));
            Assert.That(gun.ChamberLoaded, Is.True);
            Assert.That(gun.Magazine.rounds, Is.EqualTo(5));

            yield return GrabLoose(full.GetComponent<PhysicsGrabbable>(), true);
            yield return Move(true, full.transform.position + Vector3.left * 0.35f, full.transform.rotation);
            Assert.That(gun.Magazine, Is.Null);
            Assert.That(full.rounds, Is.EqualTo(5));
            Assert.That(gun.ChamberLoaded, Is.True, "The chambered round stays when the magazine comes out.");
            left.grip = 0f;
            Push();
            yield return Seconds(0.2f);

            Magazine spare = SpawnMag("Mag_45", 2);
            yield return Seat(gun, spare);
            Assert.That(gun.Magazine.rounds, Is.EqualTo(2));
            Assert.That(gun.ChamberLoaded, Is.True);
            right.grip = 0f;
            Push();
            yield return Seconds(0.25f);
            Assert.That(gun.ChamberLoaded && gun.Magazine && gun.Magazine.rounds == 2, "Dropping the gun keeps the load.");
            yield return GrabPrimary(gun, true);
            yield return Click(true);
            Assert.That(gun.ShotsFired, Is.EqualTo(2));
            Assert.That(gun.Magazine.rounds, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RecoilIsWeakerBracedAndDoesNotPropelTheBody()
        {
            Firearm gun = SpawnGun("M1911", rig.rightHand);
            yield return GrabPrimary(gun, false);
            yield return Seat(gun, SpawnMag("Mag_45", 7));
            yield return Cycle(gun);
            Vector3 body = rig.Body.position;
            yield return Click(false);
            float oneHand = gun.LastImpulse;
            Assert.That(oneHand, Is.GreaterThan(0.05f));
            yield return GrabNamed(gun, "Support grip", true);
            Assert.That(gun.Grabbable.HoldingHands, Is.EqualTo(2));
            yield return Click(false);
            Assert.That(gun.LastImpulse, Is.LessThan(oneHand), "A second hand reduces the recoil impulse.");
            Assert.That(gun.ShotsFired, Is.EqualTo(2));
            yield return Click(false);
            yield return Click(false);
            Assert.That(Vector3.Distance(body, rig.Body.position), Is.LessThan(0.05f), "Firing must not propel the player.");
            Assert.That(rig.Body.position.y, Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator MuzzleWallStopsTheShot()
        {
            Firearm gun = SpawnGun("M1911", rig.rightHand);
            yield return GrabPrimary(gun, false);
            yield return Seat(gun, SpawnMag("Mag_45", 7));
            yield return Cycle(gun);
            Vector3 dir = gun.muzzle.forward;
            HitTarget wall = Target("Muzzle wall", gun.muzzle.position + dir * 0.3f, new Vector3(0.5f, 0.5f, 0.08f), dir);
            HitTarget far = Target("Downrange", gun.muzzle.position + dir * 2.2f, new Vector3(0.8f, 0.8f, 0.08f), dir);
            yield return Click(false);
            Assert.That(wall.hits, Is.GreaterThan(0));
            Assert.That(far.hits, Is.EqualTo(0), "A shot stops on the first wall.");

            wall.transform.position = gun.muzzle.position;
            foreach (Collider gunCollider in gun.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(wall.GetComponent<Collider>(), gunCollider, true);
            int farBefore = far.hits;
            yield return Click(false);
            Assert.That(far.hits, Is.EqualTo(farBefore), "A muzzle buried in a wall does not hit past it.");
        }

        [UnityTest]
        public IEnumerator OneClickFiresOneRoundWhenTheFrameRateChanges()
        {
            Firearm gun = SpawnGun("M1911", rig.rightHand);
            yield return GrabPrimary(gun, false);
            yield return Seat(gun, SpawnMag("Mag_45", 7));
            yield return Cycle(gun);
            float savedDt = Time.fixedDeltaTime;
            int savedCapture = Time.captureFramerate;
            try
            {
                Time.fixedDeltaTime = 1f / 30f;
                Time.captureFramerate = 20;
                int shots = gun.ShotsFired;
                yield return Click(false);
                Assert.That(gun.ShotsFired, Is.EqualTo(shots + 1));
                Assert.That(gun.ChamberLoaded, Is.True);
            }
            finally
            {
                Time.fixedDeltaTime = savedDt;
                Time.captureFramerate = savedCapture;
            }
        }

        [UnityTest]
        public IEnumerator ShotgunLoadsFromThePortAndMustBePumped()
        {
            Firearm gun = SpawnGun("BenelliM4", rig.rightHand);
            yield return GrabPrimary(gun, false);
            yield return Click(false);
            Assert.That(gun.ShotsFired, Is.EqualTo(0));
            yield return FeedShell(gun);
            yield return FeedShell(gun);
            Assert.That(gun.TubeRounds, Is.EqualTo(2));
            yield return Cycle(gun);
            Assert.That(gun.ChamberLoaded, Is.True);
            Assert.That(gun.TubeRounds, Is.EqualTo(1));
            HitTarget plate = Target("Shot plate", gun.muzzle.position + gun.muzzle.forward * 3f,
                new Vector3(1.2f, 1.2f, 0.08f), gun.muzzle.forward);
            yield return Click(false);
            Assert.That(gun.ShotsFired, Is.EqualTo(1));
            Assert.That(gun.NeedsCycle, Is.True);
            Assert.That(plate.hits, Is.GreaterThan(0));
            int hits = plate.hits;
            yield return Click(false);
            Assert.That(gun.ShotsFired, Is.EqualTo(1), "The pump has to close before the next shot.");
            Assert.That(plate.hits, Is.EqualTo(hits));
            yield return Cycle(gun);
            yield return Click(false);
            Assert.That(gun.ShotsFired, Is.EqualTo(2));
            Assert.That(gun.TubeRounds, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator UziChargesFromTheBoltAndTheRifleChangesFireMode()
        {
            Firearm uzi = SpawnGun("Uzi", rig.rightHand);
            yield return GrabPrimary(uzi, false);
            yield return Seat(uzi, SpawnMag("Mag_9", 10));
            yield return Click(false);
            Assert.That(uzi.ShotsFired, Is.EqualTo(0), "An open-bolt gun does not fire until it is cocked.");
            yield return Cycle(uzi);
            Assert.That(uzi.Cocked, Is.True);
            yield return Click(false);
            Assert.That(uzi.ShotsFired, Is.EqualTo(1));
            right.grip = 0f;
            left.grip = 0f;
            Push();
            Object.Destroy(uzi.gameObject);
            yield return Seconds(0.2f);

            Firearm ak = SpawnGun("AK74", rig.rightHand);
            yield return GrabPrimary(ak, false);
            Assert.That(ak.Mode, Is.EqualTo(FireMode.Semi));
            yield return Move(true, ak.selector);
            left.trigger = 1f;
            Push();
            yield return Seconds(0.25f);
            left.trigger = 0f;
            Push();
            yield return Seconds(0.15f);
            Assert.That(ak.Mode, Is.EqualTo(FireMode.Auto), "The other hand's trigger on the selector changes mode.");
        }

        Firearm SpawnGun(string name, PhysicsHand hand)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(GunFolder + name + ".prefab");
#else
            GameObject prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null, name + " prefab is missing. Run Tools/Universal Rig/Build Firearms.");
            Transform grip = GripOn(prefab, "Primary grip");
            // Chest height, between the shoulders, so the other hand can reach the slide.
            // Identity matches the simulated controller's rest rotation.
            Quaternion handRot = Quaternion.identity;
            Vector3 handPos = new Vector3(0f, 1.22f, 0.32f);
            if (!hand) handPos = Vector3.zero;
            Vector3 localPos = prefab.transform.InverseTransformPoint(grip.position);
            Quaternion localRot = Quaternion.Inverse(prefab.transform.rotation) * grip.rotation;
            Quaternion gunRot = handRot * Quaternion.Inverse(localRot);
            Vector3 gunPos = handPos - gunRot * localPos;
            GameObject gun = Object.Instantiate(prefab, gunPos, gunRot);
            Rigidbody body = gun.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            spawned.Add(gun);
            return gun.GetComponent<Firearm>();
        }

        Magazine SpawnMag(string name, int rounds)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(GunFolder + name + ".prefab");
#else
            GameObject prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null, name);
            GameObject mag = Object.Instantiate(prefab);
            Rigidbody body = mag.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            spawned.Add(mag);
            mag.GetComponent<Magazine>().rounds = rounds;
            return mag.GetComponent<Magazine>();
        }

        IEnumerator Seat(Firearm gun, Magazine mag)
        {
            mag.Body.position = gun.socket.seat.position;
            mag.Body.rotation = gun.socket.seat.rotation;
            mag.Body.linearVelocity = Vector3.zero;
            mag.Body.angularVelocity = Vector3.zero;
            float end = Time.time + 1f;
            while (Time.time < end && gun.Magazine != mag)
            {
                mag.Body.position = gun.socket.seat.position;
                mag.Body.rotation = gun.socket.seat.rotation;
                mag.Body.linearVelocity = Vector3.zero;
                Push();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(gun.Magazine, Is.SameAs(mag), "Magazine did not seat.");
        }

        IEnumerator FeedShell(Firearm gun)
        {
            Magazine shell = SpawnMag("TEMPORARY_Shell", 1);
            shell.Body.position = gun.socket.seat.position;
            shell.Body.rotation = gun.socket.seat.rotation;
            shell.Body.linearVelocity = Vector3.zero;
            int before = gun.TubeRounds;
            float end = Time.time + 1f;
            while (Time.time < end && gun.TubeRounds == before)
            {
                if (shell)
                {
                    shell.Body.position = gun.socket.seat.position;
                    shell.Body.rotation = gun.socket.seat.rotation;
                    shell.Body.linearVelocity = Vector3.zero;
                }
                Push();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(gun.TubeRounds, Is.EqualTo(before + 1));
        }

        IEnumerator GrabPrimary(Firearm gun, bool leftHand)
        {
            PhysicsHand hand = leftHand ? rig.leftHand : rig.rightHand;
            // Between the shoulders. A gun parked on the right-hand rest is out of the other arm's reach.
            Vector3 chest = new Vector3(leftHand ? -0.04f : 0.04f, 1.32f, 0.18f);
            if (leftHand) left.grip = 0f;
            else right.grip = 0f;
            float move = Time.time + 0.45f;
            while (Time.time < move)
            {
                Aim(leftHand, chest, Quaternion.identity);
                Push();
                yield return null;
            }
            if (leftHand) left.grip = 1f;
            else right.grip = 1f;
            float end = Time.time + 1.5f;
            while (Time.time < end && gun.Grabbable.PrimaryHand != hand)
            {
                Stick(gun, Grip(gun, "Primary grip"), hand);
                Push();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(gun.Grabbable.PrimaryHand, Is.SameAs(hand),
                "Primary grip was not taken. Tracking error " + hand.TrackingError);
        }

        IEnumerator GrabNamed(Firearm gun, string gripName, bool leftHand)
        {
            PhysicsHand hand = leftHand ? rig.leftHand : rig.rightHand;
            if (leftHand) left.grip = 1f;
            else right.grip = 1f;
            float end = Time.time + 1.5f;
            while (Time.time < end && hand.HeldObject != gun.Grabbable)
            {
                Transform grip = Grip(gun, gripName);
                Aim(leftHand, grip.position, grip.rotation);
                Push();
                yield return null;
            }
            Assert.That(hand.HeldObject, Is.SameAs(gun.Grabbable), gripName + " was not taken. Tracking error " + hand.TrackingError);
        }

        IEnumerator GrabLoose(PhysicsGrabbable item, bool leftHand)
        {
            PhysicsHand hand = leftHand ? rig.leftHand : rig.rightHand;
            if (leftHand) left.grip = 1f;
            else right.grip = 1f;
            float end = Time.time + 1.5f;
            while (Time.time < end && item.PrimaryHand != hand)
            {
                item.Body.position = hand.Body.position;
                item.Body.rotation = hand.Body.rotation;
                item.Body.linearVelocity = Vector3.zero;
                item.Body.angularVelocity = Vector3.zero;
                Push();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(item.PrimaryHand, Is.SameAs(hand), "Loose object was not grabbed.");
        }

        IEnumerator Cycle(Firearm gun)
        {
            ActionPart action = gun.action;
            Assert.That(action && action.HandleBody, Is.True);
            PhysicsGrabbable handle = action.HandleBody.GetComponent<PhysicsGrabbable>();
            int cap = gun.Grabbable.maximumHands;
            gun.Grabbable.maximumHands = Mathf.Max(1, gun.Grabbable.HoldingHands);
            // Midpoint of the two hands. Each arm can reach it; either extreme cannot.
            Vector3 meet = 0.5f * (rig.leftHand.Body.position + rig.rightHand.Body.position);
            meet.y = Mathf.Min(meet.y, 1.25f);
            float end = Time.time + 1.4f;
            while (Time.time < end && Vector3.Distance(action.HandleBody.position, meet) > 0.05f)
            {
                Vector3 shift = Vector3.ClampMagnitude(meet - action.HandleBody.position, 0.015f);
                Aim(false, rig.rightHand.TargetPosition + shift, rig.rightHand.TargetRotation);
                Push();
                yield return null;
            }
            Aim(false, rig.rightHand.Body.position, rig.rightHand.Body.rotation);
            // Grip has to fall below the release threshold or the hand will not try again.
            left.grip = 0f;
            for (int i = 0; i < 4; ++i) { Push(); yield return new WaitForFixedUpdate(); }
            left.grip = 1f;
            end = Time.time + 1.2f;
            while (Time.time < end && handle.PrimaryHand != rig.leftHand)
            {
                Aim(true, action.HandleBody.position, action.HandleBody.rotation);
                Push();
                yield return null;
            }
            Assert.That(handle.PrimaryHand, Is.SameAs(rig.leftHand),
                "Did not grab the action. Hand-to-handle " +
                Vector3.Distance(rig.leftHand.Body.position, action.HandleBody.position).ToString("F3") +
                " tracking " + rig.leftHand.TrackingError.ToString("F3"));
            Vector3 start = action.HandleBody.position;
            Vector3 open = start + action.OpenDirectionWorld * (action.travel + 0.02f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                Vector3 pos = Vector3.Lerp(start, open, t / 0.45f);
                action.HandleBody.position = pos;
                action.HandleBody.linearVelocity = Vector3.zero;
                Aim(true, pos, action.HandleBody.rotation);
                Push();
                yield return new WaitForFixedUpdate();
            }
            left.grip = 0f;
            Push();
            end = Time.time + 1.2f;
            while (Time.time < end && !gun.ChamberLoaded && !gun.Cocked)
            {
                Push();
                yield return null;
            }
            Assert.That(gun.ChamberLoaded || gun.Cocked, "Working the action did not chamber or cock. Open01=" + action.Open01);
        }

        static void Stick(Firearm gun, Transform grip, PhysicsHand hand)
        {
            Vector3 pivot = grip.position;
            Quaternion delta = hand.Body.rotation * Quaternion.Inverse(grip.rotation);
            Rigidbody handle = gun.action ? gun.action.HandleBody : null;
            Vector3 handlePos = handle ? handle.position : Vector3.zero;
            Quaternion handleRot = handle ? handle.rotation : Quaternion.identity;
            gun.transform.rotation = delta * gun.transform.rotation;
            gun.transform.position += hand.Body.position - grip.position;
            gun.Body.position = gun.transform.position;
            gun.Body.rotation = gun.transform.rotation;
            gun.Body.linearVelocity = Vector3.zero;
            gun.Body.angularVelocity = Vector3.zero;
            if (!handle) return;
            handle.position = hand.Body.position + delta * (handlePos - pivot);
            handle.rotation = delta * handleRot;
            handle.linearVelocity = Vector3.zero;
            handle.angularVelocity = Vector3.zero;
        }

        void Aim(bool leftHand, Vector3 worldPos, Quaternion worldRot)
        {
            Vector3 pos = input.trackingSpace.InverseTransformPoint(worldPos) / Mathf.Max(0.01f, input.trackingScale);
            Quaternion rot = Quaternion.Inverse(input.trackingSpace.rotation) * worldRot;
            if (leftHand) { left.devicePosition = pos; left.deviceRotation = rot; }
            else { right.devicePosition = pos; right.deviceRotation = rot; }
        }

        IEnumerator Move(bool leftHand, Transform target)
        {
            yield return Move(leftHand, target.position, target.rotation);
        }

        IEnumerator Move(bool leftHand, Vector3 worldPos, Quaternion worldRot)
        {
            Vector3 from = leftHand ? left.devicePosition : right.devicePosition;
            Quaternion fromRot = leftHand ? left.deviceRotation : right.deviceRotation;
            Vector3 to = input.trackingSpace.InverseTransformPoint(worldPos) / Mathf.Max(0.01f, input.trackingScale);
            Quaternion toRot = Quaternion.Inverse(input.trackingSpace.rotation) * worldRot;
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                Vector3 pos = Vector3.Lerp(from, to, u);
                Quaternion rot = Quaternion.Slerp(fromRot, toRot, u);
                if (leftHand)
                {
                    left.devicePosition = pos;
                    left.deviceRotation = rot;
                }
                else
                {
                    right.devicePosition = pos;
                    right.deviceRotation = rot;
                }
                Push();
                yield return null;
            }
            if (leftHand) { left.devicePosition = to; left.deviceRotation = toRot; }
            else { right.devicePosition = to; right.deviceRotation = toRot; }
            Push();
        }

        IEnumerator Click(bool leftHand)
        {
            if (leftHand) left.trigger = 1f;
            else right.trigger = 1f;
            Push();
            yield return Seconds(0.12f);
            if (leftHand) left.trigger = 0f;
            else right.trigger = 0f;
            Push();
            yield return Seconds(0.15f);
        }

        static Transform Grip(Firearm gun, string name)
        {
            Transform grip = GripOn(gun.gameObject, name);
            Assert.That(grip, Is.Not.Null, name);
            return grip;
        }

        static Transform GripOn(GameObject root, string name)
        {
            foreach (GripPoint grip in root.GetComponentsInChildren<GripPoint>(true))
                if (grip.name == name) return grip.transform;
            return null;
        }

        HitTarget Target(string name, Vector3 position, Vector3 size, Vector3 forward)
        {
            GameObject obj = Box(name, position, size);
            if (forward.sqrMagnitude > 0.0001f)
                obj.transform.rotation = Quaternion.LookRotation(forward);
            return obj.AddComponent<HitTarget>();
        }

        GameObject Box(string name, Vector3 position, Vector3 size)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = size;
            spawned.Add(obj);
            return obj;
        }

        static XRSimulatedControllerState Controller(Vector3 position)
        {
            var state = new XRSimulatedControllerState();
            state.Reset();
            state.isTracked = true;
            state.trackingState = 3;
            state.devicePosition = position;
            state.deviceRotation = Quaternion.identity;
            return state;
        }

        void Push()
        {
            InputSystem.QueueStateEvent(hmd, head);
            InputSystem.QueueStateEvent(leftDevice, left);
            InputSystem.QueueStateEvent(rightDevice, right);
        }

        IEnumerator Seconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                Push();
                yield return null;
            }
        }
    }
}
