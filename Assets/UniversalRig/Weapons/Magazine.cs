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

        void OnValidate() { rounds = Mathf.Clamp(rounds, 0, Mathf.Max(1, capacity)); }
    }
}
