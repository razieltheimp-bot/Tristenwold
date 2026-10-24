// Talks to the Tristinwold server's HTTP gateway: join once, then sync about once a second,
// posting whatever is queued and receiving whatever is waiting. Driven by tick() from the
// game's update loop; never blocks it.
import type { ClientMessage, Hello, ServerMessage, SyncResponse } from "./protocol.ts";

/** One POST. status 0 (or an empty body) means the request never reached the server. */
export interface Transport {
  post(path: string, body: string): Promise<{ status: number; body: string }>;
}

export type ConnectionStatus = "disconnected" | "connecting" | "connected" | "rejected";

export class SharedWorldClient {
  status: ConnectionStatus = "disconnected";
  lastError = "";

  private session: string | null = null;
  private outbox: ClientMessage[] = [];
  private inFlight = false;
  private nextAttemptMs = 0;
  private backoffMs = 1000;

  private readonly transport: Transport;
  private readonly hello: () => Hello;
  private readonly onMessage: (message: ServerMessage) => void;
  private readonly intervalMs: number;

  constructor(transport: Transport, hello: () => Hello, onMessage: (message: ServerMessage) => void, intervalMs = 1000) {
    this.transport = transport;
    this.hello = hello;
    this.onMessage = onMessage;
    this.intervalMs = intervalMs;
  }

  send(message: ClientMessage): void {
    if (this.status !== "rejected") this.outbox.push(message);
  }

  /** Call every frame (or often). Starts at most one request at a time. */
  tick(nowMs: number): void {
    if (this.inFlight || this.status === "rejected" || nowMs < this.nextAttemptMs) return;
    this.inFlight = true;
    const request = this.session === null ? this.join() : this.sync();
    request
      .then(() => { this.nextAttemptMs = nowMs + this.intervalMs; })
      .catch((err: unknown) => this.failed(nowMs, String(err)))
      .then(() => { this.inFlight = false; });
  }

  private async join(): Promise<void> {
    this.status = "connecting";
    const res = await this.transport.post("/v1/join", JSON.stringify(this.hello()));
    const body = parse(res.body);
    if (res.status === 200 && body?.session) {
      this.session = body.session;
      this.status = "connected";
      this.backoffMs = 1000;
      this.deliver(body);
      return;
    }
    if (res.status === 403) {
      this.status = "rejected";
      this.deliver(body);
      const rejected = body?.messages.find((m) => m.type === "rejected");
      this.lastError = rejected && rejected.type === "rejected" ? rejected.reason : "rejected";
      return;
    }
    throw new Error(body?.error ?? `join failed (HTTP ${res.status})`);
  }

  private async sync(): Promise<void> {
    const messages = this.outbox;
    this.outbox = [];
    const res = await this.transport.post("/v1/sync", JSON.stringify({ session: this.session, messages }));
    if (res.status === 401) {
      // The server forgot us (restart, or we were away too long). Join again and resend.
      this.session = null;
      this.status = "disconnected";
      this.outbox = messages.concat(this.outbox);
      return;
    }
    const body = parse(res.body);
    if (res.status !== 200 || !body) {
      this.outbox = messages.concat(this.outbox);
      throw new Error(body?.error ?? `sync failed (HTTP ${res.status})`);
    }
    this.deliver(body);
  }

  private deliver(body: SyncResponse | null): void {
    for (const m of body?.messages ?? []) this.onMessage(m);
  }

  private failed(nowMs: number, error: string): void {
    this.lastError = error;
    if (this.status === "connecting") this.status = "disconnected";
    this.nextAttemptMs = nowMs + this.backoffMs;
    this.backoffMs = Math.min(30000, this.backoffMs * 2);
  }
}

function parse(text: string): SyncResponse | null {
  if (!text) return null;
  try {
    return JSON.parse(text) as SyncResponse;
  } catch {
    return null;
  }
}
