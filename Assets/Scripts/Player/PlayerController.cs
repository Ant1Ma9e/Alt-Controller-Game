using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace AltControllerGame
{
    /// <summary>
    /// 玩家八方位锁定转身。当前用键盘 Q/E 测试,后续替换为 Joy-Con 输入。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerController : MonoBehaviour
    {
        [Header("旋转设置")]
        [Tooltip("旋转到目标方位的插值速度(度/秒)。设为 0 立即吸附。")]
        [SerializeField] private float rotationSpeed = 720f;

        [Tooltip("两次手动旋转之间的最小间隔,防止单次输入连转。")]
        [SerializeField] private float rotateCooldown = 0.12f;

        [Header("调试输入(等待替换为 Joy-Con)")]
        [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode rotateRightKey = KeyCode.E;
        [SerializeField] private KeyCode attackKey = KeyCode.Space;
        [Tooltip("调试用:按下立即击杀玩家当前面向方位的敌人。")]
        [SerializeField] private KeyCode killFacingKey = KeyCode.K;

        [Header("斩击音效")]
        [Tooltip("空挥音(挥砍时面前没有敌人)。")]
        [FormerlySerializedAs("slashClip")]
        [SerializeField] private AudioClip missClip;
        [Tooltip("命中音(挥砍时面前有敌人,实际击杀)。")]
        [SerializeField] private AudioClip hitClip;
        [Tooltip("斩击音效音量(0-1),空挥/命中共用。")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("slashVolume")]
        [SerializeField] private float slashVolume = 1f;

        [Header("引用")]
        [Tooltip("可选;留空时自动 FindObjectOfType。用于按 K 击杀面前敌人。")]
        [SerializeField] private EnemySpawner spawner;

        private AudioSource audioSource;
        private int currentDirectionIndex;
        private float lastRotateTime = -999f;

        /// <summary>当前面向的方位索引 (0-7)。</summary>
        public int CurrentDirectionIndex => currentDirectionIndex;

        /// <summary>玩家方位变化时触发,参数为新方位索引。</summary>
        public event Action<int> OnDirectionChanged;

        /// <summary>玩家触发攻击(挥动摇杆)时触发。</summary>
        public event Action OnAttack;

        /// <summary>玩家挥砍完成时触发,参数 true=命中、false=空挥。</summary>
        public event Action<bool> OnSlashPerformed;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
            }
        }

        private void Start()
        {
            currentDirectionIndex = GameDirection.YawToIndex(transform.eulerAngles.y);
            transform.rotation = GameDirection.IndexToRotation(currentDirectionIndex);

            if (spawner == null)
            {
                spawner = FindObjectOfType<EnemySpawner>();
            }
        }

        private void Update()
        {
            HandleDebugInput();
            ApplyRotation();
        }

        private void HandleDebugInput()
        {
            if (Time.time - lastRotateTime >= rotateCooldown)
            {
                if (Input.GetKeyDown(rotateLeftKey))
                {
                    StepDirection(-1);
                }
                else if (Input.GetKeyDown(rotateRightKey))
                {
                    StepDirection(1);
                }
            }

            if (Input.GetKeyDown(attackKey))
            {
                TriggerAttack();
            }

            if (Input.GetKeyDown(killFacingKey))
            {
                PerformSlash();
            }
        }

        /// <summary>挥砍动作:命中则播放命中音并击杀,未命中则播放空挥音。供 Joy-Con 挥动检测调用。</summary>
        public void PerformSlash()
        {
            bool hit = KillFacingEnemy();
            PlaySlashSfx(hit);
            OnSlashPerformed?.Invoke(hit);
        }

        private void PlaySlashSfx(bool hit)
        {
            if (audioSource == null) return;
            AudioClip clip = hit ? hitClip : missClip;
            if (clip == null) return;
            audioSource.PlayOneShot(clip, slashVolume);
        }

        /// <summary>击杀当前面向方位上的敌人(若存在)。返回是否实际击杀到敌人。</summary>
        public bool KillFacingEnemy()
        {
            if (spawner == null) spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return false;

            Enemy target = spawner.GetEnemyAt(currentDirectionIndex);
            if (target == null || !target.IsAlive) return false;

            target.Kill();
            return true;
        }

        private void ApplyRotation()
        {
            Quaternion target = GameDirection.IndexToRotation(currentDirectionIndex);
            if (rotationSpeed <= 0f)
            {
                transform.rotation = target;
            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>按 step 方向旋转(±1)。供调试输入或 Joy-Con 适配层调用。</summary>
        public void StepDirection(int step)
        {
            SetDirection(GameDirection.WrapIndex(currentDirectionIndex + step));
            lastRotateTime = Time.time;
        }

        /// <summary>直接设置方位索引。</summary>
        public void SetDirection(int newIndex)
        {
            newIndex = GameDirection.WrapIndex(newIndex);
            if (newIndex == currentDirectionIndex) return;
            currentDirectionIndex = newIndex;
            OnDirectionChanged?.Invoke(currentDirectionIndex);
        }

        /// <summary>外部(Joy-Con 挥动检测)调用以触发攻击。</summary>
        public void TriggerAttack()
        {
            OnAttack?.Invoke();
        }
    }
}
