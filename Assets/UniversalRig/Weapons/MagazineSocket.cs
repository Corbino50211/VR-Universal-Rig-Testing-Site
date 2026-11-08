using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig.Weapons
{
    // Seats a magazine with a ConfigurableJoint. It is never parented to the gun.
    // A tube port uses the same alignment test, then stores the round inside the gun:
    // the shell model has no internal tube to simulate.
    [DefaultExecutionOrder(20)]
    public sealed class MagazineSocket : MonoBehaviour
    {
        public Firearm firearm;
        public Transform seat;
        public string ammoId = "9mm";
        public bool loadsTube;
        [Min(0.005f)] public float distance = 0.035f;
        [Range(1f, 60f)] public float angle = 28f;
        public Magazine Seated { get; private set; }

        readonly Collider[] nearby = new Collider[16];
        readonly List<Collider> pairsA = new List<Collider>(8);
        readonly List<Collider> pairsB = new List<Collider>(8);
        ConfigurableJoint joint;

        void FixedUpdate()
        {
            if (!seat || !firearm) return;
            if (Seated)
            {
                PhysicsGrabbable grab = Seated.GetComponent<PhysicsGrabbable>();
                if (!Seated.gameObject.activeInHierarchy || (grab && grab.HoldingHands > 0)) Unseat();
                return;
            }
            int count = Physics.OverlapSphereNonAlloc(seat.position, distance + 0.04f, nearby, ~0, QueryTriggerInteraction.Ignore);
            Magazine best = null;
            float bestDistance = distance;
            for (int i = 0; i < count; ++i)
            {
                if (!nearby[i] || !nearby[i].attachedRigidbody) continue;
                Magazine mag = nearby[i].attachedRigidbody.GetComponent<Magazine>();
                if (!mag || mag.ammoId != ammoId) continue;
                PhysicsGrabbable grab = mag.GetComponent<PhysicsGrabbable>();
                if (!grab || grab.HoldingHands > 0 || grab.Body.isKinematic) continue;
                float gap = Vector3.Distance(mag.transform.position, seat.position);
                if (gap > bestDistance) continue;
                if (!loadsTube && Quaternion.Angle(mag.transform.rotation, seat.rotation) > angle) continue;
                best = mag;
                bestDistance = gap;
            }
            if (best) Seat(best);
        }

        void Seat(Magazine mag)
        {
            if (loadsTube)
            {
                if (!firearm.TryLoadTube(mag.rounds)) return;
                mag.rounds = 0;
                mag.gameObject.SetActive(false);
                return;
            }
            mag.transform.SetPositionAndRotation(seat.position, seat.rotation);
            mag.Body.linearVelocity = Vector3.zero;
            mag.Body.angularVelocity = Vector3.zero;
            joint = mag.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = firearm.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = firearm.transform.InverseTransformPoint(seat.position);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.breakForce = 700f;
            joint.breakTorque = 40f;
            joint.enableCollision = false;
            IgnorePair(mag, true);
            Seated = mag;
        }

        public void Unseat()
        {
            if (joint)
            {
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
                Destroy(joint);
            }
            joint = null;
            if (Seated) IgnorePair(Seated, false);
            Seated = null;
        }

        void IgnorePair(Magazine mag, bool ignore)
        {
            if (!ignore) 
            {
                for (int i = 0; i < pairsA.Count; ++i)
                    if (pairsA[i] && pairsB[i]) Physics.IgnoreCollision(pairsA[i], pairsB[i], false);
                pairsA.Clear();
                pairsB.Clear();
                return;
            }
            Collider[] magColliders = mag.GetComponentsInChildren<Collider>();
            Collider[] gunColliders = firearm.GetComponentsInChildren<Collider>();
            foreach (Collider a in magColliders)
                foreach (Collider b in gunColliders)
                {
                    if (!a || !b) continue;
                    Physics.IgnoreCollision(a, b, true);
                    pairsA.Add(a);
                    pairsB.Add(b);
                }
        }

        void OnDisable() { if (Seated) Unseat(); }
    }
}
