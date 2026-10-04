using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TIEC.Runner
{
    [Serializable]
    public sealed class RunResult
    {
        public string Id;
        public string PlayerName;
        public float Seconds;
        public float Progress;
        public bool Completed;
    }

    // Local device records. Updating a name replaces the same attempt, never duplicates it.
    public sealed class ScoreRepository
    {
        [Serializable] sealed class SaveData { public List<RunResult> entries = new List<RunResult>(); }
        readonly string storageKey;
        SaveData data;
        public IReadOnlyList<RunResult> Scores => data.entries;
        public ScoreRepository(string key = "TIEC.GroveRunner.Scores.v1")
        {
            storageKey = key;
            try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(storageKey, "")); }
            catch (Exception) { data = null; }
            if (data == null || data.entries == null) data = new SaveData();
            data.entries.RemoveAll(x => x == null || string.IsNullOrEmpty(x.Id));
            Sort();
        }
        public void Save(RunResult entry, string name)
        {
            entry.PlayerName = CleanName(name);
            int existing = data.entries.FindIndex(x => x.Id == entry.Id);
            if (existing >= 0) data.entries[existing] = entry; else data.entries.Add(entry);
            Sort();
            if (data.entries.Count > 30) data.entries.RemoveRange(30, data.entries.Count - 30);
            PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        void Sort()
        {
            data.entries = data.entries.OrderByDescending(x => x.Completed)
                .ThenBy(x => x.Completed ? x.Seconds : -x.Progress).ThenByDescending(x => x.Seconds).ToList();
        }
        public static string CleanName(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "OYUNCU" : name.Trim();
            name = new string(name.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(18).ToArray());
            return string.IsNullOrWhiteSpace(name) ? "OYUNCU" : name;
        }
    }
}
