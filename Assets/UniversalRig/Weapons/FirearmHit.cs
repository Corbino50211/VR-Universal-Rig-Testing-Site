using UnityEngine;

namespace UniversalRig.Weapons
{
    // Hitscan contact. Game damage listens for this; the firearm never inspects target types.
    public struct FirearmHit
    {
        public Vector3 point;
        public Vector3 normal;
        public Vector3 direction;
        public float distance;
        public Collider collider;
        public Firearm source;
    }

    public interface IHitReceiver
    {
        void ReceiveHit(FirearmHit hit);
    }

    // Lab / test target. A hit counts only because this component is on the object.
    public sealed class HitTarget : MonoBehaviour, IHitReceiver
    {
        public int hits;
        public void ReceiveHit(FirearmHit hit) { hits++; }
    }
}
