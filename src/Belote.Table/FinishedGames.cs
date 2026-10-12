using System;
using System.Collections.Generic;
using System.IO;

namespace Belote.Table;

/// <summary>
/// Server-side, in-memory store of finished games and their omniscient logs. It is never exposed to
/// a client. When a directory is configured each log is also written as JSON Lines for developers.
/// </summary>
public sealed class FinishedGames
{
    private readonly object _lock = new();
    private readonly List<FinishedGame> _games = new();
    private readonly string? _directory;

    public FinishedGames(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory) ? null : directory;
    }

    public IReadOnlyList<FinishedGame> All
    {
        get
        {
            lock (_lock)
            {
                return _games.ToArray();
            }
        }
    }

    /// <summary>Raised (best effort) when writing a log file fails; the game itself is unaffected.</summary>
    public event Action<Exception>? WriteFailed;

    public void Add(FinishedGame game)
    {
        lock (_lock)
        {
            _games.Add(game);
        }

        if (_directory is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                Path.Combine(_directory, $"{game.Log.Seed}-{game.TableId}.jsonl"),
                game.Log.ToJsonLines());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WriteFailed?.Invoke(ex);
        }
    }
}
