using System;
using System.Collections.Generic;
using UnityEngine;

namespace TIEC.Runner
{
    public enum RunState { Ready, Running, Result }
    [DefaultExecutionOrder(-50)]
    public sealed class RunnerSession : MonoBehaviour
    {
        [SerializeField] RunnerConfig settings;
        [SerializeField] RunnerInput input;
        [SerializeField] BicycleMotor motor;
        [SerializeField] RunnerCamera followCamera;
        ScoreRepository repository;
        string playerName = "OYUNCU";
        float resultTime;
        public RunState State { get; private set; } = RunState.Ready;
        public float Elapsed { get; private set; }
        public float Progress => settings == null ? 0 : Mathf.Clamp01(settings.DistanceAt(Elapsed) / settings.courseLength);
        public float Speed => State == RunState.Running ? settings.SpeedAt(Elapsed) : 0;
        public float FinishDuration => settings.Duration;
        public float ResultAge => Time.unscaledTime - resultTime;
        public RunnerInput Input => input;
        public RunResult LastResult { get; private set; }
        public IReadOnlyList<RunResult> Scores => repository.Scores;
        public event Action<RunState> StateChanged;
        public void Configure(RunnerConfig config, RunnerInput controls, BicycleMotor bicycle, RunnerCamera camera)
        { settings = config; input = controls; motor = bicycle; followCamera = camera; }
        void Awake()
        {
            repository = new ScoreRepository();
            playerName = ScoreRepository.CleanName(PlayerPrefs.GetString("TIEC.GroveRunner.Name", "OYUNCU"));
            if (settings == null || input == null || motor == null || followCamera == null)
            { Debug.LogError("RunnerSession: Settings, Input, Motor ve Camera referanslarını atayın.", this); enabled = false; return; }
            motor.ResetToStart(); followCamera.SnapToTarget();
        }
        public void StartRun()
        {
            if (!enabled || State == RunState.Running) return;
            Elapsed = 0; input.ClearSteering(); motor.ResetToStart(); followCamera.SnapToTarget();
            State = RunState.Running; StateChanged?.Invoke(State);
        }
        void FixedUpdate()
        {
            if (State != RunState.Running) return;
            float nextTime = Mathf.Min(Elapsed + Time.fixedDeltaTime, settings.Duration);
            float nextDistance = settings.DistanceAt(nextTime);
            if (!motor.Advance(nextDistance, input.Steer, nextTime - Elapsed, settings.SpeedAt(nextTime), out float fraction))
            {
                Elapsed = Mathf.Lerp(Elapsed, nextTime, fraction); Finish(false); return;
            }
            Elapsed = nextTime;
            if (Elapsed >= settings.Duration) Finish(true);
        }
        void Finish(bool completed)
        {
            if (State != RunState.Running) return;
            State = RunState.Result; resultTime = Time.unscaledTime;
            LastResult = new RunResult { Id = Guid.NewGuid().ToString("N"), PlayerName = playerName,
                Seconds = Elapsed, Progress = Progress, Completed = completed };
            repository.Save(LastResult, playerName); input.ClearSteering(); motor.StopVisuals();
            StateChanged?.Invoke(State);
        }
        public void SaveResult(string name)
        {
            if (State != RunState.Result || LastResult == null) return;
            playerName = ScoreRepository.CleanName(name); repository.Save(LastResult, playerName);
            PlayerPrefs.SetString("TIEC.GroveRunner.Name", playerName); PlayerPrefs.Save();
        }
        void OnApplicationPause(bool paused) { if (paused) input.ClearSteering(); }
    }
}
