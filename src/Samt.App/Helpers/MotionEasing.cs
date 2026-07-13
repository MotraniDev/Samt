namespace Samt_App.Helpers;

/// <summary>
/// Shared easing curves for overlay / design-lab toast motion.
/// Tuned for a calm prayer companion — soft ease-out entrance, gentle ease-in exit.
/// </summary>
internal static class MotionEasing
{
    /// <summary>
    /// Entrance: cubic-bezier(0.16, 1, 0.3, 1) — quick start, long soft settle (Material-like ease-out).
    /// </summary>
    public static double EaseOut(double t)
        => CubicBezier(Clamp01(t), 0.16, 1.0, 0.30, 1.0);

    /// <summary>
    /// Exit: cubic-bezier(0.4, 0, 0.7, 0) — soft accelerate away.
    /// </summary>
    public static double EaseIn(double t)
        => CubicBezier(Clamp01(t), 0.40, 0.0, 0.70, 0.0);

    /// <summary>Opacity often feels better with a slightly faster ease-out than position.</summary>
    public static double EaseOutOpacity(double t)
        => CubicBezier(Clamp01(t), 0.20, 0.0, 0.0, 1.0);

    private static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;

    /// <summary>
    /// Approximate CSS cubic-bezier(x1,y1,x2,y2) for progress t in [0,1].
    /// Uses Newton-Raphson on the X curve, then evaluates Y.
    /// </summary>
    private static double CubicBezier(double t, double x1, double y1, double x2, double y2)
    {
        if (t <= 0)
        {
            return 0;
        }

        if (t >= 1)
        {
            return 1;
        }

        // Solve BezierX(s) = t for s, then return BezierY(s).
        var s = t;
        for (var i = 0; i < 6; i++)
        {
            var x = SampleCurveX(s, x1, x2) - t;
            if (Math.Abs(x) < 1e-5)
            {
                break;
            }

            var d = SampleCurveDerivativeX(s, x1, x2);
            if (Math.Abs(d) < 1e-6)
            {
                break;
            }

            s -= x / d;
            s = Clamp01(s);
        }

        return SampleCurveY(s, y1, y2);
    }

    // Cubic Bezier basis: (1-s)^3*P0 + 3(1-s)^2*s*P1 + 3(1-s)*s^2*P2 + s^3*P3
    // With P0=0, P3=1: 3(1-s)^2*s*p1 + 3(1-s)*s^2*p2 + s^3
    private static double SampleCurveX(double s, double x1, double x2)
    {
        var inv = 1 - s;
        return 3 * inv * inv * s * x1 + 3 * inv * s * s * x2 + s * s * s;
    }

    private static double SampleCurveY(double s, double y1, double y2)
    {
        var inv = 1 - s;
        return 3 * inv * inv * s * y1 + 3 * inv * s * s * y2 + s * s * s;
    }

    private static double SampleCurveDerivativeX(double s, double x1, double x2)
    {
        var inv = 1 - s;
        return 3 * inv * inv * x1 + 6 * inv * s * (x2 - x1) + 3 * s * s * (1 - x2);
    }
}
