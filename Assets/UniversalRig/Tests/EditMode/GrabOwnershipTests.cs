using NUnit.Framework;
using UnityEngine;

namespace UniversalRig.Tests
{
    public sealed class GrabOwnershipTests
    {
        [Test]
        public void ReleasingPrimaryPromotesRemainingHandAndDisabledItemRejectsGrabs()
        {
            GameObject itemObject = new GameObject("item");
            GameObject leftObject = new GameObject("left");
            GameObject rightObject = new GameObject("right");
            try
            {
                PhysicsGrabbable item = itemObject.AddComponent<PhysicsGrabbable>();
                PhysicsHand left = leftObject.AddComponent<PhysicsHand>();
                PhysicsHand right = rightObject.AddComponent<PhysicsHand>();
                Assert.That(item.TryHold(left), Is.True);
                Assert.That(item.TryHold(left), Is.False, "A duplicate holder must not consume capacity.");
                Assert.That(item.TryHold(right), Is.True);
                Assert.That(item.PrimaryHand, Is.SameAs(left));
                item.Release(left);
                Assert.That(item.PrimaryHand, Is.SameAs(right));
                Assert.That(item.HoldingHands, Is.EqualTo(1));
                item.Release(right);
                item.enabled = false;
                Assert.That(item.HoldingHands, Is.Zero);
                Assert.That(item.TryHold(left), Is.False, "Disabled items must not be grabbed.");
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(leftObject);
                Object.DestroyImmediate(rightObject);
            }
        }

        [Test]
        public void SingleHandLimitAndKinematicBodiesAreRespected()
        {
            GameObject itemObject = new GameObject("item");
            GameObject handObject = new GameObject("hand");
            try
            {
                PhysicsGrabbable item = itemObject.AddComponent<PhysicsGrabbable>();
                PhysicsHand hand = handObject.AddComponent<PhysicsHand>();
                item.Body.isKinematic = true;
                Assert.That(item.TryHold(hand), Is.False);
                item.Body.isKinematic = false;
                item.maximumHands = 1;
                Assert.That(item.TryHold(hand), Is.True);
                Assert.That(item.TryHold(hand), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(handObject);
            }
        }
    }
}
