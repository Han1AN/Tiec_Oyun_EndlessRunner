using System;
using System.Collections.Generic;
using UnityEngine;

namespace TIEC.Runner
{
    /// <summary>Loops cheering gestures on a humanoid or a named Generic skeleton.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class FinishSpectator : MonoBehaviour
    {
        public enum CheerStyle { Clap, FistPump, Wave, Flag }

        public Animator animator;
        public CheerStyle style;
        [Tooltip("Cycles per second. Give nearby spectators slightly different values.")]
        [Range(0.2f, 2f)] public float speed = 0.85f;
        [Range(0f, 1f)] public float phase;
        [Range(0f, 1f)] public float enthusiasm = 1f;
        public WavingFlag flag;

        [Serializable]
        struct BonePose
        {
            public Transform bone;
            public Vector3 position;
            public Quaternion rotation;
        }

        // Serialized so a saved authoring preview never becomes the new bind pose.
        [SerializeField, HideInInspector] List<BonePose> restPose = new List<BonePose>();
        [SerializeField, HideInInspector] Animator cachedAnimator;

        sealed class Limb
        {
            public Transform upper, lower, end;
            public bool Valid => upper && lower && end;
            public float Length => Valid ? Vector3.Distance(upper.position, lower.position)
                + Vector3.Distance(lower.position, end.position) : 0f;
        }

        readonly Dictionary<string, Transform> names = new Dictionary<string, Transform>();
        Limb leftArm, rightArm, leftLeg, rightLeg;
        Transform hips, chest, head;
        Transform leftFinger, rightFinger, leftIndex, rightIndex, leftLittle, rightLittle;
        Transform[] leftFingerJoints, rightFingerJoints;
        bool initialized;
        float stature = 1.75f;

        public bool HasArmRig => initialized && leftArm.Valid && rightArm.Valid;

        public void Configure(Animator source, CheerStyle gesture, float startPhase,
            Material flagMaterial = null, Material poleMaterial = null)
        {
            RestorePose();
            animator = source;
            style = gesture;
            phase = Mathf.Repeat(startPhase, 1f);
            CacheRig();
            if (style == CheerStyle.Flag && rightArm.Valid)
            {
                if (!flag)
                {
                    var flagObject = new GameObject("Red cheering flag");
                    flagObject.transform.SetParent(transform, false);
                    flag = flagObject.AddComponent<WavingFlag>();
                }
                flag.Configure(rightArm.end, transform, flagMaterial, poleMaterial,
                    stature / 1.75f, phase, speed);
            }
        }

        void Awake() => CacheRig();
        void OnEnable() { if (!initialized) CacheRig(); }
        void LateUpdate() => ApplyPose(Time.time);
        void OnDisable() => RestorePose();

        /// <summary>Can be called from an editor authoring script for a static preview.</summary>
        public void ApplyPose(float time)
        {
            if (!Finite(time)) return;
            if (!initialized) CacheRig();
            if (!initialized) return;
            RestorePose();

            float t = (time * Mathf.Clamp(speed, 0.2f, 2f) + phase) * Mathf.PI * 2f;
            float energy = Mathf.Clamp01(enthusiasm);
            Vector3 up = transform.up, right = transform.right, forward = transform.forward;
            Vector3 leftFoot = leftLeg.Valid ? leftLeg.end.position : Vector3.zero;
            Vector3 rightFoot = rightLeg.Valid ? rightLeg.end.position : Vector3.zero;
            Quaternion leftFootRotation = leftLeg.Valid ? leftLeg.end.rotation : Quaternion.identity;
            Quaternion rightFootRotation = rightLeg.Valid ? rightLeg.end.rotation : Quaternion.identity;

            // Lower the pelvis during each beat; leg solving keeps both feet planted.
            if (hips)
            {
                hips.position += stature * energy * (right * (0.008f * Mathf.Sin(t))
                    - up * (0.012f * (0.5f + 0.5f * Mathf.Sin(t * 2f))));
                RotateWorld(hips, forward, 1.8f * energy * Mathf.Sin(t));
            }
            if (chest)
            {
                RotateWorld(chest, forward, 3.5f * energy * Mathf.Sin(t + 0.3f));
                RotateWorld(chest, right, 1.6f * energy * Mathf.Sin(t * 2f));
            }
            if (head) RotateWorld(head, right, 3f * energy * Mathf.Sin(t * 2f + 0.5f));

            if (leftArm.Valid && rightArm.Valid)
            {
                float reach = (leftArm.Length + rightArm.Length) * 0.5f;
                Vector3 center = (leftArm.upper.position + rightArm.upper.position) * 0.5f;
                Vector3 l, r, leftHint, rightHint;
                switch (style)
                {
                    case CheerStyle.Clap:
                        float separation = reach * (0.04f + 0.31f * Mathf.Abs(Mathf.Sin(t * 1.35f)));
                        Vector3 clapCenter = center + forward * (reach * 0.62f)
                            - up * (reach * (0.06f + 0.025f * Mathf.Sin(t)));
                        l = clapCenter - right * separation;
                        r = clapCenter + right * separation;
                        leftHint = -right - up * 0.6f + forward * 0.3f;
                        rightHint = right - up * 0.6f + forward * 0.3f;
                        break;
                    case CheerStyle.FistPump:
                        l = leftArm.upper.position + reach * (-right * 0.34f
                            + up * (0.67f + 0.12f * Mathf.Sin(t * 2f)) + forward * 0.15f);
                        r = rightArm.upper.position + reach * (right * 0.34f
                            + up * (0.67f + 0.12f * Mathf.Sin(t * 2f + 0.7f)) + forward * 0.15f);
                        leftHint = -right + forward * 0.5f;
                        rightHint = right + forward * 0.5f;
                        break;
                    default:
                        l = leftArm.upper.position + reach * (-right * 0.45f
                            + up * (0.39f + 0.18f * Mathf.Sin(t * 2f + 1.2f)) + forward * 0.32f);
                        r = rightArm.upper.position + reach * (right * (0.17f + 0.22f * Mathf.Sin(t))
                            + up * (0.78f + 0.07f * Mathf.Cos(t * 2f)) + forward * 0.16f);
                        leftHint = -right + forward * 0.4f;
                        rightHint = right + forward * 0.4f;
                        break;
                }
                SolveLimb(leftArm, l, leftHint);
                SolveLimb(rightArm, r, rightHint);
                // Aim fingers using their actual bind geometry instead of imported local axes.
                Vector3 leftFingers = up + forward * 0.15f;
                Vector3 rightFingers = up + (style == CheerStyle.Wave
                    ? right * (0.4f * Mathf.Sin(t * 2f)) : Vector3.zero);
                Vector3 leftPalm = style == CheerStyle.Clap ? right : forward;
                Vector3 rightPalm = style == CheerStyle.Clap ? -right : forward;
                AimHand(leftArm.end, leftFinger, leftIndex, leftLittle, leftFingers, leftPalm, true);
                AimHand(rightArm.end, rightFinger, rightIndex, rightLittle, rightFingers, rightPalm, false);
                if (style == CheerStyle.FistPump || style == CheerStyle.Wave)
                    CurlFingers(leftFingerJoints, Vector3.Cross(leftFingers, leftPalm));
                if (style == CheerStyle.FistPump || style == CheerStyle.Flag)
                    CurlFingers(rightFingerJoints, Vector3.Cross(rightFingers, rightPalm));
            }

            if (leftLeg.Valid)
            {
                SolveLimb(leftLeg, leftFoot, forward);
                leftLeg.end.rotation = leftFootRotation;
            }
            if (rightLeg.Valid)
            {
                SolveLimb(rightLeg, rightFoot, forward);
                rightLeg.end.rotation = rightFootRotation;
            }
            if (flag) flag.ApplyPose(time);
        }

        public void RestorePose()
        {
            for (int i = 0; i < restPose.Count; i++)
            {
                BonePose pose = restPose[i];
                if (!pose.bone) continue;
                pose.bone.localPosition = pose.position;
                pose.bone.localRotation = pose.rotation;
            }
        }

        public void CacheRig()
        {
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (!animator) { initialized = false; return; }
            names.Clear();
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
            {
                string key = Canonical(bone.name);
                if (!names.ContainsKey(key)) names.Add(key, bone);
            }
            if (cachedAnimator != animator || restPose.Count == 0)
            {
                restPose.Clear();
                foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.GetComponent<WavingFlag>()) continue;
                    restPose.Add(new BonePose { bone = bone, position = bone.localPosition,
                        rotation = bone.localRotation });
                }
                cachedAnimator = animator;
            }
            else RestorePose();

