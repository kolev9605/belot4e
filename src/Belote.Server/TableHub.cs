using System;
using System.Threading.Tasks;
using Belote.Table;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server;

/// <summary>
/// The persistent-connection surface of the live table. It parses and forwards intents only; every
/// decision (ownership, turn, legality) is made by <see cref="LiveTable"/> and the rules engine.
/// </summary>
public sealed class TableHub : Hub
{
    public const string Path = "/table";

    private readonly TableRegistry _registry;

    public TableHub(TableRegistry registry)
    {
        _registry = registry;
    }

    public Task<CreateTableResultDto> CreateTable(CreateTableRequest? request) =>
        _registry.CreateAsync(Context.ConnectionId, request);

    public async Task<TakeSeatResultDto> TakeSeat(TakeSeatRequest? request)
    {
        var table = _registry.TableFor(Context.ConnectionId);
        if (table is null)
        {
            return new TakeSeatResultDto(false, null, NoTable().ToDto());
        }

        if (!IntentParser.TryParseSeat(request?.Seat, out var seat))
        {
            return new TakeSeatResultDto(
                false, null, Rejection.Malformed($"Unknown or missing seat [{request?.Seat}].").ToDto());
        }

        if (seat != LiveTable.ClientSeat)
        {
            return new TakeSeatResultDto(false, null, Rejection.NotOwner(seat).ToDto());
        }

        var outcome = await table.TakeSeatAsync(new ConnectionPrincipal(Context.ConnectionId)).ConfigureAwait(false);
        return outcome.Ok
            ? new TakeSeatResultDto(true, outcome.Version, null)
            : new TakeSeatResultDto(false, null, outcome.Rejection!.ToDto());
    }

    public async Task<IntentResultDto> SubmitIntent(IntentDto? intent)
    {
        var table = _registry.TableFor(Context.ConnectionId);
        if (table is null)
        {
            return new IntentResultDto(false, null, NoTable().ToDto());
        }

        var outcome = await table.SubmitAsync(new ConnectionPrincipal(Context.ConnectionId), intent!)
            .ConfigureAwait(false);
        return outcome switch
        {
            IntentAccepted a => new IntentResultDto(true, a.Version, null),
            IntentRejected r => new IntentResultDto(false, null, r.Rejection.ToDto()),
            _ => throw new InvalidOperationException("Unknown outcome."),
        };
    }

    public GetViewResultDto GetView()
    {
        var table = _registry.TableFor(Context.ConnectionId);
        if (table is null)
        {
            return new GetViewResultDto(null, NoTable().ToDto());
        }

        var view = table.GetView(new ConnectionPrincipal(Context.ConnectionId));
        return new GetViewResultDto(view.Update, view.Rejection?.ToDto());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _registry.DisconnectedAsync(Context.ConnectionId).ConfigureAwait(false);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    private static Rejection NoTable() =>
        Rejection.NotSeated("This connection has not created a table.");
}
