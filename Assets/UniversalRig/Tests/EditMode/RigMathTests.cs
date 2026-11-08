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
    }
}
