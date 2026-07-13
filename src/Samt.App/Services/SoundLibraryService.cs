using Samt.Core.Domain;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Built-in + user sound catalog. Built-ins live under Assets/Audio/library;
/// user files under %LocalAppData%\SAMT\sounds.
/// </summary>
public static class SoundLibraryService
{
    public static IReadOnlyList<SoundCatalogItem> BuiltInCatalog { get; } =
    [
        new()
        {
            Id = BuiltInSoundIds.Silent,
            DisplayNameAr = "صامت",
            DisplayNameEn = "Silent",
            RelativeFileName = null,
            IsBuiltIn = true,
            IsSilent = true,
            Role = SoundCatalogRole.Any
        },
        new()
        {
            Id = BuiltInSoundIds.SoftTone,
            DisplayNameAr = "نغمة ناعمة",
            DisplayNameEn = "Soft alert tone",
            RelativeFileName = null,
            IsBuiltIn = true,
            Role = SoundCatalogRole.PreAlert
        },
        new()
        {
            Id = BuiltInSoundIds.Takbir,
            DisplayNameAr = "تكبير",
            DisplayNameEn = "Takbir",
            RelativeFileName = "phrase-takbir.wav",
            IsBuiltIn = true,
            Role = SoundCatalogRole.PreAlert
        },
        new()
        {
            Id = BuiltInSoundIds.HayyaAlaSalah,
            DisplayNameAr = "حي على الصلاة",
            DisplayNameEn = "Hayya 'ala as-salah",
            RelativeFileName = "phrase-hayya-alas-salah.wav",
            IsBuiltIn = true,
            Role = SoundCatalogRole.PreAlert
        },
        new()
        {
            Id = BuiltInSoundIds.AdhanAlaqsa,
            DisplayNameAr = "أذان الأقصى",
            DisplayNameEn = "Adhan Al-Aqsa",
            RelativeFileName = "adhan-alaqsa.mp3",
            IsBuiltIn = true,
            Role = SoundCatalogRole.Adhan
        },
        new()
        {
            Id = BuiltInSoundIds.AdhanEgypt,
            DisplayNameAr = "أذان مصر",
            DisplayNameEn = "Adhan Egypt",
            RelativeFileName = "adhan-egypt.mp3",
            IsBuiltIn = true,
            Role = SoundCatalogRole.Adhan
        },
        new()
        {
            Id = BuiltInSoundIds.AdhanAbdulBasit,
            DisplayNameAr = "عبد الباسط",
            DisplayNameEn = "Abdul Basit",
            RelativeFileName = "adhan-abdul-basit.mp3",
            IsBuiltIn = true,
            Role = SoundCatalogRole.Adhan
        },
        new()
        {
            Id = BuiltInSoundIds.AdhanAbdulGhaffar,
            DisplayNameAr = "عبد الغفار",
            DisplayNameEn = "Abdul Ghaffar",
            RelativeFileName = "adhan-abdul-ghaffar.mp3",
            IsBuiltIn = true,
            Role = SoundCatalogRole.Adhan
        },
        new()
        {
            Id = BuiltInSoundIds.AdhanAbdulHakam,
            DisplayNameAr = "عبد الحكم",
            DisplayNameEn = "Abdul Hakam",
            RelativeFileName = "adhan-abdul-hakam.mp3",
            IsBuiltIn = true,
            Role = SoundCatalogRole.Adhan
        }
    ];

    public static string UserSoundsDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAMT",
                "sounds");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static IReadOnlyList<SoundCatalogItem> GetCatalog(AppSettings settings)
    {
        var list = new List<SoundCatalogItem>(BuiltInCatalog);
        foreach (var u in settings.UserSounds)
        {
            list.Add(new SoundCatalogItem
            {
                Id = u.Id,
                DisplayNameAr = u.DisplayName,
                DisplayNameEn = u.DisplayName,
                RelativeFileName = u.FilePath,
                IsBuiltIn = false,
                Role = SoundCatalogRole.Any
            });
        }

        return list;
    }

    public static string DisplayName(SoundCatalogItem item, bool arabic)
        => arabic ? item.DisplayNameAr : item.DisplayNameEn;

    public static AudioProfile ProfileForSoundId(string? soundId)
    {
        if (string.IsNullOrWhiteSpace(soundId) || soundId == BuiltInSoundIds.Silent)
        {
            return new AudioProfile { Source = AudioSource.Silent };
        }

        if (soundId == BuiltInSoundIds.SoftTone)
        {
            return new AudioProfile { Source = AudioSource.WindowsDefault };
        }

        return new AudioProfile
        {
            Source = AudioSource.Library,
            SoundId = soundId
        };
    }

    public static string? ResolvePath(string? soundId, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(soundId) || soundId == BuiltInSoundIds.Silent)
        {
            return null;
        }

        if (soundId == BuiltInSoundIds.SoftTone)
        {
            return AdhanAudioService.EnsureDefaultTonePath();
        }

        var builtIn = BuiltInCatalog.FirstOrDefault(b => b.Id == soundId);
        if (builtIn is { RelativeFileName: { } rel })
        {
            var path = FindBuiltInFile(rel);
            if (path is not null)
            {
                return path;
            }

            LaunchLog.Write($"Sound library missing built-in file: {rel}");
            return AdhanAudioService.EnsureDefaultTonePath();
        }

        var user = settings.UserSounds.FirstOrDefault(u => u.Id == soundId);
        if (user is not null && File.Exists(user.FilePath))
        {
            return Path.GetFullPath(user.FilePath);
        }

        LaunchLog.Write($"Sound library id not found: {soundId}");
        return AdhanAudioService.EnsureDefaultTonePath();
    }

    public static string? FindBuiltInFile(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "Assets", "Audio", "library", fileName),
            Path.Combine(baseDir, "Assets", "Audio", fileName),
            Path.Combine(baseDir, "library", fileName)
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Copy a user-selected audio file into the library folder and return a catalog entry.</summary>
    public static UserSoundEntry ImportUserFile(string sourcePath, string? displayName = null)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Audio file not found.", sourcePath);
        }

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".mp3";
        }

        var id = "user-" + Guid.NewGuid().ToString("N")[..12];
        var safeName = string.Concat(
            (displayName ?? Path.GetFileNameWithoutExtension(sourcePath))
            .Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = id;
        }

        var dest = Path.Combine(UserSoundsDirectory, id + ext.ToLowerInvariant());
        File.Copy(sourcePath, dest, overwrite: true);

        return new UserSoundEntry
        {
            Id = id,
            DisplayName = safeName.Length > 48 ? safeName[..48] : safeName,
            FilePath = dest
        };
    }
}
