using UnityEngine;

public static class MatrixMath
{
    public static Matrix4x4 Translation(Vector3 t)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m03 = t.x;
        m.m13 = t.y;
        m.m23 = t.z;
        return m;
    }

    public static Matrix4x4 Scale(Vector3 s)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = s.x;
        m.m11 = s.y;
        m.m22 = s.z;
        return m;
    }

    public static Matrix4x4 RotationX(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        Matrix4x4 m = Matrix4x4.identity;
        m.m11 = c;  m.m12 = -s;
        m.m21 = s;  m.m22 = c;
        return m;
    }

    public static Matrix4x4 RotationY(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = c;  m.m02 = s;
        m.m20 = -s; m.m22 = c;
        return m;
    }

    public static Matrix4x4 RotationZ(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = c;  m.m01 = -s;
        m.m10 = s;  m.m11 = c;
        return m;
    }

    public static Matrix4x4 RotationEuler(Vector3 eulerDegrees)
    {
        return RotationY(eulerDegrees.y) * RotationX(eulerDegrees.x) * RotationZ(eulerDegrees.z);
    }

    public static Matrix4x4 TRS(Vector3 position, Vector3 eulerDegrees, Vector3 scale)
    {
        return Translation(position) * RotationEuler(eulerDegrees) * Scale(scale);
    }

    public static Vector3 ExtractPosition(Matrix4x4 m)
    {
        return new Vector3(m.m03, m.m13, m.m23);
    }

    public static Vector3 ExtractScale(Matrix4x4 m)
    {
        Vector3 x = new Vector3(m.m00, m.m10, m.m20);
        Vector3 y = new Vector3(m.m01, m.m11, m.m21);
        Vector3 z = new Vector3(m.m02, m.m12, m.m22);
        Vector3 s = new Vector3(x.magnitude, y.magnitude, z.magnitude);

        if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f) s.x = -s.x;
        return s;
    }

    public static Quaternion ExtractRotation(Matrix4x4 m)
    {
        Vector3 forward = new Vector3(m.m02, m.m12, m.m22);
        Vector3 up = new Vector3(m.m01, m.m11, m.m21);
        if (forward.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f) return Quaternion.identity;
        return Quaternion.LookRotation(forward, up);
    }

    public static void ApplyToLocal(Transform t, Matrix4x4 m)
    {
        t.localPosition = ExtractPosition(m);
        t.localRotation = ExtractRotation(m);
        t.localScale = ExtractScale(m);
    }
}
