import { HttpTransportType, HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import type {
  ConnectionStatus,
  CreateTableResult,
  GetViewResult,
  IntentDto,
  IntentResult,
  RoomResult,
  RoomState,
  TableClosed,
  TableUpdate,
  TakeSeatResult,
} from "../protocol/types";
import { CLIENT_SEAT } from "../protocol/types";

/** Waits between connection attempts: immediately, then 1 s, 2 s, 5 s, then every 10 s forever. */
const RETRY_DELAYS_MS = [0, 1000, 2000, 5000];
const STEADY_DELAY_MS = 10_000;

export function retryDelay(attempt: number): number {
  return RETRY_DELAYS_MS[attempt] ?? STEADY_DELAY_MS;
}

export interface HubHandlers {
  status: (status: ConnectionStatus) => void;
  update: (update: TableUpdate) => void;
  tableClosed: (message: TableClosed) => void;
  room: (state: RoomState) => void;
  superseded: () => void;
}

/** What the page needs from the game server; the SignalR implementation is below. */
export interface HubClient {
  start(handlers: HubHandlers): void;
  stop(): Promise<void>;
  createTable(): Promise<CreateTableResult>;
  takeSeat(): Promise<TakeSeatResult>;
  submitIntent(intent: IntentDto): Promise<IntentResult>;
  getView(): Promise<GetViewResult>;

  // Rooms (protocol v2). Identity is the token, never the connection.
  createRoom(request: { token: string; name: string; seed?: number; botDelayMs?: number }): Promise<RoomResult>;
  joinRoom(request: { roomId: string; token: string; name: string }): Promise<RoomResult>;
  rejoin(request: { roomId: string; token: string }): Promise<RoomResult>;
  setName(name: string): Promise<RoomResult>;
  claimSeat(seat: string): Promise<RoomResult>;
  releaseSeat(): Promise<RoomResult>;
  startRoomGame(): Promise<RoomResult>;
  skipWait(participantId?: string): Promise<RoomResult>;
  leaveRoom(): Promise<RoomResult>;
  playAgain(): Promise<RoomResult>;
  takeBack(): Promise<RoomResult>;
}

/** The subset of a SignalR HubConnection that the client relies on (also implemented by test fakes). */
export interface HubConnectionLike {
  readonly state: string;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T>;
  on(method: string, handler: (...args: never[]) => void): void;
  onclose(handler: (error?: Error) => void): void;
  onreconnecting(handler: (error?: Error) => void): void;
  onreconnected(handler: (connectionId?: string) => void): void;
}

export function createSignalRConnection(url = "/table"): HubConnectionLike {
  return new HubConnectionBuilder()
    .withUrl(url, { transport: HttpTransportType.WebSockets, skipNegotiation: false })
    // Match the server's 5 s keep-alive so a silent loss is noticed quickly on both sides.
    .withKeepAliveInterval(5000)
    .withAutomaticReconnect({
      // Never give up: the page keeps trying for as long as it is open.
      nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount),
    })
    .build() as unknown as HubConnectionLike;
}

const CONNECTED = HubConnectionState.Connected as string;

/** One start()..stop() lifetime. Stale loops and handlers only ever touch their own session. */
interface Session {
  readonly connection: HubConnectionLike;
  readonly handlers: HubHandlers;
  stopped: boolean;
  timer: ReturnType<typeof setTimeout> | null;
}

export class SignalRHubClient implements HubClient {
  private session: Session | null = null;

  constructor(private readonly factory: () => HubConnectionLike = () => createSignalRConnection()) {}

