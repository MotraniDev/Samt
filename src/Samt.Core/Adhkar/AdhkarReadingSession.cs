namespace Samt.Core.Adhkar;

/// <summary>
/// In-session reading progress for one collection open:
/// per-item counters, completion checkmarks, and path progress.
/// </summary>
public sealed class AdhkarReadingSession
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private AdhkarCollection? _collection;

    public AdhkarCollection? Collection => _collection;

    public void Bind(AdhkarCollection collection, bool reset = true)
    {
        _collection = collection;
        if (reset)
        {
            Reset();
        }
    }

    public void Reset()
    {
        _counts.Clear();
    }

    public int GetCount(string itemId)
        => _counts.TryGetValue(itemId, out var n) ? n : 0;

    public int GetTarget(AdhkarItem item)
        => Math.Max(1, item.RepeatCount);

    public bool IsItemComplete(AdhkarItem item)
        => GetCount(item.Id) >= GetTarget(item);

    /// <summary>Increment by one toward the target; returns new count.</summary>
    public int Increment(AdhkarItem item)
    {
        var target = GetTarget(item);
        var next = Math.Min(target, GetCount(item.Id) + 1);
        _counts[item.Id] = next;
        return next;
    }

    public void MarkComplete(AdhkarItem item)
        => _counts[item.Id] = GetTarget(item);

    public void ResetItem(AdhkarItem item)
        => _counts.Remove(item.Id);

    public int CompletedItemCount
    {
        get
        {
            if (_collection is null)
            {
                return 0;
            }

            return _collection.Items.Count(IsItemComplete);
        }
    }

    public int TotalItems => _collection?.Items.Count ?? 0;

    /// <summary>0–1 fraction of items fully completed.</summary>
    public double ItemProgress
        => TotalItems == 0 ? 0 : (double)CompletedItemCount / TotalItems;

    /// <summary>Sum of all repetition progress toward total required counts.</summary>
    public int CompletedRepetitions
    {
        get
        {
            if (_collection is null)
            {
                return 0;
            }

            var sum = 0;
            foreach (var item in _collection.Items)
            {
                sum += Math.Min(GetCount(item.Id), GetTarget(item));
            }

            return sum;
        }
    }

    public int TotalRepetitions
    {
        get
        {
            if (_collection is null)
            {
                return 0;
            }

            return _collection.Items.Sum(GetTarget);
        }
    }

    /// <summary>0–1 fraction of all required repetitions done.</summary>
    public double RepetitionProgress
    {
        get
        {
            var total = TotalRepetitions;
            return total == 0 ? 0 : (double)CompletedRepetitions / total;
        }
    }
}
