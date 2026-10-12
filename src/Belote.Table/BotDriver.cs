using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Table;

/// <summary>
/// Plays the bot seats. Each move is chosen from the seat's own <see cref="SeatView"/> and enters the
/// table through <see cref="LiveTable.SubmitAsync(Intent)"/>, exactly like a human seat's intent.
/// </summary>
internal sealed class BotDriver
{
    private static readonly Seat[] Seats = { Seat.North, Seat.East, Seat.West };

    private readonly LiveTable _table;
    private readonly TableOptions _options;
    private readonly Dictionary<Seat, IPlayer> _bots = new();
    private readonly CancellationToken _stop;

    public BotDriver(LiveTable table, TableOptions options, Func<Seat, IPlayer> botFactory, CancellationToken stop)
    {
        _table = table;
        _options = options;
        _stop = stop;
        foreach (var seat in Seats)
        {
            _bots[seat] = botFactory(seat);
        }
    }

    public async Task RunAsync()
    {
        (int Version, PlayerAction Action)? pending = null;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                // Read the change signal before the snapshot so a change can never be missed.
                var changed = _table.ChangedTask;
                var snap = _table.Snapshot;
                if (snap.Lifecycle is TableLifecycle.Finished or TableLifecycle.Faulted)
                {
                    return;
                }

                if (snap.Lifecycle == TableLifecycle.InProgress
                    && !snap.Paused
                    && _bots.TryGetValue(snap.State!.ToAct, out var bot))
                {
                    var seat = snap.State.ToAct;
                    if (_options.BotDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(_options.BotDelay, _options.TimeProvider, _stop).ConfigureAwait(false);
                        var after = _table.Snapshot;
                        if (after.Version != snap.Version || after.Paused || after.Lifecycle != TableLifecycle.InProgress)
                        {
                            continue;
                        }
                    }

                    // A decision is made once per state and kept if the submit is deferred by a pause,
                    // so a pause never consumes a bot's random draw.
                    if (pending is not { } p || p.Version != snap.Version)
                    {
                        var view = Projection.For(snap.State, seat);
                        var action = await bot.DecideAsync(view, _stop).ConfigureAwait(false);
                        pending = (snap.Version, action);
                    }

                    var outcome = await _table.SubmitAsync(new Intent(seat, pending.Value.Action, new BotPrincipal(seat)))
                        .ConfigureAwait(false);
                    if (outcome is IntentRejected { Rejection.Category: RejectionCategory.Paused })
                    {
                        continue;
                    }

                    if (outcome is IntentRejected rejected)
                    {
                        await _table.ReportFaultAsync(
                            $"{seat} bot move was rejected: {rejected.Rejection.Code}: {rejected.Rejection.Detail}")
                            .ConfigureAwait(false);
                        return;
                    }

                    pending = null;
                    continue;
                }

                await changed.WaitAsync(_stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            try
            {
                await _table.ReportFaultAsync($"Bot driver failed: {ex.GetType().Name}: {ex.Message}")
                    .ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
