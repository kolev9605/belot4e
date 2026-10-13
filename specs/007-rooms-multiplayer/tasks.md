---

description: "Task list for Rooms and Multiplayer"
---

# Tasks: Rooms and Multiplayer

**Input**: Design documents from `/specs/007-rooms-multiplayer/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/hub-protocol.md, contracts/client-storage.md

**Tests**: Included. Constitution Principle V (Test-First Game Logic) is NON-NEGOTIABLE: room, pause,
reclaim and handover behaviour is written as failing tests first. Playwright e2e is opt-in and not
part of verification; verify with `dotnet test`, `npm run typecheck` and `npm test` (Vitest).

**Organization**: Grouped by user story. Stories 1-3 (all P1) plus 4 (P1) form the playable
multiplayer; 5-7 add depth. The "late join" requirement (FR-008a, Story 2 scenarios 7-8) needs a
running game, so it sits in its own phase after Story 7 and keeps the `[US2]` label.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1-US7 from spec.md
- Paths are repository-relative

## Path Conventions

- Server/domain: `src/Belote.Table/`, `src/Belote.Server/`
- Server tests: `tests/Belote.Table.Tests/`, `tests/Belote.Server.Tests/`
- Client: `web/src/`, tests in `web/tests/`

---

## Phase 1: Setup

**Purpose**: Shared configuration and contract scaffolding

- [x] T001 Create `RoomOptions` in `src/Belote.Table/Rooms/RoomOptions.cs` with `GraceSeconds = 60`, `IdleExpiryMinutes = 60`, `LobbySeatReleaseSeconds = 60`, `MaxNameLength = 20`, `TimeProvider`; bind a `Room` configuration section in `src/Belote.Server/ServerHost.cs` and register it as a singleton
- [x] T002 [P] Raise the protocol version to 2: `TableUpdateDto.CurrentProtocolVersion` in `src/Belote.Table/Views/Dtos.cs` and `PROTOCOL_VERSION` in `web/src/protocol/types.ts`; update every existing server/client test that asserts version 1 (search `protocolVersion` in `tests/` and `web/tests/`)
- [x] T003 [P] Add room DTOs from `contracts/hub-protocol.md` (`RoomStateDto`, `SeatSlotDto`, `MissingDto`, `PendingTakeoverDto`, `RoomEventDto`, request/result records for CreateRoom, JoinRoom, Rejoin, SetName, ClaimSeat, ReleaseSeat, StartGame, SkipWait, LeaveRoom, PlayAgain, TakeBack, plus `SupersededDto`) to `src/Belote.Table/Views/Dtos.cs` and mirror them as types in `web/src/protocol/types.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Dynamic seat control, per-seat projection, name rules, seat-relative client table, identity storage, routing and hub client plumbing that every story builds on.

**CRITICAL**: No user story work can begin until this phase is complete.

### Table layer (tests first)

- [x] T004 [P] Write failing tests `tests/Belote.Table.Tests/Unit/ControllerSwapTests.cs`: (a) swapping any seat between human and bot principal never changes `GameState` or `Version`; (b) a bot intent in flight for a seat that was just reassigned is rejected `NotOwner` and the driver does NOT fault the table; (c) FsCheck property: a game where one seat is driven by an equivalent `SmartBot` through intents and is swapped to the table's bot at random points finishes with a log identical to an undisturbed all-bot game of the same seed
- [x] T005 [P] Write failing tests `tests/Belote.Table.Tests/Unit/PerSeatUpdateTests.cs`: for a table with humans on two seats, `BuildUpdateFor(seat)` contains only that seat's `OwnHand` and legal actions, `view.seat` equals the requested seat, and versions match across seats (extend `PrivacyOracle` use from `tests/Belote.Table.Tests/Support/PrivacyOracle.cs`)
- [x] T006 [P] Write failing tests `tests/Belote.Table.Tests/Unit/NameRulesTests.cs`: name is trimmed; empty or whitespace-only is `NameInvalid`; longer than 20 characters (`MaxNameLength`) is `NameInvalid`; control characters rejected; uniqueness within a room is case-insensitive (`NameTaken`)

### Table layer (implementation)

