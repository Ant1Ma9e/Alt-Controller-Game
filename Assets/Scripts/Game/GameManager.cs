using System;
using System.Collections.Generic;
using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 单局 60 秒、命中 +100 分,游戏结束写入本地排行榜。
    /// 用 OnGUI 画 Menu / In-Game HUD / Game Over 三个状态的简易 UI。
    /// </summary>
    [DefaultExecutionOrder(-50)] // Awake 比 EnemySpawner.Start 早,关闭 autoStart
    public class GameManager : MonoBehaviour
    {
        public enum State { Menu, Playing, GameOver }

        [Header("引用(留空自动 Find)")]
        [SerializeField] private PlayerController player;
        [SerializeField] private EnemySpawner spawner;

        [Header("游戏参数")]
        [Tooltip("一局游戏时长(秒)。")]
        [SerializeField] private float gameDuration = 60f;

        [Header("排行榜")]
        [Tooltip("保存的最高分条数。")]
        [Range(3, 50)]
        [SerializeField] private int leaderboardSize = 10;

        [Header("UI 样式")]
        [SerializeField] private int titleFontSize = 48;
        [SerializeField] private int bodyFontSize = 24;
        [SerializeField] private int hudFontSize = 32;

        private State currentState = State.Menu;
        private float remainingTime;
        private int currentScore;
        private List<LeaderboardStorage.Entry> leaderboardCache;
        private int lastRunRank = -1;
        private int lastRunScore = 0;

        // UI 样式缓存
        private bool stylesReady;
        private GUIStyle titleStyle, bodyStyle, hudStyle, buttonStyle, rowStyle;

        public State CurrentState => currentState;
        public float RemainingTime => remainingTime;
        public int CurrentScore => currentScore;

        public event Action OnGameStarted;
        public event Action<int> OnGameEnded;     // 最终得分
        public event Action<int> OnScoreChanged;  // 当前分数
        public event Action<float> OnTimeChanged; // 剩余秒数

        private void Awake()
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (spawner == null) spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null) spawner.SetAutoStart(false);
        }

        private void Start()
        {
            if (spawner != null) spawner.OnEnemyKilled += HandleEnemyKilled;
            leaderboardCache = LeaderboardStorage.Load(leaderboardSize);
            EnterMenu();
        }

        private void OnDestroy()
        {
            if (spawner != null) spawner.OnEnemyKilled -= HandleEnemyKilled;
        }

        private void Update()
        {
            if (currentState != State.Playing) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                OnTimeChanged?.Invoke(remainingTime);
                EndGame();
                return;
            }
            OnTimeChanged?.Invoke(remainingTime);
        }

        public void StartGame()
        {
            currentState = State.Playing;
            currentScore = 0;
            remainingTime = gameDuration;

            if (spawner != null) spawner.StartRound();

            OnScoreChanged?.Invoke(currentScore);
            OnTimeChanged?.Invoke(remainingTime);
            OnGameStarted?.Invoke();
        }

        public void EndGame()
        {
            if (currentState == State.GameOver) return;
            currentState = State.GameOver;
            lastRunScore = currentScore;

            if (spawner != null) spawner.StopRound();

            lastRunRank = LeaderboardStorage.Submit(lastRunScore, leaderboardSize, out leaderboardCache);

            OnGameEnded?.Invoke(lastRunScore);
        }

        public void EnterMenu()
        {
            currentState = State.Menu;
            currentScore = 0;
            remainingTime = gameDuration;
            if (spawner != null) spawner.StopRound();
            leaderboardCache = LeaderboardStorage.Load(leaderboardSize);
        }

        private void HandleEnemyKilled(Enemy enemy)
        {
            if (currentState != State.Playing || enemy == null) return;
            // 普通敌人 +100,小猫咪 -50,具体数值取自各 Enemy 的 ScoreValue。
            currentScore += enemy.ScoreValue;
            if (currentScore < 0) currentScore = 0;
            OnScoreChanged?.Invoke(currentScore);
        }

        // ---------------- OnGUI ----------------

        private void InitStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            titleStyle.normal.textColor = Color.white;

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = bodyFontSize,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
            };
            bodyStyle.normal.textColor = Color.white;

            hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = hudFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
            };
            hudStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = bodyFontSize + 4,
                fontStyle = FontStyle.Bold,
            };

            rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = bodyFontSize,
                alignment = TextAnchor.MiddleLeft,
            };
            rowStyle.normal.textColor = Color.white;

            stylesReady = true;
        }

        private void OnGUI()
        {
            if (!stylesReady) InitStyles();

            switch (currentState)
            {
                case State.Menu: DrawMenu(); break;
                case State.Playing: DrawHud(); break;
                case State.GameOver: DrawGameOver(); break;
            }
        }

        private void DrawHud()
        {
            // 顶部居中:倒计时 + 当前分数
            float w = 720f, h = 80f;
            float x = (Screen.width - w) * 0.5f;
            float y = 20f;
            string time = Mathf.CeilToInt(remainingTime).ToString();
            string text = $"<color=#FFD24D>{time}s</color>   |   Score: <color=#7CFFB0>{currentScore}</color>";
            GUI.Label(new Rect(x, y, w, h), text, hudStyle);
        }

        private void DrawMenu()
        {
            DrawModal("ALT CONTROLLER GAME",
                "戴上眼罩,戴上耳机。\n按 START GAME 开始 60 秒挑战。\nQ/E 转身,K 挥砍(将由 Joy-Con 替换)。",
                primaryLabel: "START GAME",
                primaryAction: StartGame,
                showLeaderboard: true,
                showLastRun: false);
        }

        private void DrawGameOver()
        {
            string rankLine = lastRunRank > 0
                ? $"Rank: <color=#FFD24D>#{lastRunRank}</color> / {leaderboardSize}"
                : $"Not in Top {leaderboardSize}";

            string body =
                $"Time's up!\n\nYour Score: <color=#7CFFB0>{lastRunScore}</color>\n{rankLine}";

            DrawModal("GAME OVER",
                body,
                primaryLabel: "PLAY AGAIN",
                primaryAction: StartGame,
                showLeaderboard: true,
                showLastRun: true,
                secondaryLabel: "MENU",
                secondaryAction: EnterMenu);
        }

        private void DrawModal(
            string title,
            string body,
            string primaryLabel,
            Action primaryAction,
            bool showLeaderboard,
            bool showLastRun,
            string secondaryLabel = null,
            Action secondaryAction = null)
        {
            float panelW = Mathf.Min(600f, Screen.width - 40f);
            float panelH = Mathf.Min(700f, Screen.height - 40f);
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // 背景遮罩
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none);

            GUILayout.BeginArea(new Rect(panelX + 24f, panelY + 24f, panelW - 48f, panelH - 48f));

            GUILayout.Label(title, titleStyle);
            GUILayout.Space(12f);
            GUILayout.Label(body, bodyStyle);
            GUILayout.Space(20f);

            if (GUILayout.Button(primaryLabel, buttonStyle, GUILayout.Height(56f)))
            {
                primaryAction?.Invoke();
            }

            if (!string.IsNullOrEmpty(secondaryLabel))
            {
                GUILayout.Space(6f);
                if (GUILayout.Button(secondaryLabel, buttonStyle, GUILayout.Height(40f)))
                {
                    secondaryAction?.Invoke();
                }
            }

            GUILayout.Space(18f);

            if (showLeaderboard) DrawLeaderboard(showLastRun);

            GUILayout.EndArea();
        }

        private void DrawLeaderboard(bool highlightLastRun)
        {
            GUILayout.Label("LEADERBOARD", bodyStyle);
            GUILayout.Space(6f);

            if (leaderboardCache == null || leaderboardCache.Count == 0)
            {
                GUILayout.Label("(empty)", bodyStyle);
                return;
            }

            for (int i = 0; i < leaderboardCache.Count; i++)
            {
                var e = leaderboardCache[i];
                int rank = i + 1;
                bool isMine = highlightLastRun && rank == lastRunRank && e.score == lastRunScore;
                string date = DateTimeOffset.FromUnixTimeSeconds(e.timestamp).ToLocalTime().ToString("MM-dd HH:mm");
                string line = isMine
                    ? $"<color=#FFD24D>#{rank,-3} {e.score,5}   {date}   ← YOU</color>"
                    : $"#{rank,-3} {e.score,5}   {date}";
                GUILayout.Label(line, rowStyle);
            }
        }
    }
}
