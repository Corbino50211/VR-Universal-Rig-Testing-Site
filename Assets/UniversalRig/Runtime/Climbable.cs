using UnityEngine;

namespace UniversalRig
{
    // Marks static or kinematic colliders (this object and its children) as climbing holds.
    // A grip on one locks the hand to the surface and lets PhysicsRig pull the body.
    public sealed class Climbable : MonoBehaviour { }
}
