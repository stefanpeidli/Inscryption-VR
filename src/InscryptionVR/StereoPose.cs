using UnityEngine;

namespace InscryptionVR
{
    internal static class StereoPose
    {
        // OpenVR poses are right-handed row-major 3x4. Change world and local basis with S*M*S.
        internal static Vector3 Position(float[] matrix)
        {
            return new Vector3(matrix[3], matrix[7], -matrix[11]);
        }

        internal static Quaternion Rotation(float[] matrix)
        {
            return Quaternion.LookRotation(new Vector3(-matrix[2], -matrix[6], matrix[10]),
                new Vector3(matrix[1], matrix[5], -matrix[9]));
        }

        internal static Matrix4x4 Projection(float[] raw, float near, float far)
        {
            // OpenVR raw top/bottom tangents use downward-positive Y. Unity camera projection uses upward Y.
            return Matrix4x4.Frustum(raw[0] * near, raw[1] * near, -raw[3] * near, -raw[2] * near, near, far);
        }

        internal static bool IsFinite(float[] values)
        {
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return false;
            return true;
        }

        internal static bool IsPoseValid(float[] values)
        {
            if (!IsFinite(values)) return false;
            var forward = new Vector3(-values[2], -values[6], values[10]);
            var up = new Vector3(values[1], values[5], -values[9]);
            return forward.sqrMagnitude > 0.5f && up.sqrMagnitude > 0.5f &&
                Vector3.Cross(forward, up).sqrMagnitude > 0.25f;
        }

        internal static bool IsFrustumValid(float[] values)
        {
            return IsFinite(values) && values[0] < values[1] && values[2] < values[3];
        }
    }
}
