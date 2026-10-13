using Belote.Table;

namespace Belote.Server.Tests;

public class RoomEndToEndTests
{
    private static async Task<(RoomClient Host, RoomClient Guest, string RoomId)> TwoInARoomAsync(
        LiveServer server, int botDelayMs = 0, ulong seed = 11)
    {
        var host = await RoomClient.ConnectAsync(server.HubUrl, "Ivo");
        var created = await host.CreateRoomAsync(seed, botDelayMs);
        Assert.True(created.Ok, created.Rejection?.Code);
        var guest = await RoomClient.ConnectAsync(server.HubUrl, "Mira");
        var joined = await guest.JoinRoomAsync(created.RoomId!);
        Assert.True(joined.Ok, joined.Rejection?.Code);
        var claim = await guest.ClaimSeatAsync("north");
        Assert.True(claim.Ok, claim.Rejection?.Code);
        return (host, guest, created.RoomId!);
    }

    [Fact]
    public async Task CreateRoom_seats_the_host_and_one_human_plays_a_full_game_against_bots()
    {
        await using var server = await LiveServer.StartAsync();
        await using var host = await RoomClient.ConnectAsync(server.HubUrl, "Ivo");

        var created = await host.CreateRoomAsync(seed: 21);
        Assert.True(created.Ok);
        Assert.Equal("lobby", created.State!.Lifecycle);
        Assert.Equal("south", created.State.You.Seat);
        Assert.True(Guid.TryParse(created.RoomId, out _));

        Assert.True((await host.StartGameAsync()).Ok);
        await RoomClient.PlayToEndAsync(new[] { host });

        Assert.True(host.LastUpdate!.Finished);
        Assert.Single(server.Registry.Finished.All);
    }

    [Fact]
    public async Task Two_clients_see_the_same_room_and_a_random_room_id_is_not_found()
    {
        await using var server = await LiveServer.StartAsync();
        var (host, guest, _) = await TwoInARoomAsync(server);
        await using var _h = host;
        await using var _g = guest;

        await RoomClient.WaitForAsync(
            () => host.LastRoom?.Seats.Single(s => s.Seat == "north").Name == "Mira",
            "host sees the guest seated");
        await RoomClient.WaitForAsync(() => guest.LastRoom?.Revision == host.LastRoom?.Revision, "same revision");

        await using var stranger = await RoomClient.ConnectAsync(server.HubUrl, "Pavel");
        var missing = await stranger.JoinRoomAsync(Guid.NewGuid().ToString("D"));
        Assert.False(missing.Ok);
        Assert.Equal("RoomNotFound", missing.Rejection!.Code);
        var garbage = await stranger.JoinRoomAsync("not-a-guid");
        Assert.Equal("RoomNotFound", garbage.Rejection!.Code);
    }

