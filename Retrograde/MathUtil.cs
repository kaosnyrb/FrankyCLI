using Noggog;

namespace FrankyCLI
{
    public static class MathUtil
    {
        public static float DistanceSquared(P3Float a, P3Float b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public static bool PositionsClose(P3Float a, P3Float b, float tolerance)
        {
            return System.Math.Abs(a.X - b.X) <= tolerance &&
                   System.Math.Abs(a.Y - b.Y) <= tolerance &&
                   System.Math.Abs(a.Z - b.Z) <= tolerance;
        }

        public static P3Float Subtract(P3Float a, P3Float b)
        {
            return new P3Float(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static float Dot(P3Float a, P3Float b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static float Length(P3Float v)
        {
            return (float)System.Math.Sqrt(Dot(v, v));
        }

        public static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
