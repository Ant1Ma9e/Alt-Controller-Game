using UnityEngine;

namespace AltControllerGame
{
    /// <summary>
    /// 八方位通用工具。索引 0 = 正前方 (+Z),顺时针每 45° 一个方位。
    /// 0:N  1:NE  2:E  3:SE  4:S  5:SW  6:W  7:NW
    /// </summary>
    public static class GameDirection
    {
        public const int Count = 8;
        public const float StepDegrees = 360f / Count;

        public static float IndexToYaw(int index)
        {
            return Mathf.Repeat(index, Count) * StepDegrees;
        }

        public static int YawToIndex(float yawDegrees)
        {
            float normalized = Mathf.Repeat(yawDegrees + StepDegrees * 0.5f, 360f);
            return Mathf.FloorToInt(normalized / StepDegrees) % Count;
        }

        public static Vector3 IndexToDirection(int index)
        {
            float rad = IndexToYaw(index) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        public static Vector3 IndexToPosition(Vector3 center, int index, float radius)
        {
            return center + IndexToDirection(index) * radius;
        }

        public static Quaternion IndexToRotation(int index)
        {
            return Quaternion.Euler(0f, IndexToYaw(index), 0f);
        }

        public static int WrapIndex(int index)
        {
            return ((index % Count) + Count) % Count;
        }
    }
}