    [Fact]
    public async Task Two_humans_play_a_game_and_never_receive_each_others_cards()
    {
        await using var server = await LiveServer.StartAsync();
        var (host, guest, _) = await TwoInARoomAsync(server);
        await using var _h = host;
        await using var _g = guest;
        Assert.True((await host.StartGameAsync()).Ok);

        await RoomClient.PlayToEndAsync(new[] { host, guest });

        Assert.All(host.Updates, u => Assert.Equal("south", u.View.Seat));
        Assert.All(guest.Updates, u => Assert.Equal("north", u.View.Seat));
        var guestByVersion = guest.Updates.GroupBy(u => u.Version).ToDictionary(g => g.Key, g => g.First());
        foreach (var update in host.Updates)
        {
            if (guestByVersion.TryGetValue(update.Version, out var theirs) && update.View.DealNumber == theirs.View.DealNumber)
            {
                Assert.Empty(update.View.OwnHand.Intersect(theirs.View.OwnHand));
            }

            var json = System.Text.Json.JsonSerializer.Serialize(update, WireJson.Options);
            Assert.DoesNotContain("\"seed\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"deck\"", json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotNull(host.LastRoom);
        Assert.DoesNotContain("seed", System.Text.Json.JsonSerializer.Serialize(host.LastRoom, WireJson.Options),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_reload_returns_the_same_seat_and_hand_and_the_others_see_the_pause_lift()
    {
        await using var server = await LiveServer.StartAsync(o => o.Rooms.GraceSeconds = 30);
        var (host, guest, roomId) = await TwoInARoomAsync(server, botDelayMs: 1000);
        await using var _h = host;
        Assert.True((await host.StartGameAsync()).Ok);
        await RoomClient.WaitForAsync(() => guest.LastUpdate is not null, "guest has a view");
        var handBefore = guest.LastUpdate!.View.OwnHand.ToArray();

        // The guest "reloads": the old connection closes, a brand new one rejoins with the same token.
        await guest.StopAsync();
        await RoomClient.WaitForAsync(() => host.LastRoom?.Missing.Count == 1, "pause with a countdown");
        var missing = host.LastRoom!.Missing.Single();
        Assert.Equal("Mira", missing.Name);

        await using var reloaded = await guest.OpenAnotherTabAsync(server.HubUrl);
        var back = await reloaded.RejoinAsync(roomId);

        Assert.True(back.Ok, back.Rejection?.Code);
        Assert.Equal("north", back.State!.You.Seat);
        Assert.Equal(handBefore, back.Update!.View.OwnHand.ToArray());
        await RoomClient.WaitForAsync(() => host.LastRoom?.Missing.Count == 0, "pause lifted");
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task A_second_tab_takes_over_and_the_first_can_take_it_back()
    {
        await using var server = await LiveServer.StartAsync();
        var (host, guest, roomId) = await TwoInARoomAsync(server);
        await using var _h = host;
        await using var _g = guest;
        Assert.True((await host.StartGameAsync()).Ok);
        await RoomClient.WaitForAsync(() => guest.LastUpdate is not null, "guest has a view");

        await using var second = await guest.OpenAnotherTabAsync(server.HubUrl);
        var taken = await second.RejoinAsync(roomId);
        Assert.True(taken.Ok);
        await RoomClient.WaitForAsync(() => guest.SupersededCount == 1, "first tab told it was superseded");

        var refused = await guest.SubmitAsync(new IntentDto("pass", "north"));
        Assert.False(refused.Accepted);
        Assert.Equal("Superseded", refused.Rejection!.Code);

        var back = await guest.TakeBackAsync();
        Assert.True(back.Ok, back.Rejection?.Code);
        await RoomClient.WaitForAsync(() => second.SupersededCount == 1, "second tab superseded in turn");
    }

    [Fact]
    public async Task A_player_who_stays_away_is_replaced_and_can_reclaim_the_seat()
    {
        await using var server = await LiveServer.StartAsync(o => o.Rooms.GraceSeconds = 1);
        var (host, guest, roomId) = await TwoInARoomAsync(server, botDelayMs: 1000);
        await using var _h = host;
        Assert.True((await host.StartGameAsync()).Ok);
        await RoomClient.WaitForAsync(() => guest.LastUpdate is not null, "guest has a view");

        await guest.StopAsync();
        await RoomClient.WaitForAsync(
            () => host.LastRoom?.Seats.Single(s => s.Seat == "north").Origin == "replaced",
            "a bot replaced the guest",
            timeoutMs: 15_000);

        await using var returned = await guest.OpenAnotherTabAsync(server.HubUrl);
        var back = await returned.RejoinAsync(roomId);
        Assert.True(back.Ok);
        Assert.NotNull(back.AwayMoves);
        Assert.Equal("human", back.State!.Seats.Single(s => s.Seat == "north").Kind);
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task Rooms_are_independent_and_run_side_by_side()
    {
        await using var server = await LiveServer.StartAsync();
        var rooms = new List<(RoomClient Host, RoomClient Guest)>();
        for (int i = 0; i < 3; i++)
        {
            var (host, guest, _) = await TwoInARoomAsync(server, seed: (ulong)(30 + i));
            Assert.True((await host.StartGameAsync()).Ok);
            rooms.Add((host, guest));
        }

        await Task.WhenAll(rooms.Select(r => RoomClient.PlayToEndAsync(new[] { r.Host, r.Guest })));

        Assert.Equal(3, server.Registry.Finished.All.Count);
        foreach (var (host, guest) in rooms)
        {
            Assert.Equal(host.RoomId, host.LastRoom!.RoomId);
            Assert.Equal(guest.RoomId, guest.LastRoom!.RoomId);
            await host.DisposeAsync();
            await guest.DisposeAsync();
        }

        Assert.Equal(3, rooms.Select(r => r.Host.RoomId).Distinct().Count());
    }
}
