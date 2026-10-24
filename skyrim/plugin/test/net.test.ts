import { test } from "node:test";
import assert from "node:assert/strict";
import { SharedWorldClient, type Transport } from "../src/net.ts";
import type { ServerMessage } from "../src/protocol.ts";

const hello = () => ({ type: "hello" as const, player: "Joe", password: "", protocolVersion: 1, party: ["Joe"] });
const flush = () => new Promise((r) => setTimeout(r, 0));

function fakeServer() {
  const calls: Array<{ path: string; body: any }> = [];
  let sessions = 0;
  let forget = false;
  const transport: Transport = {
    async post(path, body) {
      calls.push({ path, body: JSON.parse(body) });
      if (path === "/v1/join") {
        sessions++;
        return { status: 200, body: JSON.stringify({ session: `s${sessions}`, messages: [{ type: "welcome", player: "Joe", campaign: "c", flags: [] }] }) };
      }
      if (forget) { forget = false; return { status: 401, body: JSON.stringify({ messages: [], error: "expired" }) }; }
      return { status: 200, body: JSON.stringify({ session: "s", messages: [{ type: "notice", kind: "Chat", text: "hi", from: "Mira" }] }) };
    },
  };
  return { transport, calls, forgetNext: () => { forget = true; } };
}

test("joins, then syncs queued messages on the interval", async () => {
  const server = fakeServer();
  const got: ServerMessage[] = [];
  const client = new SharedWorldClient(server.transport, hello, (m) => got.push(m), 1000);

  client.tick(0); await flush();
  assert.equal(client.status, "connected");
  assert.equal(got[0].type, "welcome");

  client.send({ type: "say", text: "hello" });
  client.tick(500); await flush();
  assert.equal(server.calls.length, 1, "waits for the interval");
  client.tick(1000); await flush();
  assert.deepEqual(server.calls[1].body, { session: "s1", messages: [{ type: "say", text: "hello" }] });
  assert.equal(got.at(-1)!.type, "notice");
});

test("rejoins and resends when the server forgot the session", async () => {
  const server = fakeServer();
  const client = new SharedWorldClient(server.transport, hello, () => {}, 1000);
  client.tick(0); await flush();
  server.forgetNext();
  client.send({ type: "rested" });
  client.tick(1000); await flush();
  assert.equal(client.status, "disconnected");
  client.tick(2000); await flush(); // rejoin
  client.tick(3000); await flush(); // resend
  assert.equal(server.calls.filter((c) => c.path === "/v1/join").length, 2);
  assert.deepEqual(server.calls.at(-1)!.body.messages, [{ type: "rested" }]);
});

test("a rejected join stops trying and says why", async () => {
  const transport: Transport = {
    async post() {
      return { status: 403, body: JSON.stringify({ messages: [{ type: "rejected", reason: "Wrong password." }] }) };
    },
  };
  const client = new SharedWorldClient(transport, hello, () => {}, 1000);
  client.tick(0); await flush();
  assert.equal(client.status, "rejected");
  assert.equal(client.lastError, "Wrong password.");
});

test("backs off when the server can't be reached", async () => {
  let attempts = 0;
  const transport: Transport = { async post() { attempts++; return { status: 0, body: "" }; } };
  const client = new SharedWorldClient(transport, hello, () => {}, 1000);
  client.tick(0); await flush();
  client.tick(500); await flush();
  assert.equal(attempts, 1);
  client.tick(1000); await flush();
  client.tick(2500); await flush(); // second backoff is 2s, not yet
  assert.equal(attempts, 2);
  client.tick(3000); await flush();
  assert.equal(attempts, 3);
});
