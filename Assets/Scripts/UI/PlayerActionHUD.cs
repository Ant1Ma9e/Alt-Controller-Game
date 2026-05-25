using System.Collections.Generic;
using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 用 OnGUI 在屏幕左上角显示玩家操作:当前面向方位 + 最近若干条操作日志(转身/挥砍命中/挥砍空挥)。
    /// 优点:零 Canvas 设置,丢到任意 GameObject 上即可使用。
    /// 后续要做正式 UI 时,把 PlayerController.OnDirectionChanged / OnSlashPerformed 接到自己的 UI 上即可。
    /// </summary>
    public class PlayerActionHUD : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("留空时自动 FindObjectOfType。")]
        [SerializeField] private PlayerController player;

        [Header("显示")]
        [SerializeField] private Vector2 hudPosition = new Vector2(20f, 20f);
        [SerializeField] private int fontSize = 22;
        [Range(1, 20)]
        [SerializeField] private int maxLogLines = 6;
        [Tooltip("日志逐行淡出时长(秒)。0 = 不淡出。")]
        [Min(0f)]
        [SerializeField] private float lineFadeDuration = 4f;
        [SerializeField] private Color textColor = Color.white;

        private static readonly string[] DirectionNames =
            { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private struct LogEntry
        {
            public string text;
            public float spawnTime;
        }

        private readonly List<LogEntry> log = new List<LogEntry>();
        private GUIStyle facingStyle;
        private GUIStyle logStyle;
        private bool stylesInitialized;

        private void Start()
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                player.OnDirectionChanged += HandleDirectionChanged;
                player.OnSlashPerformed += HandleSlash;
                AddLog($"Facing {DirectionNames[player.CurrentDirectionIndex]}");
            }
            else
            {
                Debug.LogWarning("[PlayerActionHUD] 未找到 PlayerController。");
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.OnDirectionChanged -= HandleDirectionChanged;
                player.OnSlashPerformed -= HandleSlash;
            }
        }

        private void HandleDirectionChanged(int newDirection)
        {
            AddLog($"Turn → {DirectionNames[newDirection]}");
        }

        private void HandleSlash(bool hit)
        {
            AddLog(hit ? "Slash · <b>HIT</b>" : "Slash · miss");
        }

        private void AddLog(string text)
        {
            log.Add(new LogEntry { text = text, spawnTime = Time.time });
            while (log.Count > maxLogLines) log.RemoveAt(0);
        }

        private void InitStyles()
        {
            facingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 4,
                fontStyle = FontStyle.Bold,
                richText = true,
            };
            facingStyle.normal.textColor = textColor;

            logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true,
            };
            logStyle.normal.textColor = textColor;
            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!stylesInitialized) InitStyles();

            float x = hudPosition.x;
            float y = hudPosition.y;
            float lineHeight = fontSize + 6;
            float width = 460f;

            if (player != null)
            {
                string facing = DirectionNames[player.CurrentDirectionIndex];
                GUI.Label(new Rect(x, y, width, lineHeight + 6),
                    $"Facing: <b>{facing}</b>  ({player.CurrentDirectionIndex})", facingStyle);
                y += lineHeight + 10;
            }

            for (int i = 0; i < log.Count; i++)
            {
                var entry = log[i];
                float alpha = 1f;
                if (lineFadeDuration > 0f)
                {
                    float age = Time.time - entry.spawnTime;
                    alpha = Mathf.Clamp01(1f - age / lineFadeDuration);
                }
                Color c = textColor;
                c.a *= alpha;
                logStyle.normal.textColor = c;
                GUI.Label(new Rect(x, y + i * lineHeight, width, lineHeight), entry.text, logStyle);
            }
        }
    }
}
