#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TIEC.Runner
{
    /// <summary>Read-only scene audit plus isolated profile and score regression checks.</summary>
    public static class RunnerValidation
    {
        [MenuItem("TIEC/Runner/Validate Current Scene")]
        private static void ValidateMenu()
        {
            int errors = ValidateScene();
            var session = Find<RunnerSession>(SceneManager.GetActiveScene()).FirstOrDefault();
            if (session != null)
            {
                var config = Reference<RunnerConfig>(session, "settings");
                if (config != null) errors += ValidateProfiles(config);
            }
            Debug.Log($"Runner validation completed: {errors} error(s).");
        }

        /// <returns>The number of missing references or invalid scene conditions. Does not modify the scene.</returns>
        public static int ValidateScene()
        {
            var scene = SceneManager.GetActiveScene();
            int errors = 0;
            int missingReferences = 0;
            int auditedReferences = 0;
            var scripts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            foreach (var script in scripts)
            {
                if (script == null)
                {
                    Fail(ref errors, "Missing script in scene hierarchy.");
                    continue;
                }
                if (script.GetType().Namespace != "TIEC.Runner") continue;
                var serialized = new SerializedObject(script);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                    auditedReferences++;
                    if (property.objectReferenceValue != null) continue;
                    missingReferences++;
                    Fail(ref errors, $"{PathOf(script.transform)}: missing {property.propertyPath}.", script);
                }
            }

            var sessions = Find<RunnerSession>(scene);
            var inputs = Find<RunnerInput>(scene);
            var motors = Find<BicycleMotor>(scene);
            var follows = Find<RunnerCamera>(scene);
            var huds = Find<RunnerHud>(scene);
            Count(ref errors, sessions.Length, 1, "RunnerSession");
            Count(ref errors, inputs.Length, 1, "RunnerInput");
            Count(ref errors, motors.Length, 1, "BicycleMotor");
            Count(ref errors, follows.Length, 1, "RunnerCamera");
            Count(ref errors, huds.Length, 1, "RunnerHud");

            var eventSystems = Find<EventSystem>(scene).Where(item => item.isActiveAndEnabled).ToArray();
            Count(ref errors, eventSystems.Length, 1, "active EventSystem");
            if (eventSystems.Length == 1)
            {
                var modules = eventSystems[0].GetComponents<BaseInputModule>().Where(module => module.isActiveAndEnabled).ToArray();
                Count(ref errors, modules.Length, 1, "active UI input module");
                if (modules.Length == 1 && !(modules[0] is InputSystemUIInputModule))
                    Fail(ref errors, "EventSystem requires InputSystemUIInputModule.", eventSystems[0]);
            }
            var gameCameras = Find<Camera>(scene).Where(camera => camera.isActiveAndEnabled && camera.cameraType == CameraType.Game).ToArray();
            Count(ref errors, gameCameras.Length, 1, "active game Camera");
            Count(ref errors, Find<AudioListener>(scene).Count(listener => listener.isActiveAndEnabled), 1, "active AudioListener");

            if (sessions.Length == 1 && inputs.Length == 1 && motors.Length == 1 && follows.Length == 1 && huds.Length == 1)
            {
                var session = sessions[0];
                var input = inputs[0];
                var motor = motors[0];
                var follow = follows[0];
                var hud = huds[0];
                Match(ref errors, session, "input", input);
                Match(ref errors, session, "motor", motor);
                Match(ref errors, session, "followCamera", follow);
                Match(ref errors, input, "session", session);
                Match(ref errors, input, "hud", hud);
                Match(ref errors, motor, "input", input);
                Match(ref errors, follow, "target", motor.transform);
                Match(ref errors, follow, "session", session);
                Match(ref errors, hud, "session", session);
                Match(ref errors, hud, "input", input);
                var config = Reference<RunnerConfig>(session, "settings");
                if (config != null)
                {
                    Match(ref errors, motor, "settings", config);
                    var motorConfig = Reference<RunnerConfig>(motor, "settings");
                    var box = Reference<BoxCollider>(motor, "hitbox");
                    var body = Reference<Rigidbody>(motor, "body");
                    if (box != null)
                    {
                        if (box.gameObject != motor.gameObject || !box.enabled || box.isTrigger)
                            Fail(ref errors, "Motor hitbox must be an enabled, non-trigger BoxCollider on the motor object.", box);
                        if (box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f)
                            Fail(ref errors, "Motor hitbox must have positive dimensions.", box);
                        if (motorConfig != null)
                        {
                            if (motor.MinX >= motor.MaxX || motor.MinX >= 0f || motor.MaxX <= 0f)
                                Fail(ref errors, "Road limits must provide a non-empty steering interval around X=0.", motor);
                            if (Mathf.Abs(motor.MinX) > config.roadHalfWidth || Mathf.Abs(motor.MaxX) > config.roadHalfWidth)
                                Fail(ref errors, "Motor clamp limits extend beyond the configured road.", motor);
                            var start = new SerializedObject(motor).FindProperty("startPosition").vector3Value;
                            if (start.x < motor.MinX || start.x > motor.MaxX)
                                Fail(ref errors, "Motor starts outside its permitted road interval.", motor);
                        }
                        if ((motor.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                            Fail(ref errors, "Motor object must have unit world scale because collision sweeps use hitbox dimensions.", motor);
                    }
                    if (body != null && body.gameObject != motor.gameObject)
                        Fail(ref errors, "Motor Rigidbody reference must point to the same object.", body);
                    if (config.obstacleMask.value == 0) Fail(ref errors, "Obstacle layer mask is empty.", config);
                    if (config.groundMask.value == 0) Fail(ref errors, "Ground layer mask is empty.", config);
                    if ((config.obstacleMask.value & config.groundMask.value) != 0)
                        Fail(ref errors, "Obstacle and ground layer masks overlap; riding on a ramp could end a run.", config);
                    var hazards = Find<RunnerHazard>(scene).Where(item => item.gameObject.activeInHierarchy).ToArray();
                    if (hazards.Length == 0) Fail(ref errors, "No active RunnerHazard objects exist in the course.");
                    foreach (var hazard in hazards)
                    {
                        var colliders = hazard.GetComponentsInChildren<Collider>(true).Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
                        if (colliders.Length == 0) Fail(ref errors, $"Hazard {PathOf(hazard.transform)} has no enabled collider.", hazard);
                        foreach (var collider in colliders)
                            if ((config.obstacleMask.value & (1 << collider.gameObject.layer)) == 0)
                                Fail(ref errors, $"Hazard collider {PathOf(collider.transform)} is excluded from obstacleMask.", collider);
                    }
                }
                var view = Reference<Camera>(follow, "view");
                if (view != null && (!view.isActiveAndEnabled || !view.CompareTag("MainCamera")))
                    Fail(ref errors, "Runner camera must be enabled and tagged MainCamera.", view);
                var canvas = hud.GetComponent<Canvas>();
                if (canvas == null || canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    Fail(ref errors, "Runner HUD requires a Canvas and GraphicRaycaster.", hud);
                var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler == null || scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    Fail(ref errors, "Runner HUD requires CanvasScaler ScaleWithScreenSize for computer and tablet.", hud);
                if (hud.GetComponentInChildren<RunnerSafeArea>(true) == null)
                    Fail(ref errors, "Runner HUD has no safe-area container.", hud);
                var touchButtons = hud.GetComponentsInChildren<RunnerTouchSteer>(true);
                Count(ref errors, touchButtons.Length, 2, "touch steering controls");
                foreach (var button in touchButtons) Match(ref errors, button, "input", input);
                foreach (var text in hud.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font == null) Fail(ref errors, $"{PathOf(text.transform)} has no TMP font.", text);
                    if (text.fontSharedMaterial == null) Fail(ref errors, $"{PathOf(text.transform)} has no TMP font material.", text);
                }
            }
            Debug.Log($"Runner scene audit: {auditedReferences} reference(s), {missingReferences} missing reference(s), {errors} total error(s).");
            return errors;
        }

        /// <summary>Tests the authored profile plus 30s baseline and 1.2x speed on temporary clones.</summary>
        public static int ValidateProfiles(RunnerConfig source)
        {
            int errors = 0;
            if (source == null) { Fail(ref errors, "Profile validation requires a settings asset."); return errors; }
            var clone = UnityEngine.Object.Instantiate(source);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                errors += CheckProfile(clone, "authored");
                clone.targetSeconds = 30f;
                clone.speedMultiplier = 1f;
                if (Mathf.Abs(clone.Duration - 30f) > 0.0001f) Fail(ref errors, "Baseline duration should be exactly 30 seconds.");
                errors += CheckProfile(clone, "30-second baseline");
                float baselineHalfSpeed = clone.SpeedAt(15f);
                clone.speedMultiplier = 1.2f;
                if (Mathf.Abs(clone.Duration - 25f) > 0.0001f) Fail(ref errors, "1.2x multiplier should finish the same map in 25 seconds.");
                if (Mathf.Abs(clone.SpeedAt(12.5f) - baselineHalfSpeed * 1.2f) > 0.001f)
                    Fail(ref errors, "Speed multiplier must preserve the normalized acceleration profile.");
                errors += CheckProfile(clone, "1.2x multiplier");
                clone.speedMultiplier = 0.75f;
                if (Mathf.Abs(clone.Duration - 40f) > 0.0001f) Fail(ref errors, "0.75x multiplier should finish the same map in 40 seconds.");
                errors += CheckProfile(clone, "0.75x multiplier");
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            Debug.Log($"Runner profile checks: {errors} error(s). Original settings were not modified.");
            return errors;
        }

        private static int CheckProfile(RunnerConfig profile, string label)
        {
            int errors = 0;
            if (!Finite(profile.Duration) || profile.Duration <= 0f) Fail(ref errors, $"{label}: duration is not positive and finite.");
            if (Mathf.Abs(profile.DistanceAt(0f)) > 0.0001f) Fail(ref errors, $"{label}: distance does not start at zero.");
            if (Mathf.Abs(profile.DistanceAt(profile.Duration) - profile.courseLength) > 0.001f)
                Fail(ref errors, $"{label}: final distance does not equal courseLength.");
            float previousDistance = -1f, previousSpeed = -1f;
            const int samples = 512;
            float integral = 0f;
            for (int i = 0; i <= samples; i++)
            {
                float seconds = profile.Duration * i / samples;
                float distance = profile.DistanceAt(seconds);
                float speed = profile.SpeedAt(seconds);
                if (!Finite(distance) || !Finite(speed) || speed <= 0f)
                { Fail(ref errors, $"{label}: non-finite or non-positive sample at {seconds:0.000}s."); break; }
                if (distance + 0.0001f < previousDistance || speed + 0.0001f < previousSpeed)
                { Fail(ref errors, $"{label}: speed or distance decreases at {seconds:0.000}s."); break; }
                if (i > 0) integral += (previousSpeed + speed) * 0.5f * profile.Duration / samples;
                if (Mathf.Abs(profile.TimeAtDistance(distance) - seconds) > 0.0001f)
                { Fail(ref errors, $"{label}: TimeAtDistance does not invert DistanceAt."); break; }
                previousDistance = distance;
                previousSpeed = speed;
            }
            if (Mathf.Abs(integral - profile.courseLength) > Mathf.Max(0.02f, profile.courseLength * 0.0002f))
                Fail(ref errors, $"{label}: integrated sampled speed does not reach courseLength.");
            if (profile.SpeedAt(profile.Duration) <= profile.SpeedAt(0f))
                Fail(ref errors, $"{label}: the challenge does not accelerate.");
            return errors;
        }

        /// <summary>Uses a unique test storage key; requires ScoreRepository(string storageKey).</summary>
        public static int ValidateScores()
        {
            int errors = 0;
            var constructor = typeof(ScoreRepository).GetConstructor(new[] { typeof(string) });
            if (constructor == null)
            { Fail(ref errors, "Isolated score validation requires a ScoreRepository(string storageKey) constructor."); return errors; }
            string key = "TIEC.GroveRunner.Validation." + Guid.NewGuid().ToString("N");
            try
            {
                var repository = (ScoreRepository)constructor.Invoke(new object[] { key });
                repository.Save(new RunResult { Id = "attempt-a", Seconds = 14f, Progress = 0.4f }, " <Ada>\n ");
                repository.Save(new RunResult { Id = "attempt-a", Seconds = 14f, Progress = 0.4f }, "Ece");
                repository.Save(new RunResult { Id = "attempt-b", Seconds = 30f, Progress = 1f, Completed = true }, "Bitiren");
                repository.Save(new RunResult { Id = "attempt-c", Seconds = 13f, Progress = 0.8f }, "Hızlı");
                var reloaded = (ScoreRepository)constructor.Invoke(new object[] { key });
                if (reloaded.Scores.Count != 3 || reloaded.Scores.Count(item => item.Id == "attempt-a") != 1)
                    Fail(ref errors, "Renaming a score creates duplicate attempts or persistence loses a score.");
                if (reloaded.Scores.FirstOrDefault(item => item.Id == "attempt-a")?.PlayerName != "Ece")
                    Fail(ref errors, "Renamed player name does not survive repository reload.");
                if (reloaded.Scores.Count >= 3 && (reloaded.Scores[0].Id != "attempt-b" || reloaded.Scores[1].Id != "attempt-c"))
                    Fail(ref errors, "Scores must prioritize successful runs, then farther failed runs across different speeds.");
                for (int i = 0; i < 32; i++)
                    repository.Save(new RunResult { Id = "retention-" + i, Seconds = i, Progress = i / 100f }, "Test");
                reloaded = (ScoreRepository)constructor.Invoke(new object[] { key });
                if (reloaded.Scores.Count != 30) Fail(ref errors, "Score retention does not preserve exactly the best 30 runs.");
                if (!reloaded.Scores.Any(item => item.Id == "attempt-b") || !reloaded.Scores.Any(item => item.Id == "attempt-c"))
                    Fail(ref errors, "Score retention discarded a successful or higher-progress run.");
                if (ScoreRepository.CleanName(" \n<> ") != "OYUNCU" || ScoreRepository.CleanName(new string('A', 40)).Length > 18)
                    Fail(ref errors, "Name normalization does not provide a safe fallback and length limit.");
            }
            catch (Exception exception) { Fail(ref errors, "Score validation threw: " + exception.GetBaseException().Message); }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            Debug.Log($"Runner isolated score checks: {errors} error(s). Player score keys were not modified.");
            return errors;
        }

        private static T[] Find<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T Reference<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object
            => new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T;

        private static void Match(ref int errors, UnityEngine.Object owner, string field, UnityEngine.Object expected)
        {
            if (Reference<UnityEngine.Object>(owner, field) != expected)
                Fail(ref errors, $"{owner.name}.{field} does not reference the expected {expected.name}.", owner);
        }

        private static void Count(ref int errors, int actual, int expected, string label)
        { if (actual != expected) Fail(ref errors, $"Expected {expected} {label}, found {actual}."); }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string PathOf(Transform item)
        {
            string path = item.name;
            while (item.parent != null) { item = item.parent; path = item.name + "/" + path; }
            return path;
        }

        private static void Fail(ref int errors, string message, UnityEngine.Object context = null)
        { errors++; Debug.LogError("Runner validation: " + message, context); }
    }
}
#endif
