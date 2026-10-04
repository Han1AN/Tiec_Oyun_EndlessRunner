using UnityEngine;

namespace TIEC.Runner
{
    [CreateAssetMenu(menuName = "TIEC/Runner Settings", fileName = "RunnerSettings")]
    public sealed class RunnerConfig : ScriptableObject
    {
        [Header("Parkur ve süre")]
        [Min(5)] public float targetSeconds = 30;
        [Min(1)] public float courseLength = 217;
        [Tooltip("1 = yaklaşık 30 sn. 1.2 = %20 daha hızlı, 25 sn.")]
        [Range(.25f, 3)] public float speedMultiplier = 1.2f;
        [Header("Hızlanma profili")]
        [Min(.05f)] public float startWeight = .35f;
        [Min(.05f)] public float endWeight = 1.8f;
        [Range(.5f, 4)] public float accelerationPower = 1.3f;
        [Header("Direksiyon ve yol sınırı")]
        [Min(1)] public float lateralSpeed = 8.5f;
        [Range(1, 6)] public float roadHalfWidth = 5.6f;
        [Range(0, 25)] public float visualLeanDegrees = 12;
        public LayerMask obstacleMask;
        public LayerMask groundMask;
        public float Duration => Mathf.Max(.1f, targetSeconds / Mathf.Max(.05f, speedMultiplier));
        float Area => startWeight + (Mathf.Max(startWeight, endWeight) - startWeight) / (accelerationPower + 1);

        // The integrated speed profile reaches courseLength exactly at Duration.
        public float DistanceAt(float seconds)
        {
            float t = Mathf.Clamp01(seconds / Duration);
            return courseLength * (startWeight * t + (Mathf.Max(startWeight, endWeight) - startWeight)
                * Mathf.Pow(t, accelerationPower + 1) / (accelerationPower + 1)) / Area;
        }
        public float SpeedAt(float seconds)
        {
            float t = Mathf.Clamp01(seconds / Duration);
            return courseLength / Duration * (startWeight + (Mathf.Max(startWeight, endWeight) - startWeight)
                * Mathf.Pow(t, accelerationPower)) / Area;
        }
        public float TimeAtDistance(float distance)
        {
            float lo = 0, hi = Duration;
            for (int i = 0; i < 24; i++) { float mid = (lo + hi) * .5f; if (DistanceAt(mid) < distance) lo = mid; else hi = mid; }
            return (lo + hi) * .5f;
        }
        void OnValidate()
        {
            targetSeconds = Mathf.Max(5, targetSeconds); courseLength = Mathf.Max(1, courseLength);
            startWeight = Mathf.Max(.05f, startWeight); endWeight = Mathf.Max(startWeight, endWeight);
        }
    }
}
