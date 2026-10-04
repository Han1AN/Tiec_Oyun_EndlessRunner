using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace TIEC.Runner
{
    /// <summary>Presentation only. The session owns run state, timing and persisted scores.</summary>
    [DisallowMultipleComponent]
    public sealed class RunnerHud : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private RunnerSession session;
        [SerializeField] private RunnerInput input;
        [Header("Screens")]
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private GameObject runningPanel;
        [SerializeField] private GameObject resultPanel;
        [Header("Run HUD")]
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text speedLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private TMP_Text readyTargetLabel;
        [Header("Result")]
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultSummary;
        [SerializeField] private TMP_Text scoreTable;
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_Text saveStatus;
        [SerializeField] private UnityEngine.UI.Button readyStartButton;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private UnityEngine.UI.Button saveButton;

        private bool subscribed;
        private float nextHudRefresh;
        private readonly List<RunResult> rankedScores = new List<RunResult>();
        private readonly StringBuilder scoreText = new StringBuilder(256);

        public bool IsEditingName => nameField != null && nameField.isFocused;

        public void Configure(RunnerSession owner, RunnerInput controls,
            GameObject ready, GameObject running, GameObject result,
            TMP_Text timer, TMP_Text speed, TMP_Text progress, UnityEngine.UI.Image fill,
            TMP_Text target, TMP_Text headline, TMP_Text summary, TMP_Text leaderboard,
            TMP_InputField playerName, TMP_Text saved,
            UnityEngine.UI.Button start, UnityEngine.UI.Button retry, UnityEngine.UI.Button save)
        {
            Unsubscribe();
            session = owner;
            input = controls;
            readyPanel = ready;
            runningPanel = running;
            resultPanel = result;
            timeLabel = timer;
            speedLabel = speed;
            progressLabel = progress;
            progressFill = fill;
            readyTargetLabel = target;
            resultTitle = headline;
            resultSummary = summary;
            scoreTable = leaderboard;
            nameField = playerName;
            saveStatus = saved;
            readyStartButton = start;
            retryButton = retry;
            saveButton = save;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable()
        {
            Unsubscribe();
            if (input != null) input.ClearSteering();
        }

        private void Subscribe()
        {
            if (subscribed || session == null) return;
            session.StateChanged += ShowState;
            if (readyStartButton != null) readyStartButton.onClick.AddListener(StartRun);
            if (retryButton != null) retryButton.onClick.AddListener(StartRun);
            if (saveButton != null) saveButton.onClick.AddListener(SaveName);
            if (nameField != null)
            {
                nameField.onValueChanged.AddListener(NameChanged);
                nameField.onEndEdit.AddListener(NameEditingFinished);
            }
            subscribed = true;
            ShowState(session.State);
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (session != null) session.StateChanged -= ShowState;
            if (readyStartButton != null) readyStartButton.onClick.RemoveListener(StartRun);
            if (retryButton != null) retryButton.onClick.RemoveListener(StartRun);
            if (saveButton != null) saveButton.onClick.RemoveListener(SaveName);
            if (nameField != null)
            {
                nameField.onValueChanged.RemoveListener(NameChanged);
                nameField.onEndEdit.RemoveListener(NameEditingFinished);
            }
            subscribed = false;
        }

        private void Update()
        {
            if (session == null || session.State != RunState.Running || Time.unscaledTime < nextHudRefresh) return;
            nextHudRefresh = Time.unscaledTime + 0.05f;
            RefreshRun();
        }

        private void ShowState(RunState state)
        {
            if (input != null) input.ClearSteering();
            if (readyPanel != null) readyPanel.SetActive(state == RunState.Ready);
            if (runningPanel != null) runningPanel.SetActive(state == RunState.Running);
            if (resultPanel != null) resultPanel.SetActive(state == RunState.Result);
            if (readyTargetLabel != null) readyTargetLabel.text = $"{session.FinishDuration:0} SANİYE / TEK HAK";
            nextHudRefresh = 0f;
            if (state == RunState.Running) RefreshRun();
            if (state == RunState.Result) RefreshResult();
        }

        private void RefreshRun()
        {
            if (timeLabel != null) timeLabel.text = $"{session.Elapsed:00.00} / {session.FinishDuration:0.00} s";
            if (speedLabel != null) speedLabel.text = $"{session.Speed * 3.6f:0} KM/H";
            if (progressLabel != null) progressLabel.text = $"PARKUR  %{Mathf.Clamp01(session.Progress) * 100f:0}";
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(session.Progress);
        }

        private void RefreshResult()
        {
            var result = session.LastResult;
            if (resultTitle != null)
            {
                resultTitle.text = result.Completed ? "MISSION PASSED" : "WASTED";
                resultTitle.color = result.Completed ? new Color32(233, 173, 70, 255) : new Color32(216, 66, 55, 255);
            }
            if (resultSummary != null)
                resultSummary.text = result.Completed
                    ? $"PARKUR TAMAMLANDI\n{result.Seconds:0.00} SANİYE"
                    : $"ENGELE ÇARPTIN\n{result.Seconds:0.00} SANİYE  /  %{result.Progress * 100f:0} PARKUR";
            if (nameField != null) nameField.SetTextWithoutNotify(result.PlayerName);
            if (saveStatus != null) saveStatus.text = "Skor kaydedildi.";
            RefreshScores();
        }

        private void RefreshScores()
        {
            if (scoreTable == null) return;
            rankedScores.Clear();
            var scores = session.Scores;
            if (scores != null)
                for (int i = 0; i < scores.Count; i++) rankedScores.Add(scores[i]);
            rankedScores.Sort((a, b) =>
            {
                int completed = b.Completed.CompareTo(a.Completed);
                if (completed != 0) return completed;
                return a.Completed ? a.Seconds.CompareTo(b.Seconds) : b.Progress.CompareTo(a.Progress);
            });
            scoreText.Clear();
            for (int i = 0; i < Mathf.Min(5, rankedScores.Count); i++)
            {
                var score = rankedScores[i];
                string name = string.IsNullOrWhiteSpace(score.PlayerName) ? "OYUNCU" : score.PlayerName;
                if (name.Length > 16) name = name.Substring(0, 16);
                scoreText.Append(i + 1).Append(". ").Append(name).Append("   ");
                scoreText.Append(score.Seconds.ToString("0.00")).Append(" s   ");
                scoreText.Append(score.Completed ? "BİTTİ" : $"%{score.Progress * 100f:0}");
                if (i < Mathf.Min(5, rankedScores.Count) - 1) scoreText.Append('\n');
            }
            if (rankedScores.Count == 0) scoreText.Append("İlk skoru sen yaz.");
            scoreTable.text = scoreText.ToString();
        }

        private void NameChanged(string value)
        {
            if (saveStatus != null) saveStatus.text = "İsmini kaydetmek için KAYDET'e bas.";
        }

        private void NameEditingFinished(string value) => SaveName();

        private void SaveName()
        {
            if (session == null || session.State != RunState.Result) return;
            session.SaveResult(nameField != null ? nameField.text : "OYUNCU");
            if (nameField != null) nameField.SetTextWithoutNotify(session.LastResult.PlayerName);
            if (saveStatus != null) saveStatus.text = "Skor kaydedildi.";
            RefreshScores();
        }

        private void StartRun()
        {
            if (session == null) return;
            if (session.State == RunState.Result) SaveName();
            if (nameField != null) nameField.DeactivateInputField();
            session.StartRun();
        }
        public void SaveAndRestart() => StartRun();
    }
}
