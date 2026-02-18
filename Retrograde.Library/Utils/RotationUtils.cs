using System;
using System.Numerics;

namespace Retrograde.Utils;

/// <summary>
/// Rotation utilities for quaternion/Euler conversions and cardinal rotations.
/// Ported from StarTiller (credit: dafydd99).
/// </summary>
public static class RotationUtils
{
    public static Vector3 RotateAroundZ(Vector3 rotation, float angle)
    {
        Vector3 normalisedAngles = GetLocalAngles(new Vector3(rotation.X, rotation.Y, -rotation.Z));
        Vector3 angles = GetLocalAngles(new Vector3(normalisedAngles.X, normalisedAngles.Y, -normalisedAngles.Z + angle));
        return angles;
    }

    public static Vector3 GetLocalAngles(Vector3 localAng)
    {
        Vector3 worldAng;

        localAng.X = -localAng.X;
        localAng.Y = -localAng.Y;
        localAng.Z = -localAng.Z;

        float sx = (float)Math.Sin(localAng.X);
        float cx = (float)Math.Cos(localAng.X);
        float sy = (float)Math.Sin(localAng.Y);
        float cy = (float)Math.Cos(localAng.Y);
        float sz = (float)Math.Sin(localAng.Z);
        float cz = (float)Math.Cos(localAng.Z);

        // ZYX rotation matrix
        float r11 = cz * cy;
        float r12 = -sz * cx + cz * sy * sx;
        float r13 = sz * sx + cz * sy * cx;
        float r21 = sz * cy;
        float r22 = cz * cx + sz * sy * sx;
        float r23 = cz * -sx + sz * sy * cx;
        float r31 = -sy;
        float r32 = cy * sx;
        float r33 = cy * cx;

        if (r13 > 0.9998)
        {
            worldAng.X = (float)-Math.Atan2(r32, r22);
            worldAng.Y = -1.5708f;
            worldAng.Z = 0;
        }
        else if (r13 < -0.9998)
        {
            worldAng.X = (float)-Math.Atan2(r32, r22);
            worldAng.Y = 1.5708f;
            worldAng.Z = 0;
        }
        else
        {
            r23 = -r23;
            r12 = -r12;
            worldAng.X = (float)-Math.Atan2(r23, r33);
            worldAng.Y = (float)-Math.Asin(r13);
            worldAng.Z = (float)-Math.Atan2(r12, r11);
        }

        return worldAng;
    }

    public static float EulerToRadCardinals(int euler)
    {
        switch (euler)
        {
            case 0: return 0;
            case 90: return 1.57079632f;
            case 180: return 3.14159f;
            case 270: return 4.71239f;
        }
        return 0;
    }
}
