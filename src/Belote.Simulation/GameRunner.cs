using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Simulation;

public enum FailureKind
{
    RuleViolationDetected,
    AuditMismatch,
    ControllerException,
    ControllerTimeout,
    TooManyIllegalAttempts,
    NoWinner,
    ExceededHandLimit,
    ReplayMismatch,
}

/// <summary>A seat controller together with the name recorded in the log header.</summary>
public sealed record SeatController(string Name, IPlayer Player);

public sealed record GameRunOptions
{
    /// <summary>Illegal attempts allowed per decision before the game fails.</summary>
    public int MaxIllegalAttempts { get; init; } = 3;

    /// <summary>Runner watchdog per decision (a safety net, not a turn timer).</summary>
    public TimeSpan DecisionTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Safety valve: the game fails if more deals than this are needed.</summary>
    public int MaxDeals { get; init; } = 1000;
}

public sealed record GameRunResult(
    bool Finished,
    Team? Winner,
    int NorthSouth,
    int EastWest,
    int Deals,
    int HandsScored,
    GameLog Log,
    FailureKind? Failure,
    string? FailureDetail);

/// <summary>Plays one complete game by asking each seat's controller for actions.</summary>
public static class GameRunner
{
    public static async ValueTask<GameRunResult> RunAsync(
        RuleSet rules,
        ulong seed,
        IReadOnlyDictionary<Seat, SeatController> seats,
        GameRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GameRunOptions();
        var names = seats.ToDictionary(kv => kv.Key, kv => kv.Value.Name);
        var start = GameEngine.Start(rules, seed);
        var state = start.State;
        var events = new List<GameEvent>(start.Events);
        var rejections = new List<RejectionRecord>();

        GameRunResult Fail(FailureKind kind, string detail) => new(
            false,
            null,
            state.MatchScore[0],
            state.MatchScore[1],
            state.DealNumber,
            state.HandsScored,
            new GameLog(seed, rules, names, events, rejections),
            kind,
            detail);

        while (state.Phase != Phase.Finished)
        {
            if (state.DealNumber > options.MaxDeals)
            {
                return Fail(FailureKind.ExceededHandLimit, $"More than {options.MaxDeals} deals were needed.");
            }

            var seat = state.ToAct;
            var controller = seats[seat];
            var view = Projection.For(state, seat);
            int attempts = 0;

            while (true)
            {
                PlayerAction action;
                try
                {
                    action = await AskAsync(controller.Player, view, options, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException ex)
                {
                    return Fail(FailureKind.ControllerTimeout, $"{seat} ({controller.Name}): {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return Fail(FailureKind.ControllerException, $"{seat} ({controller.Name}): {ex.GetType().Name}: {ex.Message}");
                }

                var result = GameEngine.Apply(state, seat, action);
                if (result is Accepted accepted)
                {
                    state = accepted.State;
                    events.AddRange(accepted.Events);
                    break;
                }

                var rejected = (Rejected)result;
                rejections.Add(new RejectionRecord(state.NextSequence - 1, seat, action, rejected.Reason));
                attempts++;
                if (attempts >= options.MaxIllegalAttempts)
                {
                    return Fail(
                        FailureKind.TooManyIllegalAttempts,
                        $"{seat} ({controller.Name}) made {attempts} illegal attempts in a row; last: {rejected.Detail}");
                }
            }
        }

        var log = new GameLog(seed, rules, names, events, rejections);
        return new GameRunResult(
            true,
            state.Winner,
            state.MatchScore[0],
            state.MatchScore[1],
            state.DealNumber,
            state.HandsScored,
            log,
            null,
            null);
    }

    private static async ValueTask<PlayerAction> AskAsync(
        IPlayer player,
        SeatView view,
        GameRunOptions options,
        CancellationToken cancellationToken)
    {
        var pending = player.DecideAsync(view, cancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
            return pending.Result;
        }

        var task = pending.AsTask();
        var winner = await Task.WhenAny(task, Task.Delay(options.DecisionTimeout, cancellationToken))
            .ConfigureAwait(false);
        if (winner != task)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"No decision within {options.DecisionTimeout.TotalMilliseconds:0} ms.");
        }

        return await task.ConfigureAwait(false);
    }
}
