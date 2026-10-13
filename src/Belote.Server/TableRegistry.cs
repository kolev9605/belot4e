using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Belote.Table;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Belote.Server;

/// <summary>Server settings bound from the <c>Table</c> configuration section.</summary>
public sealed class TableServerOptions
{
    public const string SectionName = "Table";

    /// <summary>Default pause before each bot move, in milliseconds (clamped to 0..5000 per table).</summary>
    public int BotDelayMs { get; set; } = 600;

    /// <summary>Optional directory receiving one JSON Lines log per finished game.</summary>
    public string? LogDirectory { get; set; }

    /// <summary>Clock used for bot pacing; tests may replace it.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}

/// <summary>
/// Holds the single active table and the finished-game store. At most one unfinished table exists;
/// a table is replaced after its game has finished, or by a different connection's CreateTable.
/// </summary>
public sealed class TableRegistry
{
    public const string TableClosedMethod = "TableClosed";

    private readonly object _gate = new();
    private readonly IHubContext<TableHub> _hub;
    private readonly TableServerOptions _options;
    private readonly ILogger<TableRegistry> _log;

    private LiveTable? _table;
    private ClientUpdatePump? _pump;
    private IDisposable? _subscription;
    private string? _ownerConnectionId;

    public TableRegistry(IHubContext<TableHub> hub, TableServerOptions options, ILogger<TableRegistry> log)
    {
        _hub = hub;
        _options = options;
        _log = log;
        Finished = new FinishedGames(options.LogDirectory);
        Finished.WriteFailed += ex => _log.LogWarning(ex, "Could not write a finished game log");
    }

    /// <summary>Finished games and their omniscient logs. Server-side only.</summary>
    public FinishedGames Finished { get; }

    /// <summary>The current table, if any (for tests and diagnostics; never exposed on the hub).</summary>
    public LiveTable? Current
    {
        get
        {
            lock (_gate)
            {
                return _table;
            }
        }
    }

    public async Task<CreateTableResultDto> CreateAsync(string connectionId, CreateTableRequest? request)
    {
        LiveTable table;
        LiveTable? old;
        ClientUpdatePump? oldPump;
        IDisposable? oldSubscription;
        string? displacedConnectionId = null;
        lock (_gate)
        {
            var unfinished = _table is { } current
                && current.Snapshot.Lifecycle is TableLifecycle.AwaitingSeat or TableLifecycle.InProgress;

            // The owner may not abandon its own live game by creating another one; any other
            // connection (e.g. after a refresh or a network cut) takes over from an unfinished table.
            if (unfinished && _ownerConnectionId == connectionId)
            {
                var rejection = new Rejection(
                    RejectionCategory.TableExists, "TableExists", "A game is already in progress at this server.");
                return new CreateTableResultDto(false, null, rejection.ToDto());
            }

            if (unfinished)
            {
                displacedConnectionId = _ownerConnectionId;
            }

            old = _table;
            oldPump = _pump;
            oldSubscription = _subscription;

            var options = new TableOptions
            {
                Seed = request?.Seed ?? RandomSeed(),
                BotDelay = TimeSpan.FromMilliseconds(request?.BotDelayMs ?? _options.BotDelayMs),
                TimeProvider = _options.TimeProvider,
            };
            table = LiveTable.Create(
                options,
                new ConnectionPrincipal(connectionId),
                onFinished: game =>
                {
                    Finished.Add(game);
                    _log.LogInformation("Table {TableId} finished", game.TableId);
                });
            _pump = new ClientUpdatePump(_hub, connectionId);
            _subscription = table.Subscribe(_pump);
            _table = table;
            _ownerConnectionId = connectionId;
        }

        oldSubscription?.Dispose();
        if (oldPump is not null)
        {
            await oldPump.DisposeAsync().ConfigureAwait(false);
        }

        if (old is not null)
        {
            await old.DisposeAsync().ConfigureAwait(false);
        }

        if (displacedConnectionId is not null)
        {
            try
            {
                await _hub.Clients.Client(displacedConnectionId)
                    .SendAsync(TableClosedMethod, new TableClosedDto(TableUpdateDto.CurrentProtocolVersion, "replaced"))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Could not notify displaced connection {ConnectionId}", displacedConnectionId);
            }
        }

        _log.LogInformation("Table {TableId} created", table.Id);
        return new CreateTableResultDto(true, table.Id, null);
    }

    public LiveTable? TableFor(string connectionId)
    {
        lock (_gate)
        {
            return _ownerConnectionId == connectionId ? _table : null;
        }
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        LiveTable? table;
        lock (_gate)
        {
            if (_ownerConnectionId != connectionId)
            {
                return;
            }

            table = _table;
        }

        if (table is null)
        {
            return;
        }

        try
        {
            await table.DetachAsync(new ConnectionPrincipal(connectionId)).ConfigureAwait(false);
            _log.LogInformation("Table {TableId} paused: client disconnected", table.Id);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static ulong RandomSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
}