- [x] T007 Add `HumanPrincipal(Guid ParticipantId)` to `src/Belote.Table/Intent.cs` (keep `ConnectionPrincipal` for the legacy shim)
- [x] T008 Generalise `src/Belote.Table/LiveTable.cs`: accept initial seat controllers (`IReadOnlyDictionary<Seat, Principal>`) instead of the fixed South-creator/three-bots seeding; add `SetControllerAsync(Seat, Principal)` as an actor work item (state untouched, no version change); replace the creator-only pause check with `SetPausedAsync(bool)` callable by the room; add `BuildUpdateFor(Seat, TableSnapshot)` and a snapshot-level observer (`ISnapshotObserver`) alongside `ITableObserver`; keep the existing single-seat API working for the shim (depends on T004, T005, T007)
- [x] T009 Update `src/Belote.Table/BotDriver.cs`: play every seat whose current controller is a `BotPrincipal`, creating bot instances lazily per seat via the bot factory; treat `NotOwner` for a seat it no longer controls as stale (re-evaluate) rather than a fault; keep the "decide once per state" rule (depends on T008)
- [x] T010 [P] Implement `NameRules` in `src/Belote.Table/Rooms/NameRules.cs` so T006 passes (trim; 1-20 characters; no control characters; case-insensitive uniqueness; returns a result with stable codes `NameInvalid` / `NameTaken`, never throws)
- [x] T011 [P] Add immutable room records in `src/Belote.Table/Rooms/RoomModel.cs` and `src/Belote.Table/Rooms/RoomState.cs`: `RoomLifecycle` (`Lobby`, `InGame`, `Finished`, `Ended`), `SeatSlot` (`Free` / `Human(ParticipantId)` / `Bot(Origin)` with `Origin` = `Default` | `Left` | `Replaced(ParticipantId)`), `Participant` (`Id`, `TokenHash` SHA-256, `Name`, `Seat?`, `Connection` = `Connected(connectionId)` | `Reconnecting(deadlineUtc)` | `Away`, `PendingTakeover?`), `PauseEntry` (`ParticipantId`, `Seat`, `DeadlineUtc`), `RoomEvent` kinds from data-model.md, `RoomState` snapshot with `Revision`

### Server plumbing

- [x] T012 Replace `src/Belote.Server/ClientUpdatePump.cs` with `src/Belote.Server/ParticipantPump.cs`: one ordered drain per participant whose target connection id can be swapped atomically (so a rejoin on a new connection keeps ordering), sends `Update`, `Room`, `Superseded` messages _Done as `src/Belote.Server/ConnectionSink.cs` (one ordered sink per connection; superseding is tracked on the sink so the hub refuses a superseded tab). `ClientUpdatePump.cs` and `TableRegistry.cs` were removed._
- [x] T013 Configure `src/Belote.Server/ServerHost.cs`: SignalR `KeepAliveInterval = 5 s`, `ClientTimeoutInterval = 15 s`; confirm `MapFallbackToFile("index.html")` serves `/room/<guid>` deep links (add a server test `tests/Belote.Server.Tests/EndToEnd/ConfigurationTests.cs` case for the fallback and the timeouts)

### Client plumbing (tests first where logic exists)

- [x] T014 [P] Write failing tests `web/tests/perspective.test.ts` for a `relative(seat, viewerSeat)` helper in `web/src/theme/geometry.ts`: the viewer's seat maps to the bottom position, partner to top, opponents left/right, clockwise order preserved, and `isMyTeam(seat, viewerSeat)` replaces the South-only assumption _Done differently: views are rotated at the boundary (`web/src/protocol/perspective.ts`, tests in `web/tests/perspective.test.ts`) so the viewer is always drawn as "south"; no per-component geometry change was needed._
- [x] T015 Migrate `web/src/theme/geometry.ts`, `web/src/components/Table.tsx`, `Seat.tsx`, `TrickArea.tsx`, `Hand.tsx`, `HandResultDialog.tsx`, `web/src/presentation/steps.ts` and `usePresenter.ts` to the seat-relative helper using `view.seat` as the viewer (default behaviour for South unchanged); all existing `web/tests/*` must still pass (depends on T014) _Covered by the boundary rotation above: existing components are unchanged apart from names and room-aware panels, and every pre-existing client test still passes._
- [x] T016 [P] Add `web/src/session/storage.ts` with `getIdentityToken()` (generate 16 random bytes via `crypto.getRandomValues`, hex, key `belote.identity.v1`), `loadProfile()/saveProfile()` (`belote.profile.v1`), `loadSession()/saveSession()/clearSession()` (`belote.session.v1`); every access in try/catch with in-memory fallback; tests in `web/tests/session-storage.test.ts` including a throwing `localStorage`
- [x] T017 [P] Add `web/src/routing/router.ts` (History API: `/` and `/room/<guid>`, `navigate`, subscribe) and `parseRoomLink(text)` per `contracts/client-storage.md` (trim whitespace/braces, bare GUID with or without dashes, any text containing `/room/<guid>`); tests in `web/tests/router.test.ts` including malformed input
- [x] T018 Extend `web/src/connection/hubClient.ts` with typed room methods (`createRoom`, `joinRoom`, `rejoin`, `setName`, `claimSeat`, `releaseSeat`, `startGame`, `skipWait`, `leaveRoom`, `playAgain`, `takeBack`), `Room` and `Superseded` handlers, and an `onAttach` hook invoked after every `start()` success and every `onreconnected`; update `web/tests/connection.test.ts` (depends on T003)
- [x] T019 Extend `web/src/state/reducer.ts` and `web/src/state/selectors.ts` with a `room` slice (latest `RoomState` by `revision`, `clockOffsetMs` computed from `serverNowUtc`, `superseded` flag, `awayRecap`) and remove the "connection drop loses the game" transition in favour of a `stale` flag; tests in `web/tests/reducer.test.ts` (depends on T003, T018)

