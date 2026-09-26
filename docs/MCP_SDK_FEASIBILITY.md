# Official MCP SDK protocol-core experiment

This branch is a bounded feasibility experiment, not a production HTTP migration.
The tray still constructs the same `McpHttpServer` and `McpToolBridge`. The only
production change is an internal catalog/invocation seam in the bridge. There are
no new commands, schema changes, permissions, listeners, or production packages.

**Protocol-core feasibility is demonstrated; candidate acceptance is not green.**
All 32 SDK proof cases passed. The unchanged HTTP/legacy dispatcher selector
passed 89 cases on baseline and candidate, but the candidate full Shared suite
repeatedly failed an existing disposal assertion that catches process-global
unobserved closed-file exceptions. A reduced interaction selector also failed.
The exact exception producer and root cause are **unknown**. This is an
unresolved regression risk, separate from the full-build npm TLS blocker, and
the branch is not merge-ready. See the [validation ledger](evidence/official-mcp-sdk/VALIDATION.md).

## Problem and boundary

The current bridge owns both application policy and protocol maintenance:
the `2024-11-05` initialize response, request IDs, method routing, notification
dispatch, error envelopes, and cancellation matching. These evolve independently
of capability registration, consent, approvals, sandbox execution, and diagnostics.

`tests\OpenClaw.Mcp.Sdk.Tests` references official `ModelContextProtocol.Core`
**2.2.0**. Its `SdkBridgeProof` registers only the SDK's typed list/call handlers.
`McpServer` actually dispatches the messages. It does not forward JSON-RPC
envelopes to the old bridge dispatcher, reflect capability methods, or reconstruct
capability registration. The list handler reads the live catalog; the call handler
uses `InvokeToolAsync`, which retains `CanHandle`, `NodeInvokeRequest`,
`ExecuteAsync`, bounded waiting, result serialization, and diagnostic completion.
The custom `ITransport` decorator completes existing delivery telemetry after the
SDK's stream transport writes a response.

| Responsibility | Production before and after | Experiment |
|---|---|---|
| HTTP binding, auth, browser gate, body cap, admission, draining | `McpHttpServer`, unchanged | Not migrated |
| JSON-RPC decoding, routing, initialize negotiation, session cancellation | `McpToolBridge` | Official `McpServer` / `StreamServerTransport` |
| Live catalog, invocation policy, sanitized errors, text payload | `McpToolBridge` | Same internal bridge seam |
| Delivery diagnostics | Existing HTTP write completion | Test-only `ITransport` decorator |
| Schema metadata | Existing permissive catalog | Consumed without expanding it |

## Verified source, not assumed API

Release: [v2.2.0](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.2.0),
published August 13, 2026. All SDK source links below are pinned to tag commit
`6fa3825973949a9c4f0cd8af344e15a8db09dc35`.

- [Core package](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/ModelContextProtocol.Core.csproj)
  targets net10.0, net9.0, net8.0, and netstandard2.0. For net10.0 its direct
  package dependencies are AI.Abstractions 10.8.3 and Logging.Abstractions 10.0.10.
  The package includes an analyzer. It does not require ASP.NET hosting.
- [Official in-memory example](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/samples/InMemoryTransport/Program.cs)
  demonstrates `McpServer.Create`, `StreamServerTransport`, `StreamClientTransport`,
  and `McpClient.CreateAsync`. This experiment uses that supported transport
  composition with explicit handlers rather than reflected tools.
- [Public ITransport](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/Protocol/ITransport.cs)
  supports a custom host adapter.
- [Public StreamableHttpServerTransport](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/Server/StreamableHttpServerTransport.cs)
  accepts a decoded `JsonRpcMessage` and response `Stream`, independent of Kestrel.
  Its POST responses are SSE. It does not supply OpenClaw's HTTP security policy.
- [Server options](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/Server/McpServerOptions.cs)
  support four initialize-based revisions and 2026-07-28 per-request metadata.
  The latter removes initialize and is outside this experiment's interoperability claims.
