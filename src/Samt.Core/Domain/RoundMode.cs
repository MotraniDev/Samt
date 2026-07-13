namespace Samt.Core.Domain;

/// <summary>How internal second-precision times become display/schedule minutes.</summary>
public enum RoundMode
{
    /// <summary>Nearest minute (30s and above rounds up).</summary>
    NearestMinute = 0,

    /// <summary>Always floor to the earlier minute.</summary>
    FloorMinute = 1,

    /// <summary>Always ceil to the later minute.</summary>
    CeilMinute = 2
}
