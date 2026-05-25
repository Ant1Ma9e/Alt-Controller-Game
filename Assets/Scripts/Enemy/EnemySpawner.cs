using System.Collections.Generic;
using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 在玩家周围 8 个固定方位生成敌人。最多同时存在 maxConcurrent 只;
    /// 每死亡一只就在另一个未被占用的方位再生成一只。
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Transform player;
        [SerializeField] private Enemy enemyPrefab;

        [Header("生成参数")]
        [Tooltip("敌人围绕玩家的半径(米)。")]
        [SerializeField] private float spawnRadius = 4f;

        [Tooltip("敌人 Y 轴高度(相对玩家位置)。")]
        [SerializeField] private float spawnHeightOffset = 0f;

        [Tooltip("场上同时存在的最大敌人数。")]
        [Range(1, 8)]
        [SerializeField] private int maxConcurrent = 1;

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

        /// <summary>开始一轮:清干净场上敌人 + 生成 maxConcurrent 只。</summary>
        public void StartRound()
        {
            StopRound();
            for (int i = 0; i < maxConcurrent; i++) SpawnOne();
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
                Invoke(nameof(SpawnOne), Mathf.Max(0f, respawnDelay));
            }
        }

        /// <summary>查询某方位上的敌人(没有则返回 null)。</summary>
        public Enemy GetEnemyAt(int directionIndex)
        {
            activeEnemies.TryGetValue(GameDirection.WrapIndex(directionIndex), out var e);
            return e;
        }

        private void SpawnOne()
        {
            int dir = PickFreeDirection();
            if (dir < 0) return;

            Vector3 pos = GameDirection.IndexToPosition(player.position, dir, spawnRadius);
            pos.y += spawnHeightOffset;

            Quaternion lookAtPlayer = Quaternion.LookRotation(
                new Vector3(player.position.x - pos.x, 0f, player.position.z - pos.z).normalized,
                Vector3.up);

            Enemy enemy = Instantiate(enemyPrefab, pos, lookAtPlayer, transform);
            enemy.name = $"Enemy_Dir{dir}";
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
            return candidates[Random.Range(0, candidates.Count)];
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