- [Initialize boundary implementation](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/Server/McpServerImpl.cs)
  (`ValidateInitializeRequestBoundary`) rejects an unsupported handshake version
  using `UnsupportedProtocolVersionException`. The independently authored
  `2099-01-01` request returned `-32022`, not a fallback version, with supported
  revisions `2024-11-05`, `2025-03-26`, `2025-06-18`, and `2025-11-25`.
  The failed fallback expectation and corrected passing transcript are retained.
  Runtime host resolution selected Core 2.2.0; its actual DLL ProductVersion is
  `2.2.0+6fa3825973949a9c4f0cd8af344e15a8db09dc35`.
- [Request IDs](https://github.com/modelcontextprotocol/csharp-sdk/blob/6fa3825973949a9c4f0cd8af344e15a8db09dc35/src/ModelContextProtocol.Core/Protocol/RequestId.cs)
  are strings or signed 64-bit integers, not arbitrary JSON numbers.

Proof-only packages must not force a downgrade of another proposal's
Microsoft.Extensions.AI.Abstractions 10.9.0. A production integration would need
an explicit package-unification check across the tray, shared libraries, and
speech implementations. This experiment changes none of their pins.
Normal NuGet restore succeeded without a v2-feed recovery or dependency workaround.
The actual net10.0 proof graph resolves Core 2.2.0, AI.Abstractions 10.8.3,
Logging.Abstractions 10.0.10, and DependencyInjection.Abstractions 10.0.10.
The full restored graph is retained in the evidence.

## Compatibility decisions, not parity claims

| Case | Legacy behavior | SDK proof / decision needed |
|---|---|---|
| Supported initialize | Always returns 2024-11-05, accepts missing params | Official client/server negotiated each of the four supported initialize revisions; valid clientInfo/capabilities required |
| Unsupported future initialize | Always returns 2024-11-05 | Literal 2099-01-01 request rejected with -32022 and all four supported revisions, not fallback |
| New lifecycle | No server/discover | SDK supports 2026-07-28; not claimed validated here |
| Unknown methods | -32601; resources/list and prompts/list return empty lists | SDK returns -32601 for unregistered methods; decide whether to retain legacy probes |
| Unknown tools | `result.isError=true` | Deliberately retained by the application handler, although SDK docs recommend protocol errors for unknown names |
| Successful results | One text block containing serialized payload; explicit isError=false | Retained for the tested initialize-based revisions; no structuredContent or audio blocks |
| Notification named tools/call | Executes, sends no response | SDK does not route notifications to request handlers; intentional experiment difference |
| IDs | Preserves fractional, exponent, arbitrary-precision numbers | SDK signed int64/string restriction; no silent coercion approved |
| Malformed uncorrelatable JSON | JSON-RPC parse error with null ID | SDK stream transport logs/drops uncorrelatable input; not acceptable as an unreviewed HTTP replacement |
| Cancellation | Global stateless matching, 5-second tombstones, collision guards; returns cancelled tool error | SDK per-session cancellation; proof suppresses the response and completes canceled diagnostics. It passes requestKey=null, bypassing the legacy ID registry. Early/late/multi-client parity is not claimed |
| HTTP notifications | 204, empty | Streamable HTTP normally 202, empty; requires explicit agreement |
| HTTP response | application/json; friendly GET probe | Core HTTP transport uses SSE; changing it requires client/CLI compatibility work |
| List ordering/pagination | Registry order, one unpaginated list, ignores cursor | Retained by list handler; no automatic pagination claim |
| Delivery/shutdown | HTTP deadlines and handler drain | Stream experiment has bounded lifetime; retained HTTP tests are regression evidence only |

The SDK cancellation token identifies caller/session cancellation, not why a
session ended. The adapter supplies a separate deadline token to the shared seam.
The proof's delivery map is not a replacement for admission or duplicate-ID
policy: it is populated after execution. It must not become a production guard.
The pinned SDK checks cancellation before forwarding responses to a transport,
so an invocation can finish without a subsequent transport call. The adapter
registers caller cancellation while delivery is pending and binds each response
to its own SDK `RelatedTransport`. Cancellation and sending claim the exact
entry, so a late response cannot complete another invocation that reused its ID.
Registration handles are unregistered after completion; shutdown drains pending
entries in `finally`, even if disposing the underlying stream transport fails.
Cancellation during capability execution retains its canceled diagnostic.
Cancellation after execution but before delivery records a transport failure,
not a claim that the already-completed capability was canceled.
`CancellationAfterHandlerReturn_CompletesDeliveryBeforeShutdown` uses an SDK
request filter and explicit task gates to exercise the gap without sleeps.
The disposal-failure and late-response tests inject transport lifecycle faults
directly; they are not additional client-interoperability claims.
Shutdown attribution and first-cause races need a host-specific mapping before
production adoption. The SDK's optional logs can contain message content; this
experiment installs no logger/exporter. A production integration must not export
raw SDK traffic or enable sensitive trace logging by default.

## Follow-on host options

Keeping HttpListener is technically possible. After the existing loopback,
host/origin/browser, bearer, content-type, 4 MiB, and admission gates, decode a
bounded body with the SDK serializer and pass the message and response stream to
`StreamableHttpServerTransport.HandlePostRequestAsync`. Keep the HTTP request
deadline and tracked handler drain. Supply explicit error/status handling for
malformed input before dispatch. Wire the real request token to capability
execution, not only to the response wait, and finish diagnostics after delivery.

That is **not** a drop-in patch: the supported transport writes SSE, and session
identity/lifetime must match the chosen protocol revision. One global SDK session
would mix clients. One session per POST would lose initialize context and
cancellation. Neither shortcut is proposed. Maintainers must choose a supported
session/metadata lifecycle and JSON/SSE contract before this work proceeds.

Alternatives:

- Implement a custom `ITransport` for existing plain JSON POST responses. This
  avoids Kestrel but leaves HTTP correlation, status, cancellation, and lifecycle
  maintenance in OpenClaw. Count that residual code before claiming savings.
- Use `ModelContextProtocol.AspNetCore` and a desktop-owned host, preserving every
  security gate and shutdown invariant. It adds hosting/DI dependencies and a
  larger runtime/lifecycle change; samples alone are not justification.
- Keep the current bridge. Its limited local-only scope may not justify a
  transport migration while compatibility requirements remain unresolved.

## Reproduce and acceptance scope

Run from an isolated checkout, with isolated tray settings and
`OPENCLAW_REPO_ROOT` set to that checkout:

```powershell
dotnet test .\tests\OpenClaw.Mcp.Sdk.Tests\OpenClaw.Mcp.Sdk.Tests.csproj
.\build.ps1 -NoTrustRepository
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --no-restore
dotnet test .\tests\OpenClaw.Tray.Tests\OpenClaw.Tray.Tests.csproj --no-restore
dotnet test .\tests\OpenClaw.WinNode.Cli.Tests\OpenClaw.WinNode.Cli.Tests.csproj --no-restore
```

Fresh test projects need restore/build first. `--no-restore` alone can otherwise
report success without running tests. The proof uses safe fake capabilities and
in-memory pipes, not a live tray, microphone, gateway, or command execution.
The isolated proof project is not registered in the solution or the upstream
CI workflow; its explicit test command is required.

The literal transcript assertions are independent of the candidate. They cover
negotiation, supported IDs, serialized-text success, denied and unknown tools,
sanitized failures, notification non-response, malformed stream input, dynamic
registry listing, cancellation, deadline, and shutdown. Required Shared tests
exercise the unchanged actual HTTP boundary: auth, browser/host gate, body cap,
content type, timeout, admission, delivery diagnostics, and drain.

**Execution status:** local Windows validation ran after the exclusive host grant
on September 26, 2026. No hosted workflow ran. All 32 proof cases across 19 methods
passed, with zero skips, on the corrected final source. Two earlier proof compile
failures and the failed fallback expectation remain in the ledger.

Full Shared acceptance remains failed: 4,106 passed, one failed, 32 skipped.
The full baseline passed 4,107 with the same 32 skips. The isolated HTTP selector
passed 89 on each, but a reduced process-drain/HTTP interaction selector passed
32 on baseline and failed one of 32 on candidate. The unresolved failure must
not be classified as a proven baseline flake. Full Tray passed 3,072 after
existing runtime-copy targets recovered assets omitted by the failed full build;
WinNode CLI passed 127. The full build failed once at the locked node-pty npm TLS
download and was not retried. Scoped recovery is not shipping or MXC proof.

The three delivery/cancellation/disposal/ID-reuse cases are validated within
their stated stream and fault-injection boundaries. Broader transport races,
global cancellation/tombstone parity, full 2026 lifecycle, real OS permission
execution, UI, gateway behavior, and migrated HTTP remain unverified.
No performance or security-certification claim follows from this experiment.
