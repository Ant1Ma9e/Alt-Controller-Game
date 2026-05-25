using System;
using System.Collections;
using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 敌人。占据玩家周围 8 方位之一,被击杀时通知 Spawner。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class Enemy : MonoBehaviour
    {
        [Header("音效片段")]
        [Tooltip("循环播放的环绕音(3D 空间音)。")]
        [SerializeField] private AudioClip ambientClip;
        [Tooltip("玩家面向该敌人时播放一次的提示音。")]
        [SerializeField] private AudioClip facingAlertClip;
        [Tooltip("被击杀时播放的反馈音。")]
        [SerializeField] private AudioClip killClip;

        [Tooltip("击杀音效音量(0-1)。")]
        [Range(0f, 1f)]
        [SerializeField] private float killClipVolume = 1f;

        [Header("循环节奏(秒)")]
        [Tooltip("环绕音每次播放结束后的停顿时长。")]
        [Min(0f)]
        [SerializeField] private float ambientLoopGap = 0.6f;

        [Tooltip("面向提示音每次播放结束后的停顿时长。")]
        [Min(0f)]
        [SerializeField] private float facingAlertLoopGap = 0.4f;

        [Tooltip("两段 clip 互相切换前的过渡静默时长。")]
        [Min(0f)]
        [SerializeField] private float switchSilence = 0.3f;

        private enum AudioMode { None, Ambient, FacingAlert }

        private AudioSource audioSource;
        private EnemySpawner spawner;
        private int directionIndex = -1;
        private bool isAlive = true;
        private AudioMode currentMode = AudioMode.None;
        private Coroutine loopRoutine;

        public int DirectionIndex => directionIndex;
        public bool IsAlive => isAlive;

        /// <summary>敌人被击杀时触发。</summary>
        public event Action<Enemy> OnDied;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                // 我们自己管理循环节奏,关掉原生 loop。
                audioSource.loop = false;
                audioSource.playOnAwake = false;
            }
        }

        /// <summary>由 Spawner 在生成时调用。</summary>
        public void Initialize(EnemySpawner owner, int dirIndex)
        {
            spawner = owner;
            directionIndex = dirIndex;
            isAlive = true;

            SwitchMode(AudioMode.Ambient, useSwitchSilence: false);
        }

        /// <summary>当玩家面向/不再面向该敌人时由 Spawner 驱动。</summary>
        public void SetPlayerFacing(bool isFacing)
        {
            SwitchMode(isFacing ? AudioMode.FacingAlert : AudioMode.Ambient, useSwitchSilence: true);
        }

        private void SwitchMode(AudioMode mode, bool useSwitchSilence)
        {
            if (currentMode == mode) return;
            currentMode = mode;

            if (loopRoutine != null) StopCoroutine(loopRoutine);
            if (audioSource != null) audioSource.Stop();

            AudioClip clip = mode == AudioMode.Ambient ? ambientClip : facingAlertClip;
            float gap = mode == AudioMode.Ambient ? ambientLoopGap : facingAlertLoopGap;
            float initialDelay = useSwitchSilence ? switchSilence : 0f;

            if (clip == null) return;

            loopRoutine = StartCoroutine(LoopClipWithGap(clip, gap, initialDelay));
        }

        private IEnumerator LoopClipWithGap(AudioClip clip, float gap, float initialDelay)
        {
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

            while (isAlive && audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
                float wait = clip.length + Mathf.Max(0f, gap);
                yield return new WaitForSeconds(wait);
            }
        }

        /// <summary>击杀此敌人。</summary>
        public void Kill()
        {
            if (!isAlive) return;
            isAlive = false;

            // 先停掉自身的环绕/提示音,再用 PlayClipAtPoint 播击杀音,保证同时只有一段在响。
            if (loopRoutine != null) StopCoroutine(loopRoutine);
            if (audioSource != null) audioSource.Stop();

            if (killClip != null)
            {
                // PlayClipAtPoint 会产生临时 AudioSource,播放完自动销毁,
                // 因此即使本物体被 Destroy,击杀音也会完整播放。
                AudioSource.PlayClipAtPoint(killClip, transform.position, killClipVolume);
            }

            OnDied?.Invoke(this);
            if (spawner != null) spawner.NotifyEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
