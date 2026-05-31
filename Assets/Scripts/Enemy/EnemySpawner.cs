using System;
using System.Collections.Generic;
using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 在玩家周围 8 个固定方位生成敌人。始终维持场上恰好 1 只普通敌人 + 1 只小猫咪;
    /// 任意一只被击杀后,延迟在另一个未被占用的方位补生成同种类的一只。
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Transform player;
        [Tooltip("普通敌人预制体(击杀加分)。")]
        [SerializeField] private Enemy enemyPrefab;
        [Tooltip("小猫咪预制体(击杀扣分)。留空则只生成普通敌人。")]
        [SerializeField] private Enemy kittenPrefab;

        [Header("生成参数")]
        [Tooltip("敌人围绕玩家的半径(米)。")]
        [SerializeField] private float spawnRadius = 4f;

        [Tooltip("敌人 Y 轴高度(相对玩家位置)。")]
        [SerializeField] private float spawnHeightOffset = 0f;

        [Tooltip("敌人死亡后再次生成的延迟(秒)。")]
        [SerializeField] private float respawnDelay = 3f;

        [Tooltip("是否避免在玩家上一次面向的方位重复生成,提高节奏感。")]
        [SerializeField] private bool avoidLastKilledDirection = true;

        [Tooltip("是否在 Start 时自动生成敌人。挂上 GameManager 时把它关掉,由 GameManager 控制开始。")]
        [SerializeField] private bool autoStart = true;

        private readonly Dictionary<int, Enemy> activeEnemies = new Dictionary<int, Enemy>();
        private int lastKilledDirection = -1;
        private PlayerController playerController;
        private int currentFacingDirection = -1;

        public IReadOnlyDictionary<int, Enemy> ActiveEnemies => activeEnemies;
        public int ActiveCount => activeEnemies.Count;

        /// <summary>敌人被玩家击杀时触发(StopRound 的清场不触发)。</summary>
        public event Action<Enemy> OnEnemyKilled;

        private void Start()
        {
            if (player == null)
            {
                Debug.LogError("[EnemySpawner] 未设置 player 引用。");
                enabled = false;
                return;
            }
            if (enemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] 未设置 enemyPrefab。");
                enabled = false;
                return;
            }

            playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.OnDirectionChanged += HandlePlayerDirectionChanged;
                currentFacingDirection = playerController.CurrentDirectionIndex;
            }

            if (autoStart)
            {
                StartRound();
            }
        }

        /// <summary>开始一轮:清场后生成 1 只普通敌人 + 1 只小猫咪。</summary>
        public void StartRound()
        {
            StopRound();
            SpawnEnemy(Enemy.EnemyType.Normal);
            if (kittenPrefab != null) SpawnEnemy(Enemy.EnemyType.Kitten);
        }

        /// <summary>结束一轮:取消重生计时,立刻清除所有现存敌人(不播击杀音)。</summary>
        public void StopRound()
        {
            CancelInvoke();
            foreach (var pair in activeEnemies)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
            activeEnemies.Clear();
            lastKilledDirection = -1;
        }

        /// <summary>给外部(如 GameManager)在 Awake 阶段切换 autoStart。</summary>
        public void SetAutoStart(bool value) { autoStart = value; }

        private void OnDestroy()
        {
            if (playerController != null)
            {
                playerController.OnDirectionChanged -= HandlePlayerDirectionChanged;
            }
        }

        private void HandlePlayerDirectionChanged(int newDirection)
        {
            int previous = currentFacingDirection;
            currentFacingDirection = newDirection;

            if (previous >= 0 && activeEnemies.TryGetValue(previous, out var prevEnemy) && prevEnemy != null)
            {
                prevEnemy.SetPlayerFacing(false);
            }
            if (activeEnemies.TryGetValue(newDirection, out var newEnemy) && newEnemy != null)
            {
                newEnemy.SetPlayerFacing(true);
            }
        }

        /// <summary>由 Enemy.Kill() 回调,用于释放方位并安排重生。</summary>
        public void NotifyEnemyDied(Enemy enemy)
        {
            if (enemy == null) return;
            int dir = enemy.DirectionIndex;
            if (activeEnemies.TryGetValue(dir, out var current) && current == enemy)
            {
                activeEnemies.Remove(dir);
                lastKilledDirection = dir;
                OnEnemyKilled?.Invoke(enemy);

                // 维持场上始终 1 猫 + 1 怪:死了哪种就补哪种。
                float delay = Mathf.Max(0f, respawnDelay);
                if (enemy.Type == Enemy.EnemyType.Kitten)
                    Invoke(nameof(RespawnKitten), delay);
                else
                    Invoke(nameof(RespawnNormal), delay);
            }
        }

        /// <summary>查询某方位上的敌人(没有则返回 null)。</summary>
        public Enemy GetEnemyAt(int directionIndex)
        {
            activeEnemies.TryGetValue(GameDirection.WrapIndex(directionIndex), out var e);
            return e;
        }

        // 供 Invoke 调用的无参包装。
        private void RespawnNormal() => SpawnEnemy(Enemy.EnemyType.Normal);
        private void RespawnKitten() => SpawnEnemy(Enemy.EnemyType.Kitten);

        private void SpawnEnemy(Enemy.EnemyType type)
        {
            Enemy prefab = type == Enemy.EnemyType.Kitten ? kittenPrefab : enemyPrefab;
            if (prefab == null) return;

            int dir = PickFreeDirection();
            if (dir < 0) return;

            Vector3 pos = GameDirection.IndexToPosition(player.position, dir, spawnRadius);
            pos.y += spawnHeightOffset;

            Quaternion lookAtPlayer = Quaternion.LookRotation(
                new Vector3(player.position.x - pos.x, 0f, player.position.z - pos.z).normalized,
                Vector3.up);

            Enemy enemy = Instantiate(prefab, pos, lookAtPlayer, transform);
            enemy.name = type == Enemy.EnemyType.Kitten ? $"Kitten_Dir{dir}" : $"Enemy_Dir{dir}";
            enemy.Initialize(this, dir);
            activeEnemies[dir] = enemy;

            if (dir == currentFacingDirection)
            {
                enemy.SetPlayerFacing(true);
            }
        }

        private int PickFreeDirection()
        {
            List<int> candidates = new List<int>(GameDirection.Count);
            for (int i = 0; i < GameDirection.Count; i++)
            {
                if (activeEnemies.ContainsKey(i)) continue;
                if (avoidLastKilledDirection && i == lastKilledDirection && CountFreeExcluding(lastKilledDirection) > 0)
                    continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < GameDirection.Count; i++)
                    if (!activeEnemies.ContainsKey(i)) candidates.Add(i);
            }

            if (candidates.Count == 0) return -1;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private int CountFreeExcluding(int excluded)
        {
            int count = 0;
            for (int i = 0; i < GameDirection.Count; i++)
            {
                if (i == excluded) continue;
                if (!activeEnemies.ContainsKey(i)) count++;
            }
            return count;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (player == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < GameDirection.Count; i++)
            {
                Vector3 p = GameDirection.IndexToPosition(player.position, i, spawnRadius);
                p.y += spawnHeightOffset;
                Gizmos.DrawWireSphere(p, 0.3f);
                Gizmos.DrawLine(player.position, p);
            }
        }
#endif
    }
}
