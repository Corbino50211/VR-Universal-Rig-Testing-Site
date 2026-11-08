using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig
{
    // Visual pose prototype only. This is not a full-body active ragdoll.
    // Physical hands/body remain authoritative; this component never moves them.
    [DefaultExecutionOrder(100)]
    public sealed class AvatarPoseDriver : MonoBehaviour
    {
        public Animator avatar;
        public PhysicsRig rig;
        public RigInput input;
        public PhysicsHand leftHand;
        public PhysicsHand rightHand;
        public Vector3 leftWristEuler;
        public Vector3 rightWristEuler;
        public float eyeOffsetAboveHeadBone = 0.08f;
        private Transform hips, head;
        private float restHipHeight, restEyeHeight;
        private Quaternion restHeadRotation;
        private readonly List<Transform> bones = new List<Transform>();
        private readonly List<Quaternion> restRotations = new List<Quaternion>();
        private readonly List<Vector3> restPositions = new List<Vector3>();
        private Limb leftArm, rightArm, leftLeg, rightLeg;

        private sealed class Limb
        {
            public Transform root, mid, tip;
            public float upper, lower;
            public Vector3 restTip;
            public Quaternion restRotation;
        }

        private void Start()
        {
            if (!avatar || !avatar.isHuman || !avatar.avatar || !avatar.avatar.isValid || !rig || !input)
            {
                Debug.LogError("Universal Rig: visual driver requires a valid Humanoid avatar and rig references.", this);
                enabled = false;
                return;
            }
            avatar.enabled = false;
            avatar.applyRootMotion = false;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; ++i)
            {
                Transform bone = avatar.GetBoneTransform((HumanBodyBones)i);
                if (!bone) continue;
                bones.Add(bone);
                restRotations.Add(bone.localRotation);
                restPositions.Add(bone.localPosition);
            }
            hips = avatar.GetBoneTransform(HumanBodyBones.Hips);
            head = avatar.GetBoneTransform(HumanBodyBones.Head);
            if (!hips || !head) { enabled = false; return; }
            restHipHeight = transform.InverseTransformPoint(hips.position).y;
            restEyeHeight = transform.InverseTransformPoint(head.position).y + eyeOffsetAboveHeadBone;
            restHeadRotation = Quaternion.Inverse(transform.rotation) * head.rotation;
            leftArm = MakeLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            rightArm = MakeLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            leftLeg = MakeLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            rightLeg = MakeLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        }

        private Limb MakeLimb(HumanBodyBones root, HumanBodyBones mid, HumanBodyBones tip)
        {
            var limb = new Limb { root = avatar.GetBoneTransform(root), mid = avatar.GetBoneTransform(mid), tip = avatar.GetBoneTransform(tip) };
            if (!limb.root || !limb.mid || !limb.tip) return null;
            limb.upper = Vector3.Distance(limb.root.position, limb.mid.position);
            limb.lower = Vector3.Distance(limb.mid.position, limb.tip.position);
            limb.restTip = transform.InverseTransformPoint(limb.tip.position);
            limb.restRotation = Quaternion.Inverse(transform.rotation) * limb.tip.rotation;
            return limb;
        }

        private void LateUpdate()
        {
            if (!hips || !input.HeadTracked) return;
            Vector3 forward = RigMath.Planar(input.head.forward);
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            transform.SetPositionAndRotation(rig.transform.position, Quaternion.LookRotation(forward, Vector3.up));
            for (int i = 0; i < bones.Count; ++i)
                bones[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
            float eyeHeight = input.head.position.y - rig.transform.position.y;
            float hipHeight = Mathf.Clamp(restHipHeight + eyeHeight - restEyeHeight, 0.25f, restHipHeight + 0.1f);
            hips.position += Vector3.up * (hipHeight - restHipHeight);
            head.rotation = input.head.rotation * restHeadRotation;
            if (leftHand && input.LeftTracked)
                Solve(leftArm, leftHand.transform.position, transform.position + transform.rotation * new Vector3(-0.7f, hipHeight, -0.35f),
                    leftHand.transform.rotation * Quaternion.Euler(leftWristEuler));
            if (rightHand && input.RightTracked)
                Solve(rightArm, rightHand.transform.position, transform.position + transform.rotation * new Vector3(0.7f, hipHeight, -0.35f),
                    rightHand.transform.rotation * Quaternion.Euler(rightWristEuler));
            SolveFoot(leftLeg);
            SolveFoot(rightLeg);
        }

        private void SolveFoot(Limb limb)
        {
            if (limb == null) return;
            Vector3 target = transform.TransformPoint(limb.restTip);
            if (Physics.Raycast(target + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 0.8f,
                rig.settings.environmentMask, QueryTriggerInteraction.Ignore)) target.y = hit.point.y + limb.restTip.y;
            Solve(limb, target, limb.root.position + transform.forward,
                transform.rotation * limb.restRotation);
        }

        private static void Solve(Limb limb, Vector3 target, Vector3 pole, Quaternion rotation)
        {
            if (limb == null) return;
            Vector3 bend = RigMath.SolveBend(limb.root.position, target, pole, limb.upper, limb.lower, out Vector3 end);
            limb.root.rotation = Quaternion.FromToRotation(limb.mid.position - limb.root.position,
                bend - limb.root.position) * limb.root.rotation;
            limb.mid.rotation = Quaternion.FromToRotation(limb.tip.position - limb.mid.position,
                end - limb.mid.position) * limb.mid.rotation;
            limb.tip.rotation = rotation;
        }
    }
}