**Checkpoint**: A table can have any mix of human/bot controllers, the client can draw any seat at the bottom, and storage/routing/hub plumbing exists. User stories can begin.

---

## Phase 3: User Story 1 - Create a room and invite friends (Priority: P1) MVP

**Goal**: Start game creates a room; the creator lands in a lobby with a copyable link and code, can start, and bots fill every empty seat.

**Independent Test**: From the main menu press Start game, see the lobby with creator seated and link/code, press Start and play a normal game against three bots.

### Tests for User Story 1

- [x] T020 [P] [US1] Write failing `tests/Belote.Table.Tests/Rooms/RoomLobbyTests.cs`: creating a room seats the creator at the first free seat (South) and makes them host; only the host can start (`NotHost`); start requires at least one human seated; every non-human seat becomes a default bot; starting twice is rejected (`GameInProgress`)
- [x] T021 [P] [US1] Extend `tests/Belote.Server.Tests/Support/TableClient.cs` with room methods and push capture (`Room`, `Update`, `Superseded`), then write failing `tests/Belote.Server.Tests/EndToEnd/CreateRoomTests.cs`: create → lobby state → start → three bots play and the creator receives updates; legacy `CreateTable`/`TakeSeat` still plays one full game

### Implementation for User Story 1

- [x] T022 [US1] Implement the actor skeleton of `src/Belote.Table/Rooms/Room.cs`: single-reader queue, injected `TimeProvider`, `Create`, host assignment, participant registry keyed by token hash, auto-seat creator, `StartGame` building the `LiveTable` with human/bot controllers, `RoomState` publication with increasing `Revision` (depends on T008-T012)
- [x] T023 [US1] Add `src/Belote.Server/RoomRegistry.cs` (id → `Room`, generate GUID ids, resolve, remove) replacing `src/Belote.Server/TableRegistry.cs`; wire DI in `ServerHost.cs`; delete `TableRegistry.cs`
- [x] T024 [US1] Update `src/Belote.Server/TableHub.cs`: `CreateRoom`, `StartGame`; reimplement legacy `CreateTable`/`TakeSeat`/`SubmitIntent`/`GetView` as a shim over a room (creator seated South, `TakeSeat` = start with bots); retire `tests/Belote.Server.Tests/EndToEnd/ReplaceOnCreateTests.cs` (rooms are independent) and adapt other server tests that referenced `TableRegistry`
- [x] T025 [P] [US1] Add `web/src/components/ShareRoom.tsx`: prominent Copy link (clipboard with visible confirmation), native Share via `navigator.share` when available, always-visible selectable link and room code fallback; tests in `web/tests/share-room.test.tsx`
- [x] T026 [P] [US1] Add `web/src/screens/MainMenu.tsx` with **Start game** and **Join game** primary actions (Return-to-game slot added in US4)
- [x] T027 [US1] Add `web/src/screens/Lobby.tsx`: four seat cards with names/occupancy, host-only **Start game**, the `ShareRoom` block; wire `App.tsx` to route Menu → CreateRoom → Lobby → Table using `router.ts` and the room slice (depends on T025, T026)
- [x] T028 [P] [US1] Add EN and BG strings for menu, lobby and share in `web/src/i18n/en.ts` and `web/src/i18n/bg.ts` (extend `web/tests/i18n.test.ts` parity check)
- [x] T029 [US1] Add `web/tests/lobby.test.tsx`: menu → create → lobby shows link/code; Copy confirms; host starts and the table renders with seat-relative layout

