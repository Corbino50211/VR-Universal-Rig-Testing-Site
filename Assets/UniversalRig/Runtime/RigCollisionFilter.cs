using UnityEngine;

namespace UniversalRig
{
    // Local pair filtering keeps the prototype's body/hands from fighting each other.
    // It does not change the global layer collision matrix or other players' pairs.
    [DefaultExecutionOrder(-250)]
    public sealed class RigCollisionFilter : MonoBehaviour
    {
        private void OnEnable() { RefreshPairs(); }
        private void Start() { RefreshPairs(); }

        public void RefreshPairs()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; ++i)
                for (int j = i + 1; j < colliders.Length; ++j)
                    if (colliders[i].enabled && colliders[j].enabled &&
                        colliders[i].gameObject.activeInHierarchy && colliders[j].gameObject.activeInHierarchy)
                        Physics.IgnoreCollision(colliders[i], colliders[j], true);
        }
    }
}
