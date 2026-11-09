using UnityEngine;

namespace UniversalRig.Weapons
{
    // Detachable magazine or a single shell. The root pose is what the socket aligns.
    [RequireComponent(typeof(Rigidbody), typeof(PhysicsGrabbable))]
    public sealed class Magazine : MonoBehaviour
    {
        public string ammoId = "9mm";
        [Min(1)] public int capacity = 1;
        [Min(0)] public int rounds = 1;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        Rigidbody body;
        public float InsertionBlockedUntil { get; internal set; }
        public MagazineSocket Socket { get; private set; }

        internal bool TryClaim(MagazineSocket socket)
        {
            if (Socket && Socket != socket) return false;
            Socket = socket;
            return true;
        }

        internal void ReleaseSocket(MagazineSocket socket)
        {
            if (Socket == socket) Socket = null;
        }

        void OnDisable() { if (Socket) Socket.Unseat(); }


        void OnValidate() { rounds = Mathf.Clamp(rounds, 0, Mathf.Max(1, capacity)); }
    }
}
