namespace Samt.Core.Domain;

/// <summary>Asr shadow factor: Shafi'i/majority = 1, Hanafi = 2.</summary>
public enum AsrMadhab
{
    /// <summary>Jumhur / Shafi'i — shadow length = object length (factor 1).</summary>
    Standard = 0,

    /// <summary>Hanafi — shadow length = 2 × object length (factor 2).</summary>
    Hanafi = 1
}
