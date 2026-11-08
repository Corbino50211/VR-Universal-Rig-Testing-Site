using NUnit.Framework;
using UnityEngine;

namespace UniversalRig.Tests
{
    public sealed class RigMathTests
    {
        private static void Near(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(tolerance));
        }

        [Test]
        public void EquivalentQuaternionSignsDoNotCreateTorque()
        {
            Quaternion q = Quaternion.Euler(20f, 170f, -40f);
            Quaternion negative = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            Near(Vector3.zero, RigMath.RotationError(negative, q));
        }

        [Test]
        public void RotationErrorUsesShortestPathAcross180Degrees()
        {
            Vector3 error = RigMath.RotationError(Quaternion.Euler(0f, -179f, 0f), Quaternion.Euler(0f, 179f, 0f));
            Near(Vector3.up * (2f * Mathf.Deg2Rad), error);
        }

        [Test]
        public void BlockedRoomScaleRequestDoesNotMoveTrackingOrigin()
        {
            Near(Vector3.zero, RigMath.CompletedRoomStep(Vector3.zero, Vector3.right * 0.1f));
        }

        [Test]
        public void CompletedRoomScaleDoesNotConsumeExtraLocomotion()
        {
            Vector3 actual = new Vector3(0.2f, -0.05f, 0.3f);
            Vector3 room = RigMath.CompletedRoomStep(actual, Vector3.right * 0.1f);
            Near(Vector3.right * 0.1f, room);
            Near(new Vector3(0.1f, -0.05f, 0.3f), actual - room);
        }

        [Test]
        public void StickWalkOppositeToRoomScaleStillBooksTheRoomStep()
        {
            // Stick forward at 2.5 m/s while the user steps back at 0.5 m/s: the body moves
            // forward 2.0 m/s. After removing the walk-only step, the room step is booked in full.
            const float dt = 0.01f;
            Vector3 walk = Vector3.forward * 2.5f * dt, room = Vector3.back * 0.5f * dt;
            Near(room, RigMath.CompletedRoomStep(walk + room - walk, room));
            // Walking blocked by a wall while also stepping into it books nothing.
            Near(Vector3.zero, RigMath.CompletedRoomStep(Vector3.zero - walk, Vector3.forward * 0.5f * dt));
        }

        [Test]
        public void CollisionPushOppositeRoomRequestIsPreserved()
        {
            Near(Vector3.zero, RigMath.CompletedRoomStep(Vector3.left * 0.2f, Vector3.right * 0.1f));
        }

        [Test]
        public void PartialRoomScaleOnlyConsumesActualMotion()
        {
            Near(Vector3.right * 0.03f, RigMath.CompletedRoomStep(Vector3.right * 0.03f, Vector3.right * 0.1f));
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(0f, 0f, 0.5f)]
        [TestCase(0f, 0f, 10f)]
        [TestCase(0f, 0.7f, 0f)]
        [TestCase(-0.4f, 0.2f, -0.3f)]
        public void BendSolverPreservesBothBoneLengths(float x, float y, float z)
        {
            const float upper = 0.32f, lower = 0.27f;
            Vector3 bend = RigMath.SolveBend(Vector3.zero, new Vector3(x, y, z), Vector3.up,
                upper, lower, out Vector3 end);
            Assert.That(bend.x, Is.Not.NaN);
            Assert.That(bend.y, Is.Not.NaN);
            Assert.That(bend.z, Is.Not.NaN);
            Assert.That(bend.magnitude, Is.EqualTo(upper).Within(0.0001f));
            Assert.That(Vector3.Distance(bend, end), Is.EqualTo(lower).Within(0.0001f));
        }