  start(handlers: HubHandlers): void {
    // React StrictMode (development) runs start, stop, start on the same client; a restart must
    // begin a completely fresh session and leave the previous one inert.
    if (this.session) this.endSession(this.session);

    const session: Session = { connection: this.factory(), handlers, stopped: false, timer: null };
    this.session = session;
    const { connection } = session;

    connection.on("Update", (update: TableUpdate) => {
      if (!session.stopped) handlers.update(update);
    });
    connection.on("TableClosed", (message: TableClosed) => {
      if (!session.stopped) handlers.tableClosed(message);
    });
    connection.on("Room", (state: RoomState) => {
      if (!session.stopped) handlers.room(state);
    });
    connection.on("Superseded", () => {
      if (!session.stopped) handlers.superseded();
    });
    connection.onreconnecting(() => this.setStatus(session, "reconnecting"));
    connection.onreconnected(() => this.setStatus(session, "connected"));
    connection.onclose(() => {
      if (session.stopped) return;
      this.setStatus(session, "unavailable");
      void this.connectLoop(session, 1);
    });

    this.setStatus(session, "connecting");
    void this.connectLoop(session, 0);
  }

  async stop(): Promise<void> {
    const session = this.session;
    this.session = null;
    if (session) await this.endSession(session);
  }

  createTable(): Promise<CreateTableResult> {
    return this.invoke<CreateTableResult>("CreateTable", {});
  }

  takeSeat(): Promise<TakeSeatResult> {
    return this.invoke<TakeSeatResult>("TakeSeat", { seat: CLIENT_SEAT });
  }

  submitIntent(intent: IntentDto): Promise<IntentResult> {
    return this.invoke<IntentResult>("SubmitIntent", intent);
  }

  getView(): Promise<GetViewResult> {
    return this.invoke<GetViewResult>("GetView");
  }

  createRoom(request: { token: string; name: string; seed?: number; botDelayMs?: number }): Promise<RoomResult> {
    return this.invoke<RoomResult>("CreateRoom", request);
  }

  joinRoom(request: { roomId: string; token: string; name: string }): Promise<RoomResult> {
    return this.invoke<RoomResult>("JoinRoom", request);
  }

  rejoin(request: { roomId: string; token: string }): Promise<RoomResult> {
    return this.invoke<RoomResult>("Rejoin", request);
  }

  setName(name: string): Promise<RoomResult> {
    return this.invoke<RoomResult>("SetName", { name });
  }

  claimSeat(seat: string): Promise<RoomResult> {
    return this.invoke<RoomResult>("ClaimSeat", { seat });
  }

  releaseSeat(): Promise<RoomResult> {
    return this.invoke<RoomResult>("ReleaseSeat");
  }

  startRoomGame(): Promise<RoomResult> {
    return this.invoke<RoomResult>("StartGame");
  }

  skipWait(participantId?: string): Promise<RoomResult> {
    return this.invoke<RoomResult>("SkipWait", { participantId: participantId ?? null });
  }

  leaveRoom(): Promise<RoomResult> {
    return this.invoke<RoomResult>("LeaveRoom");
  }

  playAgain(): Promise<RoomResult> {
    return this.invoke<RoomResult>("PlayAgain");
  }

  takeBack(): Promise<RoomResult> {
    return this.invoke<RoomResult>("TakeBack");
  }

  private invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    const connection = this.session?.connection;
    if (!connection || connection.state !== CONNECTED) {
      return Promise.reject(new Error("Not connected"));
    }
    return connection.invoke<T>(method, ...args);
  }

  private async endSession(session: Session): Promise<void> {
    session.stopped = true;
    if (session.timer !== null) clearTimeout(session.timer);
    session.timer = null;
    try {
      await session.connection.stop();
    } catch {
      // already stopped
    }
  }

  private setStatus(session: Session, status: ConnectionStatus): void {
    if (!session.stopped) session.handlers.status(status);
  }

  /** Starts the connection, retrying with backoff until it succeeds or the session is stopped. */
  private async connectLoop(session: Session, firstAttempt: number): Promise<void> {
    let attempt = firstAttempt;
    while (!session.stopped) {
      const delay = retryDelay(attempt);
      if (delay > 0) await this.sleep(session, delay);
      if (session.stopped) return;
      try {
        await session.connection.start();
        this.setStatus(session, "connected");
        return;
      } catch {
        if (session.stopped) return;
        this.setStatus(session, "unavailable");
        attempt += 1;
      }
    }
  }

  private sleep(session: Session, ms: number): Promise<void> {
    return new Promise((resolve) => {
      session.timer = setTimeout(() => {
        session.timer = null;
        resolve();
      }, ms);
    });
  }
}
