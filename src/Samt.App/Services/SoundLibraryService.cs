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
            // Synthetic cue generated on first use if no packaged file is present.
            RelativeFileName = "phrase-takbir.wav",
            IsBuiltIn = true,
            Role = SoundCatalogRole.PreAlert
        },
        new()
        {
            Id = BuiltInSoundIds.HayyaAlaSalah,
            DisplayNameAr = "حي على الصلاة",
            DisplayNameEn = "Hayya 'ala as-salah",
            // Prefer packaged Haya-ALA-SALAT.mp3; falls back to synthetic phrase wav.
            RelativeFileName = "Haya-ALA-SALAT.mp3",
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

            // Alternate filenames shipped historically / differently cased.
            if (soundId == BuiltInSoundIds.HayyaAlaSalah)
            {
                path = FindBuiltInFile("phrase-hayya-alas-salah.wav")
                       ?? FindBuiltInFile("Haya-ALA-SALAT.mp3");
                if (path is not null)
                {
                    return path;
                }
            }

            if (soundId is BuiltInSoundIds.Takbir or BuiltInSoundIds.HayyaAlaSalah)
            {
                // Generate a short distinct cue so pre-alert never fails silently.
                return EnsurePhraseCuePath(soundId);
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

    /// <summary>
    /// Short synthetic pre-alert cue (takbir / hayya) written once under LocalAppData\SAMT\tones.
    /// Used when packaged phrase files are absent.
    /// </summary>
    internal static string EnsurePhraseCuePath(string soundId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SAMT",
            "tones");
        Directory.CreateDirectory(dir);

        var isHayya = soundId == BuiltInSoundIds.HayyaAlaSalah;
        var fileName = isHayya ? "phrase-hayya-alas-salah.wav" : "phrase-takbir.wav";
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            // Distinct soft pulses: takbir ~ lower / shorter; hayya slightly longer rising pair.
            WritePhraseCueWav(path, isHayya ? 3.0 : 1.8, isHayya ? 440.0 : 523.25, isHayya ? 587.33 : 659.25);
            LaunchLog.Write($"Sound library generated phrase cue: {path}");
        }

        return path;
    }

    private static void WritePhraseCueWav(string path, double durationSec, double freqA, double freqB)
    {
        const int sampleRate = 22050;
        var sampleCount = (int)(sampleRate * durationSec);
        var data = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var half = durationSec * 0.48;
            var inFirst = t < half;
            var local = inFirst ? t / half : (t - half) / Math.Max(0.01, durationSec - half);
            var env = Math.Sin(Math.PI * Math.Clamp(local, 0, 1)) * 0.28;
            // Gap between the two syllables.
            if (t > half - 0.04 && t < half + 0.06)
            {
                env = 0;
            }

            var freq = inFirst ? freqA : freqB;
            var sample = env * Math.Sin(2 * Math.PI * freq * t);
            data[i] = (short)(sample * short.MaxValue);
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        var byteCount = data.Length * 2;
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + byteCount);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(byteCount);
        foreach (var s in data)
        {
            bw.Write(s);
        }
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
