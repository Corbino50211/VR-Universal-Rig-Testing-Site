using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsGrabbable : MonoBehaviour
    {
        [Range(1, 2)] public int maximumHands = 2;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public int HoldingHands => holders.Count;
        public PhysicsHand PrimaryHand => holders.Count > 0 ? holders[0] : null;
        public PhysicsHand SecondaryHand => holders.Count > 1 ? holders[1] : null;
        private Rigidbody body;
        private readonly List<PhysicsHand> holders = new List<PhysicsHand>(2);

        public bool TryHold(PhysicsHand hand)
        {
            if (!isActiveAndEnabled || Body.isKinematic || holders.Count >= maximumHands || holders.Contains(hand)) return false;
            holders.Add(hand);
            return true;
        }

        public void Release(PhysicsHand hand) { holders.Remove(hand); }

        private void OnDisable()
        {
            for (int i = holders.Count - 1; i >= 0; --i)
                if (holders[i]) holders[i].Release();
            holders.Clear();
        }
    }
}
