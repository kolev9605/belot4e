using System;
using Belote.Core;

namespace Belote.Table;

/// <summary>
/// Identity of whoever submits an intent or owns a seat. The table only compares principals for
/// equality; it never inspects what kind of controller stands behind one.
/// </summary>
public abstract record Principal;

/// <summary>A network connection (the remote client).</summary>
public sealed record ConnectionPrincipal(string ConnectionId) : Principal;

/// <summary>The in-process controller of a bot seat.</summary>
public sealed record BotPrincipal(Seat Seat) : Principal;

/// <summary>A request to perform <paramref name="Action"/> on behalf of <paramref name="Seat"/>.</summary>
public sealed record Intent(Seat Seat, PlayerAction Action, Principal Sender);

/// <summary>A human participant of a room, identified by the room's participant id (never a connection).</summary>
public sealed record HumanPrincipal(Guid ParticipantId) : Principal;
