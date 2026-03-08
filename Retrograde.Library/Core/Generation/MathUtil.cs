using Mutagen.Bethesda.Starfield;
using Noggog;

namespace Retrograde;

public static class MathUtil
{
    /// <summary>
    /// Returns the XY circular bounding radius (max of X and Y half-extents) for a record's
    /// ObjectBounds, at least <paramref name="fallback"/>. Returns <paramref name="fallback"/>
    /// when <paramref name="bounds"/> is null.
    /// </summary>
    public static float BoundsRadius(IObjectBoundsGetter? bounds, float fallback)
    {
        var (hx, hy) = BoundsHalfExtents(bounds, fallback);
        return MathF.Max(hx, hy);
    }

    /// <summary>
    /// Returns the (hx, hy) XY half-extents of a record's ObjectBounds, each at least
    /// <paramref name="fallback"/>. Returns (<paramref name="fallback"/>, <paramref name="fallback"/>)
    /// when <paramref name="bounds"/> is null.
    /// </summary>
    public static (float hx, float hy) BoundsHalfExtents(IObjectBoundsGetter? bounds, float fallback)
    {
        if (bounds == null) return (fallback, fallback);
        float hx = MathF.Abs(bounds.Second.X - bounds.First.X) / 2f;
        float hy = MathF.Abs(bounds.Second.Y - bounds.First.Y) / 2f;
        return (MathF.Max(hx, fallback), MathF.Max(hy, fallback));
    }

    public static float DistanceSquared(P3Float a, P3Float b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public static bool PositionsClose(P3Float a, P3Float b, float tolerance)
    {
        return MathF.Abs(a.X - b.X) <= tolerance &&
               MathF.Abs(a.Y - b.Y) <= tolerance &&
               MathF.Abs(a.Z - b.Z) <= tolerance;
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
        return MathF.Sqrt(Dot(v, v));
    }

    public static float Clamp01(float v)
    {
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }
}
