using System;
using UnityEngine;

namespace TIEC.Runner
{
    [Serializable]
    public struct RunnerJump
    {
        public float laneX, takeoffZ, landingZ, takeoffHeight, arcHeight;
    }

    [DefaultExecutionOrder(-100), RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class BicycleMotor : MonoBehaviour
    {
        [SerializeField] RunnerConfig settings;
        [SerializeField] RunnerInput input;
        [SerializeField] Rigidbody body;
        [SerializeField] BoxCollider hitbox;
        [SerializeField] Transform visualPivot;
        [SerializeField] Animator riderAnimator;
        [SerializeField] Transform frontWheel, rearWheel, crank;
        [SerializeField] Vector3 startPosition = new Vector3(0, .08f, -3);
        [SerializeField] RunnerJump[] jumps;
        Quaternion frontRest, rearRest, crankRest;
        float wheelAngle;
        int activeJump = -1;
        float distance;
        public float Distance => distance;
        public float MinX => -settings.roadHalfWidth + hitbox.size.x * .5f;
        public float MaxX => settings.roadHalfWidth - hitbox.size.x * .5f;
        public void Configure(RunnerConfig config, RunnerInput controls, Transform pivot, Animator animator,
            Transform front, Transform rear, Transform pedals, RunnerJump[] jumpData)
        {
            settings = config; input = controls; body = GetComponent<Rigidbody>(); hitbox = GetComponent<BoxCollider>();
            visualPivot = pivot; riderAnimator = animator; frontWheel = front; rearWheel = rear; crank = pedals; jumps = jumpData;
        }
        void Awake()
        {
            if (settings == null || body == null || hitbox == null || visualPivot == null)
            { Debug.LogError("BicycleMotor: Settings, Rigidbody, Hitbox ve VisualPivot referanslarını atayın.", this); enabled = false; return; }
            if (frontWheel) frontRest = frontWheel.localRotation;
            if (rearWheel) rearRest = rearWheel.localRotation;
            if (crank) crankRest = crank.localRotation;
            body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (riderAnimator) riderAnimator.applyRootMotion = false;
            ResetToStart();
        }
        public void ResetToStart()
        {
            distance = 0; activeJump = -1; wheelAngle = 0;
            transform.SetPositionAndRotation(startPosition, Quaternion.Euler(0, 180, 0));
            if (body) { body.position = startPosition; body.rotation = transform.rotation; }
            if (visualPivot) visualPivot.localRotation = Quaternion.identity;
            if (frontWheel) frontWheel.localRotation = frontRest;
            if (rearWheel) rearWheel.localRotation = rearRest;
            if (crank) crank.localRotation = crankRest;
            if (riderAnimator) { riderAnimator.Rebind(); riderAnimator.Update(0); riderAnimator.speed = 0; }
            Physics.SyncTransforms();
        }
        public bool Advance(float targetDistance, float steer, float deltaTime, float speed, out float fraction)
        {
            Vector3 previous = body.position;
            // Steering follows the bicycle's local right, which is world -X on this -Z course.
            float lateralMove = (body.rotation * Vector3.right).x * steer * settings.lateralSpeed * deltaTime;
            Vector3 next = new Vector3(Mathf.Clamp(previous.x + lateralMove, MinX, MaxX),
                previous.y, startPosition.z - targetDistance);
            UpdateHeight(previous, ref next);
            Vector3 travel = next - previous;
            Vector3 half = hitbox.size * .5f;
            Vector3 center = previous + body.rotation * hitbox.center;
            fraction = 1;
            // Sweep the entire step: thin hazards cannot be skipped at high speed or low frame rate.
            if (Physics.CheckBox(center, half * .97f, body.rotation, settings.obstacleMask, QueryTriggerInteraction.Collide))
                fraction = 0;
            else if (travel.sqrMagnitude > .000001f && Physics.BoxCast(center, half * .97f, travel.normalized,
                out RaycastHit hit, body.rotation, travel.magnitude, settings.obstacleMask, QueryTriggerInteraction.Collide))
                fraction = Mathf.Clamp01(Mathf.Max(0, hit.distance - .01f) / travel.magnitude);
            Vector3 final = Vector3.Lerp(previous, next, fraction);
            // Physics queries and camera observe the same authoritative kinematic pose.
            body.position = final;
            transform.position = final;
            distance = Mathf.Lerp(distance, targetDistance, fraction);
            Animate(steer, speed, deltaTime);
            return fraction >= 1;
        }
        void UpdateHeight(Vector3 previous, ref Vector3 next)
        {
            if (activeJump < 0 && jumps != null)
                for (int i = 0; i < jumps.Length; i++)
                    if (previous.z > jumps[i].takeoffZ && next.z <= jumps[i].takeoffZ
                        && Mathf.Abs(next.x - jumps[i].laneX) < 1.28f) { activeJump = i; break; }
            if (activeJump >= 0)
            {
                var jump = jumps[activeJump];
                float t = Mathf.Clamp01((jump.takeoffZ - next.z) / (jump.takeoffZ - jump.landingZ));
                next.y = Mathf.Lerp(jump.takeoffHeight, .08f, t) + Mathf.Sin(t * Mathf.PI) * jump.arcHeight;
                if (t >= 1) activeJump = -1;
                return;
            }
            if (Physics.Raycast(new Vector3(next.x, 5, next.z), Vector3.down, out RaycastHit ground, 6,
                settings.groundMask, QueryTriggerInteraction.Ignore)) next.y = ground.point.y + .05f;
        }
        void Animate(float steer, float speed, float dt)
        {
            if (visualPivot) visualPivot.localRotation = Quaternion.Slerp(visualPivot.localRotation,
                Quaternion.Euler(0, steer * 8, -steer * settings.visualLeanDegrees), 1 - Mathf.Exp(-12 * dt));
            wheelAngle = Mathf.Repeat(wheelAngle + speed * dt / .31f * Mathf.Rad2Deg, 360);
            if (frontWheel) frontWheel.localRotation = frontRest * Quaternion.AngleAxis(wheelAngle, Vector3.right);
            if (rearWheel) rearWheel.localRotation = rearRest * Quaternion.AngleAxis(wheelAngle, Vector3.right);
            if (crank) crank.localRotation = crankRest * Quaternion.AngleAxis(wheelAngle * .45f, Vector3.right);
            if (riderAnimator) riderAnimator.speed = Mathf.Clamp(speed / 5, .65f, 2.5f);
        }
        public void StopVisuals() { if (riderAnimator) riderAnimator.speed = 0; }
    }
}
