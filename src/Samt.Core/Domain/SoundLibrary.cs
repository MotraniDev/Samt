namespace Samt.Core.Domain;

/// <summary>Built-in catalog IDs for packaged adhan / phrase cues.</summary>
public static class BuiltInSoundIds
{
    public const string SoftTone = "soft-tone";
    public const string AdhanAbdulBasit = "adhan-abdul-basit";
    public const string AdhanAbdulGhaffar = "adhan-abdul-ghaffar";
    public const string AdhanAbdulHakam = "adhan-abdul-hakam";
    public const string AdhanAlaqsa = "adhan-alaqsa";
    public const string AdhanEgypt = "adhan-egypt";
    public const string Takbir = "phrase-takbir";
    public const string HayyaAlaSalah = "phrase-hayya-alas-salah";
    public const string Silent = "silent";
}

/// <summary>User-imported sound stored under LocalAppData or absolute path.</summary>
public sealed class UserSoundEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string FilePath { get; init; }
}

/// <summary>UI/catalog row for sound pickers (built-in or user).</summary>
public sealed class SoundCatalogItem
{
    public required string Id { get; init; }
    public required string DisplayNameAr { get; init; }
    public required string DisplayNameEn { get; init; }
    public required string? RelativeFileName { get; init; }
    public bool IsBuiltIn { get; init; }
    public bool IsSilent { get; init; }
    public SoundCatalogRole Role { get; init; }
}

public enum SoundCatalogRole
{
    Any = 0,
    Adhan = 1,
    PreAlert = 2
}
