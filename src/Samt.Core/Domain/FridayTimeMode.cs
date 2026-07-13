namespace Samt.Core.Domain;

public enum FridayTimeMode
{
    /// <summary>Use calculated Dhuhr as Jumu'ah time.</summary>
    FollowDhuhr = 0,

    /// <summary>Use a fixed local clock time stored on the location profile.</summary>
    FixedTime = 1
}
