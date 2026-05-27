using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Switch;

namespace AltControllerGame
{
    public class JoyconControl : MonoBehaviour
    {
        public enum GyroAxis
        {
            X,
            Y,
            Z
        }

        [Header("Reference")]
        [SerializeField] private PlayerController playerController;

        [Header("Left Joy-Con Direction")]
        [SerializeField] private bool useLeftJoyConForDirection = true;
        [SerializeField] private GyroAxis leftYawAxis = GyroAxis.Y;
        [SerializeField] private bool invertLeftYaw = false;
        [SerializeField] private float yawSensitivity = 1f;
        [SerializeField] private float yawDeadZone = 1.5f;

        [Tooltip("八方位每格 45 度，不建議改。")]
        [SerializeField] private float snapAngle = 45f;

        [Header("Right Joy-Con Attack")]
        [SerializeField] private bool useRightJoyConForAttack = true;
        [SerializeField] private float swingGyroThreshold = 45f;
        [SerializeField] private float swingAccelDeltaThreshold = 0.25f;
        [SerializeField] private float attackCooldown = 0.35f;

        [Header("Debug")]
        [SerializeField] private bool showDebugText = true;
        [SerializeField] private Key recenterKey = Key.R;

        private SwitchJoyConLHID leftJoyCon;
        private SwitchJoyConRHID rightJoyCon;

        private bool leftIMUEnabled;
        private bool rightIMUEnabled;

        private float rawYaw;
        private float snappedYaw;
        private int currentFacingIndex = -1;

        private float leftGyroYawValue;

        private Vector3 lastRightAccel;
        private bool hasLastRightAccel;

        private float rightGyroPower;
        private float rightAccelDeltaPower;
        private float attackCooldownTimer;

