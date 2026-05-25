using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 排行榜本地持久化(JSON 文件存到 Application.persistentDataPath)。
    /// 这个目录在每个平台都属于用户数据区,游戏卸载/重装、版本升级都不会被清除。
    /// 文件路径示例(Windows):
    ///   %userprofile%\AppData\LocalLow\DefaultCompany\Alt Controller Game\leaderboard.json
    /// </summary>
    public static class LeaderboardStorage
    {
        private const string FileName = "leaderboard.json";

        [Serializable]
        public class Entry
        {
            public int score;
            public long timestamp; // UTC Unix 秒
        }

        [Serializable]
        private class SaveData
        {
            public List<Entry> entries = new List<Entry>();
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>读取并按分数降序返回(同分新的在前)。最多返回 maxEntries 条。</summary>
        public static List<Entry> Load(int maxEntries)
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<Entry>();
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.entries == null) return new List<Entry>();
                SortDesc(data.entries);
                if (data.entries.Count > maxEntries)
                    data.entries = data.entries.GetRange(0, maxEntries);
                return data.entries;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] Load 失败: {e.Message}");
                return new List<Entry>();
            }
        }

        /// <summary>提交新分数,返回它在 Top maxEntries 里的排名(1 起步);未上榜返回 -1。</summary>
        public static int Submit(int score, int maxEntries, out List<Entry> updatedTop)
        {
            updatedTop = Load(maxEntries * 4); // 多读一些再合并,容错
            var entry = new Entry
            {
                score = score,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            updatedTop.Add(entry);
            SortDesc(updatedTop);

            int rank = updatedTop.IndexOf(entry) + 1; // 0-based -> 1-based;-1+1=0 表示未找到

            if (updatedTop.Count > maxEntries)
                updatedTop = updatedTop.GetRange(0, maxEntries);

            Save(updatedTop);

            if (rank <= 0 || rank > maxEntries) return -1;
            return rank;
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] Clear 失败: {e.Message}");
            }
        }

        private static void Save(List<Entry> entries)
        {
            try
            {
                var data = new SaveData { entries = entries };
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] Save 失败: {e.Message}");
            }
        }

        private static void SortDesc(List<Entry> entries)
        {
            entries.Sort((a, b) =>
            {
                int cmp = b.score.CompareTo(a.score);
                if (cmp != 0) return cmp;
                return b.timestamp.CompareTo(a.timestamp); // 同分新的在前
            });
        }
    }
}
