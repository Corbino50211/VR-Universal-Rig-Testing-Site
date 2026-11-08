using System;
using UnityEditor;
using UnityEngine;

namespace UniversalRig.Editor
{
    // Measures the fixed Humanoid avatar into RigSettings so the physical rig matches it.
    // To change avatar for another game: assign RigSettings.avatarModel, then run
    // Tools/Universal Rig/Fit Avatar (or regenerate the test scene).
    public static class RigAvatarFitter
    {
        [MenuItem("Tools/Universal Rig/Fit Avatar (selected Rig Settings)")]
        private static void FitSelected()
        {
            if (!(Selection.activeObject is RigSettings settings))
                throw new InvalidOperationException("Select a Rig Settings asset whose avatarModel is assigned.");
            Fit(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Universal Rig: avatar fitted into " + AssetDatabase.GetAssetPath(settings) +
                ". Regenerate or update the rig prefab so hand colliders/first-person mesh match.", settings);
        }

        public static void Fit(RigSettings settings)
        {
            GameObject instance = InstantiateAvatar(settings, out Animator animator);
            try
            {
                Transform root = instance.transform;
                Vector3 Local(HumanBodyBones bone) => root.InverseTransformPoint(animator.GetBoneTransform(bone).position);
                settings.headBonePosition = Local(HumanBodyBones.Head);
                settings.eyePosition = MeasureEyes(instance, animator);
                settings.standingEyeHeight = settings.eyePosition.y;
                settings.maximumHeight = Mathf.Max(2.1f, settings.standingEyeHeight + 0.3f);
                settings.hipHeight = Local(HumanBodyBones.Hips).y;
                settings.leftShoulderPosition = Local(HumanBodyBones.LeftUpperArm);
                settings.rightShoulderPosition = Local(HumanBodyBones.RightUpperArm);
                float left = Vector3.Distance(Local(HumanBodyBones.LeftUpperArm), Local(HumanBodyBones.LeftLowerArm)) +
                    Vector3.Distance(Local(HumanBodyBones.LeftLowerArm), Local(HumanBodyBones.LeftHand));
                float right = Vector3.Distance(Local(HumanBodyBones.RightUpperArm), Local(HumanBodyBones.RightLowerArm)) +
                    Vector3.Distance(Local(HumanBodyBones.RightLowerArm), Local(HumanBodyBones.RightHand));
                settings.armLength = 0.5f * (left + right);
                FitHand(animator, true, out settings.leftWristOffset, out settings.leftHandBoneRotation, out float length, out float width);
                FitHand(animator, false, out settings.rightWristOffset, out settings.rightHandBoneRotation, out _, out _);
                settings.handColliderCenter = new Vector3(0f, 0.3f * length, 0f);
                settings.handColliderSize = new Vector3(0.5f * length, length, 1.4f * width);
                EditorUtility.SetDirty(settings);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        public static GameObject InstantiateAvatar(RigSettings settings, out Animator animator)
        {
            if (!settings.avatarModel) throw new InvalidOperationException("Assign RigSettings.avatarModel first.");
            GameObject instance = UnityEngine.Object.Instantiate(settings.avatarModel);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator = instance.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw new InvalidOperationException("Avatar must import as a valid Humanoid (Animation Type: Humanoid).");
            }
            return instance;
        }

        // OpenXR grip frame from the rest skeleton: +Z runs pinky->index along the fist tube,
        // +Y points from the tube back toward the wrist, X is the palm normal. Expresses the
        // wrist position and hand bone rotation in that frame.
        private static void FitHand(Animator animator, bool left, out Vector3 wrist, out Quaternion bone,
            out float length, out float width)
        {
            Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Vector3 middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal).position;
            Vector3 index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal).position;
            Vector3 little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal).position;
            Vector3 along = middle - hand.position;
            length = along.magnitude;
            along /= length;
            Vector3 across = Vector3.ProjectOnPlane(index - little, along);
            width = across.magnitude;
            across /= width;
            Vector3 palm = (left ? 1f : -1f) * Vector3.Cross(across, along).normalized;
            Quaternion grip = Quaternion.LookRotation(across, -along);
            Vector3 tubeCenter = hand.position + along * (0.75f * length) + palm * (0.3f * length);
            wrist = Quaternion.Inverse(grip) * (hand.position - tubeCenter);
            bone = Quaternion.Inverse(grip) * hand.rotation;
        }

        // Uses eye bones if mapped; otherwise estimates from head-weighted vertices.
        private static Vector3 MeasureEyes(GameObject instance, Animator animator)
        {
            Transform root = instance.transform;
            Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (leftEye && rightEye) return root.InverseTransformPoint(0.5f * (leftEye.position + rightEye.position));
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 headPosition = root.InverseTransformPoint(head.position);
            SkinnedMeshRenderer renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!renderer) return headPosition + new Vector3(0f, 0.11f, 0.1f);
            var baked = new Mesh();
            renderer.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            UnityEngine.Object.DestroyImmediate(baked);
            bool[] headVertex = HeadVertices(renderer, head);
            float top = headPosition.y;
            for (int i = 0; i < vertices.Length; ++i)
                if (headVertex[i]) top = Mathf.Max(top, root.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i])).y);
            // Humans: eyes ~45% of the way from the head joint to the crown, ~4.5 cm behind the face.
            float eyeHeight = headPosition.y + 0.45f * (top - headPosition.y);
            float front = headPosition.z;
            for (int i = 0; i < vertices.Length; ++i)
            {
                if (!headVertex[i]) continue;
                Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i]));
                if (Mathf.Abs(p.x) < 0.04f && Mathf.Abs(p.y - eyeHeight) < 0.03f) front = Mathf.Max(front, p.z);
            }
            return new Vector3(0f, eyeHeight, Mathf.Max(headPosition.z, front - 0.045f));
        }

        private static bool[] HeadVertices(SkinnedMeshRenderer renderer, Transform head)
        {
            Transform[] bones = renderer.bones;
            var isHead = new bool[bones.Length];
            for (int i = 0; i < bones.Length; ++i) isHead[i] = bones[i] && bones[i].IsChildOf(head);
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            var result = new bool[weights.Length];
            for (int i = 0; i < weights.Length; ++i)
            {
                BoneWeight w = weights[i];
                float headWeight = (isHead[w.boneIndex0] ? w.weight0 : 0f) + (isHead[w.boneIndex1] ? w.weight1 : 0f) +
                    (isHead[w.boneIndex2] ? w.weight2 : 0f) + (isHead[w.boneIndex3] ? w.weight3 : 0f);
                result[i] = headWeight >= 0.5f;
            }
            return result;
        }

        // Copy of the body mesh without head/helmet triangles for the local first-person view.
        // The original renderer stays as shadows-only so the head still casts a shadow.
        public static Mesh SaveFirstPersonMesh(SkinnedMeshRenderer renderer, Transform head, string path)
        {
            bool[] headVertex = HeadVertices(renderer, head);
            Mesh mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
            mesh.name = renderer.sharedMesh.name + " First Person";
            var kept = new System.Collections.Generic.List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; ++sub)
            {
                int[] triangles = mesh.GetTriangles(sub);
                kept.Clear();
                for (int t = 0; t < triangles.Length; t += 3)
                    if (!headVertex[triangles[t]] && !headVertex[triangles[t + 1]] && !headVertex[triangles[t + 2]])
                        kept.AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
                mesh.SetTriangles(kept, sub, false);
            }
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing); // keep the asset GUID
                existing.name = mesh.name;
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
