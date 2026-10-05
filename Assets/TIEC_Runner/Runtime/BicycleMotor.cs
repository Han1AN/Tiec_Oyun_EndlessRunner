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
        // Retained for map-import compatibility. Flight follows physical support, not Z markers.
        [SerializeField] RunnerJump[] jumps;
        const float GroundClearance = .05f;
        const float GroundSnapDistance = .10f;
        const float MaxStepUp = .20f;
        const float MinSupportNormalY = .60f;
        const float Gravity = 9.81f;
        const float MaxLaunchRiseSpeed = 3f;
        const float GroundSteeringAcceleration = 80f;
        const float AirSteeringAcceleration = 30f;
        const float MaxSubstepSeconds = 1f / 60f;
        const float MaxSubstepTravel = .15f;
        Quaternion frontRest, rearRest, crankRest;
        readonly Collider[] overlapResults = new Collider[1];
        float wheelAngle, verticalVelocity, lateralVelocity, distance;
        bool grounded;
        public float Distance => distance;
        public bool Grounded => grounded;
        public float VerticalVelocity => verticalVelocity;
        public Collider LastObstacle { get; private set; }
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
            distance = 0; wheelAngle = 0; verticalVelocity = 0; lateralVelocity = 0;
            grounded = true; LastObstacle = null;
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
            fraction = 1;
            LastObstacle = null;
            if (deltaTime <= 0) return true;
            float initialDistance = distance;
            float horizontalTravel = Mathf.Sqrt(Mathf.Pow(targetDistance - initialDistance, 2)
                + Mathf.Pow(settings.lateralSpeed * deltaTime, 2));
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(deltaTime / MaxSubstepSeconds,
                horizontalTravel / MaxSubstepTravel)));
            float dt = deltaTime / steps;
            Vector3 half = hitbox.size * .485f;
            for (int step = 0; step < steps; step++)
            {
                Vector3 previous = body.position;
                float targetLateralVelocity = (body.rotation * Vector3.right).x * Mathf.Clamp(steer, -1, 1) * settings.lateralSpeed;
                lateralVelocity = Mathf.MoveTowards(lateralVelocity, targetLateralVelocity,
                    (grounded ? GroundSteeringAcceleration : AirSteeringAcceleration) * dt);
                float nextDistance = Mathf.Lerp(initialDistance, targetDistance, (step + 1f) / steps);
                Vector3 next = new Vector3(Mathf.Clamp(previous.x + lateralVelocity * dt, MinX, MaxX),
                    previous.y, startPosition.z - nextDistance);
                if (next.x <= MinX || next.x >= MaxX) lateralVelocity = 0;
                UpdateHeight(previous, ref next, dt);
                Vector3 travel = next - previous;
                Vector3 center = previous + body.rotation * hitbox.center;
                float localFraction = 1;
                // Sweep every substep so thin walls still stop the faster rider.
                if (Physics.OverlapBoxNonAlloc(center, half, overlapResults, body.rotation,
                    settings.obstacleMask, QueryTriggerInteraction.Collide) > 0)
                { localFraction = 0; LastObstacle = overlapResults[0]; }
                else if (travel.sqrMagnitude > .000001f && Physics.BoxCast(center, half, travel.normalized,
                    out RaycastHit hit, body.rotation, travel.magnitude, settings.obstacleMask, QueryTriggerInteraction.Collide))
                {
                    localFraction = Mathf.Clamp01(Mathf.Max(0, hit.distance - .01f) / travel.magnitude);
                    LastObstacle = hit.collider;
                }
                Vector3 final = Vector3.Lerp(previous, next, localFraction);
                body.position = final;
                transform.position = final;
                distance = Mathf.Lerp(distance, nextDistance, localFraction);
                if (localFraction < 1)
                {
                    fraction = (step + localFraction) / steps;
                    Animate(steer, speed, deltaTime * fraction);
                    return false;
                }
            }
            Animate(steer, speed, deltaTime);
            return true;
        }
        void UpdateHeight(Vector3 previous, ref Vector3 next, float dt)
        {
            // A nearby probe can climb a continuous ramp, but cannot pull the rider onto a high adjacent lane.
            Vector3 origin = new Vector3(next.x, previous.y + MaxStepUp, next.z);
            bool supported = Physics.Raycast(origin, Vector3.down, out RaycastHit ground, 30f,
                settings.groundMask, QueryTriggerInteraction.Ignore) && ground.normal.y >= MinSupportNormalY;
            float supportHeight = supported ? ground.point.y + GroundClearance : float.NegativeInfinity;
            float heightDifference = supportHeight - previous.y;
            if (grounded && supported && heightDifference >= -GroundSnapDistance && heightDifference <= MaxStepUp)
            {
                next.y = supportHeight;
                Vector3 horizontalVelocity = (next - previous) / dt;
                verticalVelocity = -(ground.normal.x * horizontalVelocity.x + ground.normal.z * horizontalVelocity.z) / ground.normal.y;
                return;
            }
            if (grounded)
            {
                grounded = false;
                // Preserve ramp momentum with a modest rise above the edge (at most about 46 cm).
                verticalVelocity = Mathf.Min(verticalVelocity, MaxLaunchRiseSpeed);
            }
            next.y = previous.y + verticalVelocity * dt - .5f * Gravity * dt * dt;
            verticalVelocity -= Gravity * dt;
            // Only descending contact lands the rider; the road cannot reset height while airborne.
            if (supported && verticalVelocity <= 0 && previous.y >= supportHeight - .001f && next.y <= supportHeight)
            {
                next.y = supportHeight;
                verticalVelocity = 0;
                grounded = true;
            }
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