        private string leftStatus = "Left Joy-Con not connected";
        private string rightStatus = "Right Joy-Con not connected";
        private string lastAction = "None";

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = FindObjectOfType<PlayerController>();
            }
        }

        private void Start()
        {
            if (playerController != null)
            {
                currentFacingIndex = playerController.CurrentDirectionIndex;
                rawYaw = currentFacingIndex * snapAngle;
            }
        }

        private void Update()
        {
            FindJoyCons();
            EnableIMU();

            if (useLeftJoyConForDirection && leftJoyCon != null && leftIMUEnabled)
            {
                UpdateDirectionFromLeftJoyCon();
            }

            if (useRightJoyConForAttack && rightJoyCon != null && rightIMUEnabled)
            {
                UpdateAttackFromRightJoyCon();
            }

            if (Keyboard.current != null && Keyboard.current[recenterKey].wasPressedThisFrame)
            {
                RecenterDirection();
            }
        }

        private void FindJoyCons()
        {
            if (useLeftJoyConForDirection && leftJoyCon == null)
            {
                leftJoyCon = InputSystem.GetDevice<SwitchJoyConLHID>();

                if (leftJoyCon != null)
                {
                    leftStatus = "Left Joy-Con connected";
                    Debug.Log(leftStatus);
                }
            }

            if (useRightJoyConForAttack && rightJoyCon == null)
            {
                rightJoyCon = InputSystem.GetDevice<SwitchJoyConRHID>();

                if (rightJoyCon != null)
                {
                    rightStatus = "Right Joy-Con connected";
                    Debug.Log(rightStatus);
                }
            }
        }

        private void EnableIMU()
        {
            if (leftJoyCon != null && !leftIMUEnabled)
            {
                leftJoyCon.SetIMUEnabled(true);
                leftIMUEnabled = true;
                Debug.Log("Left Joy-Con IMU enabled.");
            }

            if (rightJoyCon != null && !rightIMUEnabled)
            {
                rightJoyCon.SetIMUEnabled(true);
                rightIMUEnabled = true;
                Debug.Log("Right Joy-Con IMU enabled.");
            }
        }

        private void UpdateDirectionFromLeftJoyCon()
        {
            if (playerController == null) return;

            Vector3 gyro = leftJoyCon.angularVelocity.ReadValue();

            float yawInput = GetAxisValue(gyro, leftYawAxis);
            leftGyroYawValue = yawInput;

            if (Mathf.Abs(yawInput) < yawDeadZone)
            {
                yawInput = 0f;
            }

            if (invertLeftYaw)
            {
                yawInput *= -1f;
            }

            rawYaw += yawInput * yawSensitivity * Time.deltaTime;

            float normalizedYaw = NormalizeAngle360(rawYaw);
            snappedYaw = Mathf.Round(normalizedYaw / snapAngle) * snapAngle;
            snappedYaw = NormalizeAngle360(snappedYaw);

            int newFacingIndex = Mathf.RoundToInt(snappedYaw / snapAngle) % 8;

            if (newFacingIndex != currentFacingIndex)
            {
                currentFacingIndex = newFacingIndex;

                // 重點：只把方位傳給同學的 PlayerController。
                // 後續旋轉、OnDirectionChanged、敵人 SetPlayerFacing 都交給原本程式。
                playerController.SetDirection(currentFacingIndex);

                lastAction = "Set Direction: " + currentFacingIndex;
                Debug.Log(lastAction);
            }
        }

        private void UpdateAttackFromRightJoyCon()
        {
            if (playerController == null) return;

            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            Vector3 gyro = rightJoyCon.angularVelocity.ReadValue();
            Vector3 accel = rightJoyCon.acceleration.ReadValue();

            rightGyroPower = gyro.magnitude;

            if (hasLastRightAccel)
            {
                rightAccelDeltaPower = (accel - lastRightAccel).magnitude;
            }
            else
            {
                rightAccelDeltaPower = 0f;
                hasLastRightAccel = true;
            }

            lastRightAccel = accel;

            if (attackCooldownTimer > 0f) return;

            bool gyroSwing = rightGyroPower >= swingGyroThreshold;
            bool accelSwing = rightAccelDeltaPower >= swingAccelDeltaThreshold;

            if (gyroSwing || accelSwing)
            {
                attackCooldownTimer = attackCooldown;

                // 先通知「玩家做了攻擊」。
                // 如果之後有人用 OnAttack 接動畫或 UI，這個事件會被觸發。
                playerController.TriggerAttack();

                // 真正執行揮砍。
                // 命中/空揮音效、KillFacingEnemy、OnSlashPerformed 都在 PlayerController 裡處理。
                playerController.PerformSlash();

                lastAction = "Joy-Con Swing Attack";
                Debug.Log(lastAction);
            }
        }

        private float GetAxisValue(Vector3 value, GyroAxis axis)
        {
            switch (axis)
            {
                case GyroAxis.X:
                    return value.x;

                case GyroAxis.Y:
                    return value.y;

                case GyroAxis.Z:
                    return value.z;

                default:
                    return value.y;
            }
        }

        private float NormalizeAngle360(float angle)
        {
            angle %= 360f;

            if (angle < 0f)
            {
                angle += 360f;
            }

            return angle;
        }

        public void RecenterDirection()
        {
            if (playerController == null) return;

            rawYaw = 0f;
            snappedYaw = 0f;
            currentFacingIndex = 0;

            playerController.SetDirection(0);

            lastAction = "Recenter Direction";
            Debug.Log("Joy-Con direction recentered to 0 / Front.");
        }

        private void OnGUI()
        {
            if (!showDebugText) return;

            GUI.Label(new Rect(20, 20, 900, 25), "Left Status: " + leftStatus);
            GUI.Label(new Rect(20, 45, 900, 25), "Right Status: " + rightStatus);

            GUI.Label(new Rect(20, 80, 900, 25), "Left Gyro Yaw Value: " + leftGyroYawValue.ToString("0.000"));
            GUI.Label(new Rect(20, 105, 900, 25), "Raw Yaw: " + rawYaw.ToString("0.000"));
            GUI.Label(new Rect(20, 130, 900, 25), "Snapped Yaw: " + snappedYaw.ToString("0.000"));
            GUI.Label(new Rect(20, 155, 900, 25), "Facing Index Sent To PlayerController: " + currentFacingIndex);

            GUI.Label(new Rect(20, 190, 900, 25), "Right Gyro Power: " + rightGyroPower.ToString("0.000"));
            GUI.Label(new Rect(20, 215, 900, 25), "Right Accel Delta Power: " + rightAccelDeltaPower.ToString("0.000"));

            GUI.Label(new Rect(20, 250, 900, 25), "Last Action: " + lastAction);
            GUI.Label(new Rect(20, 275, 900, 25), "Press " + recenterKey + " to recenter direction");
        }
    }
}