        [Test]
        public void CollinearPoleHasFiniteDeterministicFallback()
        {
            Vector3 first = RigMath.SolveBend(Vector3.zero, Vector3.forward, Vector3.forward, 0.6f, 0.6f, out _);
            Vector3 second = RigMath.SolveBend(Vector3.zero, Vector3.forward, Vector3.forward, 0.6f, 0.6f, out _);
            Near(first, second);
            Assert.That(first.magnitude, Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void PoleSelectsElbowSide()
        {
            Vector3 a = RigMath.SolveBend(Vector3.zero, Vector3.forward * 0.5f, Vector3.right, 0.3f, 0.3f, out _);
            Vector3 b = RigMath.SolveBend(Vector3.zero, Vector3.forward * 0.5f, Vector3.left, 0.3f, 0.3f, out _);
            Assert.That(a.x, Is.GreaterThan(0f));
            Assert.That(b.x, Is.LessThan(0f));
            Assert.That(a.z, Is.EqualTo(b.z).Within(0.0001f));
        }

        [TestCase(0f)]
        [TestCase(89.9f)]
        [TestCase(90f)]
        [TestCase(-90f)]
        [TestCase(45f)]
        public void YawKeepsHeadingWhenLookingStraightUpOrDown(float pitch)
        {
            Quaternion yaw = RigMath.Yaw(Quaternion.Euler(pitch, 30f, 0f));
            Near(Quaternion.Euler(0f, 30f, 0f) * Vector3.forward, yaw * Vector3.forward, 0.001f);
        }

        [Test]
        public void GroundSpringIsBoundedBothWays()
        {
            Assert.That(RigMath.GroundSpring(1f, -20f, 300f, 32f, 60f, 20f), Is.EqualTo(60f));
            Assert.That(RigMath.GroundSpring(-1f, 20f, 300f, 32f, 60f, 20f), Is.EqualTo(-20f));
            Assert.That(RigMath.GroundSpring(0f, 0f, 300f, 32f, 60f, 20f), Is.EqualTo(0f));
        }

        [Test]
        public void ClimbingHangingStillOnlySupportsWeight()
        {
            Vector3 a = RigMath.ClimbAcceleration(Vector3.zero, Vector3.zero, Vector3.zero, Physics.gravity, 12f, 2.5f, 30f, 1f / 90f);
            Near(-Physics.gravity, a);
        }

        [Test]
        public void ClimbingCannotExceedSpeedOrAccelerationLimits()
        {
            const float dt = 1f / 90f;
            // A huge pull (controller yanked 5 m) still targets at most the climb speed and the
            // total acceleration including gravity support stays within the limit.
            Vector3 a = RigMath.ClimbAcceleration(Vector3.up * 5f, Vector3.zero, Vector3.zero, Physics.gravity, 12f, 2.5f, 30f, dt);
            Assert.That(a.magnitude, Is.LessThanOrEqualTo(30.0001f));
            Vector3 v = Vector3.zero;
            for (int i = 0; i < 900; ++i) v += (RigMath.ClimbAcceleration(Vector3.up * 5f, Vector3.zero, v, Physics.gravity, 12f, 2.5f, 30f, dt) + Physics.gravity) * dt;
            Assert.That(v.magnitude, Is.LessThanOrEqualTo(2.5f + 0.05f), "Body speed must converge to the climb speed limit, not grow.");
        }

        [Test]
        public void ShouldersAndRootMatchRestPoseWhenEyesAreAtRest()
        {
            RigSettings settings = ScriptableObject.CreateInstance<RigSettings>();
            try
            {
                Vector3 offset = new Vector3(2f, 0f, -1f);
                Quaternion turned = Quaternion.Euler(0f, 90f, 0f);
                Near(offset + turned * settings.leftShoulderPosition,
                    settings.ShoulderFromEye(offset + turned * settings.eyePosition, turned, true));
                Near(offset, settings.RootFromEye(offset + turned * settings.eyePosition, turned));
                // Looking down pivots about the neck: the head bone moves back, not the eyes.
                Vector3 head = settings.HeadBoneFromEye(settings.eyePosition, Quaternion.Euler(60f, 0f, 0f));
                Assert.That(head.z, Is.LessThan(settings.headBonePosition.z));
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