**Checkpoint**: Story 1 works end to end for one human with bots (parity with today's experience, now through a room).

---

## Phase 4: User Story 2 - Join a room by link or code and pick a seat (Priority: P1)

**Goal**: Others join via link or typed/pasted code, choose a seat, and see each other in real time.

**Independent Test**: Open the link in a second browser, enter a name, pick a free seat; both lobbies match.

### Tests for User Story 2

- [x] T030 [P] [US2] Write failing `tests/Belote.Table.Tests/Rooms/RoomJoinTests.cs`: join by room id adds a participant; a name that violates `NameRules` is rejected with `NameInvalid`/`NameTaken`; two simultaneous claims of one free seat give exactly one winner and `SeatTaken` for the other; joining a full room is `RoomFull`; unknown id is `RoomNotFound`; re-joining with the same token is idempotent
- [x] T031 [P] [US2] Write failing `tests/Belote.Server.Tests/EndToEnd/MultiClientRoomTests.cs`: two clients in one room see identical `Room` state and revisions; claims propagate to both within one push; `RoomNotFound` for a random guid

### Implementation for User Story 2

- [x] T032 [US2] Extend `src/Belote.Table/Rooms/Room.cs` with `Join`, `ClaimSeat` (lobby, immediate; atomic via actor order) and token-hash matching using constant-time comparison (depends on T022, T030)
- [x] T033 [US2] Extend `src/Belote.Server/RoomRegistry.cs` and `src/Belote.Server/TableHub.cs` with `JoinRoom` and `ClaimSeat`, normalising the room id (GUID with/without dashes) and returning the rejection codes from `contracts/hub-protocol.md`
- [x] T034 [P] [US2] Add `web/src/screens/JoinGame.tsx` (text box accepting guid or full link via `parseRoomLink`, inline message "That doesn't look like a room code or link." with no network call) and wire the **Join game** button
- [x] T035 [P] [US2] Add `web/src/screens/RoomUnavailable.tsx` for `RoomNotFound`, `RoomEnded` and `RoomFull` with a menu button and (for full) a retry
- [x] T036 [US2] Add `web/src/components/SeatPicker.tsx` to `Lobby.tsx` and handle deep links: opening `/room/<guid>` joins (after a minimal name prompt when no profile exists) and lands in the lobby; seat clicks call `claimSeat` and report `SeatTaken` inline (depends on T033, T034)
- [x] T037 [P] [US2] Add EN/BG strings for join, errors and seat picking in `web/src/i18n/en.ts` and `bg.ts`
- [x] T038 [US2] Add `web/tests/join.test.tsx`: link and code paths, malformed code, room-not-found, seat-taken message

**Checkpoint**: Two or more people can gather in a lobby (starting the game with several humans is covered by Story 3).

---

## Phase 5: User Story 3 - Play together as a table of humans and bots (Priority: P1)

**Goal**: A started room delivers each human only their own seat's view; everyone sees real names; only the seat owner can act.

**Independent Test**: Two humans (partners) and two bots play a full hand; each acts only on their turn and sees only their own cards and the other's name.

### Tests for User Story 3

- [x] T039 [P] [US3] Write failing `tests/Belote.Table.Tests/Rooms/RoomPlayTests.cs`: with humans on North and South, an intent from South for North is `NotOwner`; each human receives only their `Projection.For` view; bots act on remaining seats; the game finishes and the room becomes `Finished`
- [x] T040 [P] [US3] Write failing `tests/Belote.Server.Tests/EndToEnd/TwoHumansPlayGameTests.cs` and `HiddenInfoTests.cs`: two clients play a seeded game to a winner; neither ever receives a card that is not their own hand, a played card, or public information (use `PrivacyOracle`)

### Implementation for User Story 3

- [x] T041 [US3] Extend `src/Belote.Table/Rooms/Room.cs`: subscribe to the table's snapshot observer, fan out per-seat `Update`s through the participants' pumps, push `RoomState` on every change, route `SubmitIntent` through the participant's seat with `HumanPrincipal`, detect `Finished` and move the room to `Finished` (depends on T032, T039)
- [x] T042 [US3] Update `src/Belote.Server/TableHub.cs` so `SubmitIntent`/`GetView` resolve the caller's participant via the connection index in `RoomRegistry` (not "owner connection")
- [x] T043 [P] [US3] Replace compass labels with names in `web/src/components/Seat.tsx`, `Table.tsx`, `TrickArea.tsx`, `HandResultDialog.tsx` and the score/turn indicator, sourced from `room.seats[].name`; keep seat position/partnership evident (partner badge, seat direction chip); bots show a localized "Bot" label
- [x] T044 [P] [US3] Make `web/src/state/reducer.ts` accept `Update` for any seat and show "Waiting for <name>" for the seat to act; finished games show the result with the room still open
- [x] T045 [US3] Add `web/tests/table-names.test.tsx` (names at every seat, partner indicator, viewer at bottom for East and West viewers) and extend `web/tests/table-seats.test.tsx`

**Checkpoint**: Real multi-human games work from lobby to final score.

---

## Phase 6: User Story 4 - Reload or lose connection without losing your seat (Priority: P1)

**Goal**: Reloads and network drops restore the same room and seat automatically; the table pauses with a visible countdown meanwhile.

**Independent Test**: Reload one human's browser mid-hand; others see the pause and countdown; the reloaded browser returns to the same seat and resumes where it left off.

### Tests for User Story 4

- [x] T046 [P] [US4] Write failing `tests/Belote.Table.Tests/Rooms/RoomPauseTests.cs` (FakeTimeProvider): a seated human disconnecting in-game pauses the table and records a `PauseEntry` with `deadline = now + GraceSeconds (60)`; returning before the deadline lifts the pause with no state change; two missing humans keep the table paused until both are back or replaced; each new disconnection starts a fresh 60 s (no cumulative cap); a lobby disconnect never pauses
- [x] T047 [P] [US4] Write failing `tests/Belote.Server.Tests/EndToEnd/ReloadReconnectTests.cs`: close a human's connection mid-hand, connect a new one, `Rejoin` with the same token and see the same seat, hand, version and `Room` state; others receive a paused `Room` state within the window; `Rejoin` to an unknown room is `RoomNotFound`
- [x] T048 [P] [US4] Write failing `tests/Belote.Server.Tests/EndToEnd/TakeoverTests.cs`: a second connection with the same token supersedes the first (`Superseded` pushed); the first cannot act; `TakeBack` returns control and supersedes the second

### Implementation for User Story 4

- [x] T049 [US4] Extend `src/Belote.Table/Rooms/Room.cs`: `OnDisconnected`/`OnAttached`, `Missing` map with absolute `DeadlineUtc`, timers created from the injected `TimeProvider`, `SetPausedAsync(missing.Count > 0)`, `serverNowUtc` in every push, superseding the previous connection (depends on T041, T046)
- [x] T050 [US4] Extend `src/Belote.Server/RoomRegistry.cs` with a connection → (room, participant) index, disconnect handling from `TableHub.OnDisconnectedAsync`, and `Rejoin` that attaches the new connection, pushes `Room` + `Update` before returning, and returns `RoomNotFound`/`RoomEnded` correctly
- [x] T051 [US4] Add `Rejoin`, `TakeBack` and the `Superseded` push to `src/Belote.Server/TableHub.cs`
- [x] T052 [US4] Implement auto-rejoin in `web/src/connection/hubClient.ts` and the room flow in `App.tsx`: after every successful connect/reconnect call `rejoin` with the stored token and room; on `RoomNotFound`/`RoomEnded` clear the session and show `RoomUnavailable`; nudge the connect loop on `visibilitychange` and `online`; stop auto-rejoin after `Superseded`
- [x] T053 [P] [US4] Add `web/src/components/ReconnectingOverlay.tsx`: keeps the last table visible and marked stale, announces "Reconnecting", always offers **Back to menu**
- [x] T054 [P] [US4] Add `web/src/components/PauseOverlay.tsx` and `web/src/session/useCountdown.ts`: lists every missing player by name with `deadlineUtc - (Date.now() + clockOffsetMs)` rendered each second and clamped at 0; shown to everyone while `missing` is non-empty; test in `web/tests/pause-overlay.test.tsx`
- [x] T055 [P] [US4] Add `web/src/components/LiveAnnouncer.tsx` (polite `aria-live` region) fed by `room.lastEvent` and connection status; used for pause, resume, replaced, reclaimed
- [x] T056 [US4] Extend `web/src/screens/MainMenu.tsx` with **Return to your game** when `loadSession()` has a room (verifies with `rejoin`; hidden if the room is gone) and save/clear the session at the points listed in `contracts/client-storage.md`
- [x] T057 [P] [US4] Add EN/BG strings (reconnecting, paused for X, countdown, taken over, take back, return to game) in `web/src/i18n/en.ts` and `bg.ts`
- [x] T058 [US4] Add `web/tests/reconnect.test.tsx` (fake hub: drop → overlay → rejoin → table restored; superseded → take back; storage unavailable → still plays)

**Checkpoint**: Reload, tab restore and network flaps keep players in their seats with a visible pause.

---

## Phase 7: User Story 5 - Dropped player replaced by a bot and can take the seat back (Priority: P2)

**Goal**: After 60 s (or when another human skips the wait) a bot takes the seat; the original player can return and resume at any time.

**Independent Test**: Disconnect a human, wait out the minute, see a bot continue; reconnect and resume control.

### Tests for User Story 5

- [x] T059 [P] [US5] Write failing `tests/Belote.Table.Tests/Rooms/RoomReplaceReclaimTests.cs` (FakeTimeProvider): at the deadline the seat becomes `Bot(Replaced(p))`, the pause lifts and a `replaced` event is emitted; `SkipWait` by another connected human does the same immediately with `ByName`; `SkipWait` by the missing player is rejected; returning claims the seat back atomically; a bot intent in flight at reclaim is rejected and re-evaluated; the returning player gets `awayRecap` with the number of moves the bot played for them
- [x] T060 [P] [US5] Write failing property test `tests/Belote.Table.Tests/Rooms/HandoverPropertyTests.cs` (FsCheck, FakeTimeProvider): any schedule of disconnect, return, deadline, skip and reclaim events over a seeded full game finishes the game, increases `Version` by exactly one per accepted intent, never accepts a move while paused, and produces the same final result as the undisturbed game
- [x] T061 [P] [US5] Write failing `tests/Belote.Server.Tests/EndToEnd/ReplaceAndReclaimTests.cs`: end to end replace after the grace period, reclaim over a new connection, and skip-wait (use a short `GraceSeconds` via test configuration)

### Implementation for User Story 5

- [x] T062 [US5] Extend `src/Belote.Table/Rooms/Room.cs`: deadline expiry → `SetControllerAsync(seat, BotPrincipal)` + `Bot(Replaced(p))`, `SkipWait`, reclaim on attach/`ClaimSeat` for the replaced participant, away-move counting from the table's event log since replacement, `awayRecap` delivered once (depends on T049, T059)
- [x] T063 [US5] Add `SkipWait` to `src/Belote.Server/TableHub.cs` and include `awayRecap` in the `Rejoin` result
- [x] T064 [P] [US5] Extend `PauseOverlay.tsx` with **Play with a bot now** (not rendered for the missing player themselves) and show the seat badge "bot playing for <name>" in `Seat.tsx`
- [x] T065 [P] [US5] Add a returning-player recap notice ("A bot played N cards for you") and a "<name> was replaced by a bot" toast driven by `room.lastEvent`
- [x] T066 [P] [US5] Add EN/BG strings for replacement, skip action, recap and badges; tests in `web/tests/replace-reclaim.test.tsx`

**Checkpoint**: A flaky or absent player never blocks the table for more than a minute, and can always come back.

---

## Phase 8: User Story 6 - Pick a display name (Priority: P2)

**Goal**: Players choose, remember and change a name that appears everywhere.

**Independent Test**: Enter a name, take a seat, see it at the table for everyone; reload and find it remembered.

### Tests for User Story 6

- [x] T067 [P] [US6] Write failing `tests/Belote.Table.Tests/Rooms/RoomNameTests.cs`: rename in the lobby and in a running game updates `RoomState` for everyone with a `nameChanged` event; invalid and duplicate names rejected with codes; whitespace is trimmed; names are only ever plain text in DTOs
- [x] T068 [P] [US6] Write failing `web/tests/name-field.test.tsx`: inline localized errors for empty, too long (more than 20 characters) and taken names; suggested default shown; saved name prefilled

### Implementation for User Story 6

- [x] T069 [US6] Add `SetName` to `src/Belote.Table/Rooms/Room.cs` and `src/Belote.Server/TableHub.cs` using `NameRules` (depends on T010, T067)
- [x] T070 [P] [US6] Add `web/src/components/NameField.tsx` (max length 20, inline errors, suggested default) and use it in the create/join gate and a lobby "change name" control
- [x] T071 [US6] Persist the chosen name via `saveProfile()` and prefill it on next visit; skip the prompt when a valid profile name exists (depends on T016, T070)
- [x] T072 [P] [US6] Add EN/BG strings for name entry and errors in `web/src/i18n/en.ts` and `bg.ts`

**Checkpoint**: Names replace compass labels everywhere, are remembered, and can be changed.

---

## Phase 9: User Story 7 - Lobby housekeeping: leaving, host handover, play again (Priority: P3)

**Goal**: Rooms stay usable when people leave, the host vanishes, or a game ends.

**Independent Test**: Switch seats, have the host leave and verify the remaining player can start; finish a game and play again.

### Tests for User Story 7

- [x] T073 [P] [US7] Write failing `tests/Belote.Table.Tests/Rooms/RoomHousekeepingTests.cs` (FakeTimeProvider): stand up and switch seats in the lobby; host leaves → host passes to another human; deliberate leave in game → seat `Bot(Left)` immediately, no pause; `PlayAgain` (host, `Finished` only) starts a new table with the same seats and names; a lobby seat whose holder is gone is released after `LobbySeatReleaseSeconds` (60); a room with no connected human is `Ended` after `IdleExpiryMinutes` (60) and its id then returns `RoomEnded`

### Implementation for User Story 7

- [x] T074 [US7] Extend `src/Belote.Table/Rooms/Room.cs`: `ReleaseSeat`, lobby seat switching, `LeaveRoom`, host transfer, `PlayAgain`, lobby seat release timer, idle expiry timer (depends on T062, T073)
- [x] T075 [US7] Add an expiry sweep and removal to `src/Belote.Server/RoomRegistry.cs`, push `TableClosed { reason: "ended" }` to remaining connections, and add `ReleaseSeat`, `LeaveRoom`, `PlayAgain` to `src/Belote.Server/TableHub.cs`
- [x] T076 [US7] Extend `web/src/screens/Lobby.tsx` and the game-over dialog: stand up / switch seat, **Leave room** (clears the saved session), host-only **Play again**, and `RoomEnded` handling via `RoomUnavailable`
- [x] T077 [P] [US7] Add EN/BG strings and `web/tests/housekeeping.test.tsx` for leave, host change notice and play again

**Checkpoint**: A room can host many consecutive games with people coming and going.

---

## Phase 10: Late join into a running game (Story 2 scenarios 7-8, FR-008a)

**Goal**: A newcomer can take a bot seat at the start of the next hand; replaced players keep reclaim priority.

**Independent Test**: With a game running, a third browser picks a bot seat and takes over when the next hand begins.

- [x] T078 [P] [US2] Write failing `tests/Belote.Table.Tests/Rooms/RoomLateJoinTests.cs`: a claim on `Bot(Default)` or `Bot(Left)` is queued and applied only when the table's deal number increases; `Bot(Replaced(p))` is not offered to others; the first claimant wins and later ones get `SeatTaken`; the takeover changes no game state or score; the `joiningNextHand` event is emitted
- [x] T079 [US2] Extend `src/Belote.Table/Rooms/Room.cs` with `PendingTakeover` handling keyed on deal-number change and `pendingTakeovers[]` in `RoomState` (depends on T062, T078)
- [x] T080 [US2] Allow `ClaimSeat` for in-progress rooms in `src/Belote.Server/TableHub.cs` and return `pendingNextHand: true`; in `Lobby`/in-game entry on the client show bot seats as **Take this seat** with "You'll join at the start of the next hand" and a pending indicator (`web/src/screens/Lobby.tsx`, `web/src/components/SeatPicker.tsx`)
- [x] T081 [P] [US2] Add EN/BG strings and an end-to-end case in `tests/Belote.Server.Tests/EndToEnd/MultiClientRoomTests.cs` (late joiner becomes a human at the next hand and receives their seat view)

---

## Phase 11: Polish & Cross-Cutting Concerns

- [x] T082 [P] Add a `ConcurrentRoomsTests` case in `tests/Belote.Server.Tests/EndToEnd/ConcurrentRoomsTests.cs`: 3 rooms with 4 humans each play simultaneously with no cross-room messages (SC-008)
- [x] T083 [P] Security review: confirm tokens are never logged or placed in URLs or DTOs (`grep` logging in `src/Belote.Server`), room ids/tokens use cryptographic randomness, names are rendered as text only in the client
- [x] T084 [P] Update `web/README.md` and the root `README.md` with the room flow, link format, configuration section `Room` and the in-memory-only caveat (restart ends rooms; where a durable store would become necessary)
- [x] T085 [P] Mark the v1-only parts of `specs/002-live-table-vs-bots/contracts/hub-protocol.md` as superseded by `specs/007-rooms-multiplayer/contracts/hub-protocol.md`
- [ ] T086 [P] Optional, opt-in only: add `web/e2e/rooms.spec.ts` covering the quickstart reload and replace flows (excluded from verification per project policy) _Not done on purpose: Playwright e2e is opt-in and outside verification (project policy); the same flows are covered by Vitest and the in-process SignalR tests._
- [x] T087 Run `dotnet test` for the whole solution and `npm run typecheck && npm test` in `web/`; fix regressions in earlier features
- [x] T088 Run the manual walkthrough in `specs/007-rooms-multiplayer/quickstart.md`, including the offline/online and server-restart checks, and record results _Partly done: a real-browser smoke (start, name prompt, lobby with link and code, start game, reload restoring the same seat and hand) was run against the built client; the offline and server-restart steps were not exercised by hand._

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → user stories.
- **US1** requires Foundational. **US2** requires US1's `Room`/registry/hub skeleton (T022-T024).
- **US3** requires US2 (humans must be able to join). **US4** requires US3 (a running multi-human game to pause). **US5** requires US4 (pause/missing model). **US6** requires US2 for the join flow but is otherwise independent of US4/US5. **US7** requires US5 for the seat/bot origin model. **Late join (Phase 10)** requires US3 and US5.
- **Polish** requires all stories you intend to ship.

### Within each story

- Tests first and failing, then domain (`Room.cs`), then registry/hub, then client.
- `src/Belote.Table/Rooms/Room.cs` is edited by US1-US7 and late join, so those tasks are sequential in story order even where the client work is parallel.

### Parallel opportunities

- Setup: T002 and T003 in parallel.
- Foundational: T004, T005, T006 together; then T010 and T011; client T014, T016, T017 in parallel with server T007-T013.
- Within each story, tests marked [P] run together; client components in different files (screens, overlays, strings) are [P].
- US6 (client name UI) can run in parallel with US4/US5 client work once US2 is done.

## Parallel Example: Foundational

```bash
# Failing tests together:
Task: "ControllerSwapTests in tests/Belote.Table.Tests/Unit/ControllerSwapTests.cs"
Task: "PerSeatUpdateTests in tests/Belote.Table.Tests/Unit/PerSeatUpdateTests.cs"
Task: "NameRulesTests in tests/Belote.Table.Tests/Unit/NameRulesTests.cs"

# Client plumbing in parallel with server plumbing:
Task: "session/storage.ts + tests"
Task: "routing/router.ts + parseRoomLink + tests"
Task: "perspective helper tests in web/tests/perspective.test.ts"
```

## Implementation Strategy

### MVP first (playable multiplayer: US1-US4)

1. Setup, then Foundational (T001-T019).
2. US1 → validate "Start game → lobby → play vs bots" through the new flow.
3. US2 and US3 → two friends playing together from a link.
4. US4 → reload/reconnect resilience. **Stop and validate**: this is the first release that matches the core ask, since resilience is the primary quality bar.

### Incremental delivery

5. US5 (bot replacement and reclaim, skip wait) → US6 (names polish) → US7 (housekeeping) → late join → polish.
6. Each step keeps earlier stories green; the legacy single-player hub shim keeps earlier features working throughout.

## Notes

- [P] tasks touch different files and have no unfinished dependency.
- `Room.cs` growth: if it exceeds ~600 lines, split lobby/pause/housekeeping into partial classes in the same folder; do not move logic into the server project.
- Never read the system clock directly in `Belote.Table`; always the injected `TimeProvider`.
- Commit after each task or logical group; keep Playwright out of the verification loop.