            hips = Bone(HumanBodyBones.Hips, "hips", "pelvis");
            chest = Bone(HumanBodyBones.Chest, "chest", "spine2", "spine02", "spine3", "spine03", "spine1", "spine");
            head = Bone(HumanBodyBones.Head, "head");
            leftArm = new Limb {
                upper = Bone(HumanBodyBones.LeftUpperArm, "leftarm", "leftupperarm", "upperarml", "lupperarm", "larm"),
                lower = Bone(HumanBodyBones.LeftLowerArm, "leftforearm", "leftlowerarm", "forearml", "lowerarml", "lforearm"),
                end = Bone(HumanBodyBones.LeftHand, "lefthand", "handl", "lhand") };
            rightArm = new Limb {
                upper = Bone(HumanBodyBones.RightUpperArm, "rightarm", "rightupperarm", "upperarmr", "rupperarm", "rarm"),
                lower = Bone(HumanBodyBones.RightLowerArm, "rightforearm", "rightlowerarm", "forearmr", "lowerarmr", "rforearm"),
                end = Bone(HumanBodyBones.RightHand, "righthand", "handr", "rhand") };
            leftLeg = new Limb {
                upper = Bone(HumanBodyBones.LeftUpperLeg, "leftupleg", "leftupperleg", "thighl", "lthigh"),
                lower = Bone(HumanBodyBones.LeftLowerLeg, "leftleg", "leftlowerleg", "calfl", "shinl", "lcalf"),
                end = Bone(HumanBodyBones.LeftFoot, "leftfoot", "footl", "lfoot") };
            rightLeg = new Limb {
                upper = Bone(HumanBodyBones.RightUpperLeg, "rightupleg", "rightupperleg", "thighr", "rthigh"),
                lower = Bone(HumanBodyBones.RightLowerLeg, "rightleg", "rightlowerleg", "calfr", "shinr", "rcalf"),
                end = Bone(HumanBodyBones.RightFoot, "rightfoot", "footr", "rfoot") };
            leftFinger = Bone(HumanBodyBones.LeftMiddleProximal, "lefthandmiddle1", "leftmiddle1", "middle1l", "middleproximall");
            rightFinger = Bone(HumanBodyBones.RightMiddleProximal, "righthandmiddle1", "rightmiddle1", "middle1r", "middleproximalr");
            leftIndex = Bone(HumanBodyBones.LeftIndexProximal, "lefthandindex1", "leftindex1", "index1l", "indexproximall");
            rightIndex = Bone(HumanBodyBones.RightIndexProximal, "righthandindex1", "rightindex1", "index1r", "indexproximalr");
            leftLittle = Bone(HumanBodyBones.LeftLittleProximal, "lefthandpinky1", "leftpinky1", "pinky1l", "little1l");
            rightLittle = Bone(HumanBodyBones.RightLittleProximal, "righthandpinky1", "rightpinky1", "pinky1r", "little1r");
            leftFingerJoints = FingerJoints(true);
            rightFingerJoints = FingerJoints(false);
            if (head && leftLeg.Valid && rightLeg.Valid)
                stature = Mathf.Max(0.1f, Vector3.Distance(head.position,
                    (leftLeg.end.position + rightLeg.end.position) * 0.5f) * 1.1f);
            animator.enabled = false;
            initialized = true;
        }

        Transform Bone(HumanBodyBones id, params string[] aliases)
        {
            if (animator.avatar && animator.avatar.isValid && animator.avatar.isHuman)
            {
                Transform humanoid = animator.GetBoneTransform(id);
                if (humanoid) return humanoid;
            }
            foreach (string alias in aliases)
                if (names.TryGetValue(alias, out Transform named)) return named;
            return null;
        }

        static string Canonical(string value)
        {
            int colon = value.LastIndexOf(':');
            if (colon >= 0) value = value.Substring(colon + 1);
            value = value.ToLowerInvariant().Replace("mixamorig", "");
            var clean = new System.Text.StringBuilder(value.Length);
            foreach (char c in value) if (char.IsLetterOrDigit(c)) clean.Append(c);
            return clean.ToString();
        }

        static void SolveLimb(Limb limb, Vector3 target, Vector3 bendHint)
        {
            if (!limb.Valid || !Finite(target)) return;
            Vector3 origin = limb.upper.position;
            float a = Vector3.Distance(origin, limb.lower.position);
            float b = Vector3.Distance(limb.lower.position, limb.end.position);
            if (a < 0.0001f || b < 0.0001f || !Finite(a + b)) return;
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude < 0.000001f) return;
            float distance = Mathf.Clamp(direction.magnitude,
                Mathf.Abs(a - b) + 0.0001f, (a + b) * 0.9995f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(bendHint, direction);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.ProjectOnPlane(limb.lower.position - origin, direction);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right);
            bend.Normalize();
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 elbow = origin + direction * along + bend * height;
            Vector3 reachableTarget = origin + direction * distance;
            Vector3 oldUpper = limb.lower.position - origin;
            if (oldUpper.sqrMagnitude > 0.000001f)
                limb.upper.rotation = Quaternion.FromToRotation(oldUpper, elbow - origin) * limb.upper.rotation;
            Vector3 oldLower = limb.end.position - limb.lower.position;
            Vector3 newLower = reachableTarget - limb.lower.position;
            if (oldLower.sqrMagnitude > 0.000001f && newLower.sqrMagnitude > 0.000001f)
                limb.lower.rotation = Quaternion.FromToRotation(oldLower, newLower) * limb.lower.rotation;
        }

        Transform[] FingerJoints(bool left)
        {
            string prefix = left ? "left" : "right", suffix = left ? "l" : "r";
            var joints = new List<Transform>();
            string[] digits = { "index", "middle", "ring", "pinky" };
            HumanBodyBones first = left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal;
            for (int digit = 0; digit < 4; digit++)
                for (int segment = 1; segment <= 3; segment++)
                    joints.Add(Bone((HumanBodyBones)((int)first + digit * 3 + segment - 1),
                        prefix + "hand" + digits[digit] + segment,
                        prefix + digits[digit] + segment, digits[digit] + segment + suffix));
            return joints.ToArray();
        }

        static void AimHand(Transform hand, Transform finger, Transform index,
            Transform little, Vector3 direction, Vector3 palmDirection, bool left)
        {
            if (!hand || !finger) return;
            Vector3 current = finger.position - hand.position;
            if (current.sqrMagnitude > 0.000001f)
                hand.rotation = Quaternion.FromToRotation(current, direction) * hand.rotation;
            if (!index || !little) return;
            Vector3 fingerDirection = (finger.position - hand.position).normalized;
            Vector3 palm = Vector3.Cross(index.position - little.position, fingerDirection);
            if (!left) palm = -palm;
            Vector3 desiredPalm = Vector3.ProjectOnPlane(palmDirection, fingerDirection);
            if (palm.sqrMagnitude > 0.000001f && desiredPalm.sqrMagnitude > 0.000001f)
                hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(palm, desiredPalm,
                    fingerDirection), fingerDirection) * hand.rotation;
        }

        static void CurlFingers(Transform[] joints, Vector3 bendAxis)
        {
            if (joints == null || bendAxis.sqrMagnitude < 0.000001f) return;
            for (int i = 0; i < joints.Length; i++)
                if (joints[i]) RotateWorld(joints[i], bendAxis, i % 3 == 0 ? 60f : i % 3 == 1 ? 65f : 40f);
        }

        static void RotateWorld(Transform bone, Vector3 axis, float angle)
            => bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
