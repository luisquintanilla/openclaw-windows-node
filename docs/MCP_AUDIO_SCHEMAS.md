# Audio MCP input schemas

This pilot is a narrow follow-up to existing MCP schema work, not
a newly discovered gap or a regression in a shipped schema implementation.
Local before/after and focused compatibility checks passed on source
`a993bc9e08fe1815f6080e571570f82d19f00bc3`. The full WinUI build remains blocked
by an existing npm TLS download failure. See the [evidence and exact
limitations](evidence/mcp-audio-schemas/README.md), not a merge-readiness claim.

## Prior work and scope

[openclaw/openclaw-windows-node#355](https://github.com/openclaw/openclaw-windows-node/issues/355)
already identified permissive `tools/list` schemas. Its
[closure comment](https://github.com/openclaw/openclaw-windows-node/issues/355#issuecomment-4473186623)
explicitly says it was consolidated, not necessarily fixed, into the MCP
hardening work tracked in
[openclaw/openclaw-windows-node#372](https://github.com/openclaw/openclaw-windows-node/issues/372).
That tracker was later
[closed in favor of the project board's Scenarios view (Mission field)](https://github.com/openclaw/openclaw-windows-node/issues/372#issuecomment-5222150923),
where scope would be tracked going forward. These are historical references,
not a claim that the closed tracker is the current owner, that the schema
change shipped or that a duplicate broad issue is needed.

[openclaw/openclaw-windows-node#401](https://github.com/openclaw/openclaw-windows-node/pull/401)
(Add typed MCP tool input schemas) proposed a broader schema catalog. It was
closed without merging, and the
[maintainer's disposition](https://github.com/openclaw/openclaw-windows-node/pull/401#issuecomment-4472964017)
requires fresh review and implementation after the refactor.

This pilot covers only five existing audio tools and preserves permissive
argument parsing. It adds no runtime validation and does not revive the old
all-command implementation. Any eventual follow-up should contribute its
bounded implementation and current-parser evidence to that existing history,
rather than present the general schema concern as new.

## Proposed metadata

On this branch, `McpToolBridge` publishes curated `inputSchema` metadata for the existing
`stt.transcribe`, `stt.listen`, `stt.status`, `tts.speak` and `tts.status` tool
names. `McpAudioToolSchemas` is a discovery-only helper. It neither registers
capabilities nor validates or rewrites calls. Disabled capabilities remain
absent, tool descriptions and text-wrapped results are unchanged, and other
commands retain the original schema:

```json
{"type":"object","additionalProperties":true,"properties":{}}
```

For example, `stt.transcribe` now declares these fields (description strings
omitted here; the complete response is emitted by the catalog test below):

```json
{
  "type": "object",
  "additionalProperties": true,
  "properties": {
    "maxDurationMs": {"type": "integer", "minimum": 1, "maximum": 30000},
    "language": {"examples": ["en-US", "auto"]}
  },
  "required": ["maxDurationMs"]
}
```

`tts.speak` declares required string `text` with `minLength: 1`, and names
`provider`, `voiceId`, `model` and `interrupt`. `stt.listen` names `timeoutMs`
and `language` with default annotations `30000` and `"auto"`. Status commands
describe an empty parameter set and still allow ignored unknown properties.
There is no new package, capability interface, output schema or transport.

## Compatibility boundaries

These schemas guide discovery; they do not completely predict success.
The existing capability parsers and tray-side services remain authoritative.

| Input | Existing behavior retained | Schema treatment |
|---|---|---|
| Transcribe `maxDurationMs` | Required positive Int32, at most 30000. Wrong type, fraction, decimal/exponent token or overflow selects zero and fails. | Required integer, minimum 1, maximum 30000. JSON Schema considers `5000.0` and `5e3` integers, but the parser does not. The description tells callers to send integer tokens. |
| Listen `timeoutMs` | Int32 values clamp to 1000..120000; missing, wrong type or unreadable number defaults to 30000. | Description, example and default annotation, without a misleading raw type/range restriction. |
| STT `language` | Strings trim and accept case-insensitive `auto` or the capability's limited language/script/region pattern. Blank, null and non-string inputs act as omitted. Transcribe uses the configured language; listen uses `auto`. | Examples and descriptions, not an enum or a new language validator. |
| TTS `text` | Required string. Trim first, reject whitespace-only or more than 5000 UTF-16 code units. | String and `minLength: 1`. This does not reject all whitespace. Raw `maxLength: 5000` would reject padded valid text and count Unicode differently. |
| TTS `provider` | Trimmed, case-insensitive at service resolution. Nonblank explicit requests never silently fall back. Unknown names reach the service and fail there. | Provider examples, not a case-sensitive enum. No static default: the configured provider controls it. |
| TTS `voiceId` / `model` | Optional strings trim. Missing, blank or non-string values use settings. Model is for cloud providers. | No unconditional voice/model requirement; readiness depends on settings and installed voices. |
| TTS `interrupt` | Only JSON `true` selects true; all other values select false. | Description and default annotation, no restriction that rejects existing coercions. |
| Unknown keys, including aliases/case variants | Ignored. Recognized argument names are case-sensitive. Required canonical keys still must be present. | `additionalProperties: true` on every schema. |

Optional fields with wrong-type fallback deliberately omit `type`. Adding
strict types, a provider enum or a listen timeout range would make some clients
reject calls that the current server accepts. Defaults are JSON Schema
annotations, not instructions for the bridge to insert or coerce values.
Permission, model readiness and cloud credentials cannot be proven by a static
input schema. No agent success-rate or latency improvement is claimed.

## Safe reproduction

From an isolated checkout on a configured development host:

```powershell
$env:OPENCLAW_REPO_ROOT = (Get-Location).Path
dotnet test .\tests\OpenClaw.Shared.Tests\OpenClaw.Shared.Tests.csproj --filter FullyQualifiedName~McpAudioToolSchemaTests --logger "trx;LogFileName=audio-schemas.trx"
```

`ToolsList_ExportsActualAudioCatalog` writes the complete JSON-RPC `tools/list`
response to test output (also captured by TRX). The suite invokes real
`SttCapability` / `TtsCapability` parsing through the real bridge with synthetic
event handlers. It checks schema properties, accepted and rejected inputs,
clamping, ignored keys, strict provider-resolution policy and unchanged result
JSON. It does not open a microphone, play sound, inspect private files, download
models or call a cloud provider. The listen result check intentionally retains
the existing PascalCase `SttSegment` JSON members.

The recorded run provides in-process JSON-RPC evidence, not a running tray,
HTTP client, gateway or audio engine proof. The harness-only baseline passed
75 cases; the candidate passed all 81 new cases and 121 existing bridge/audio
cases. Five schemas changed, other audio catalog fields stayed identical,
and seven synthetic request/response pairs stayed identical. PowerShell 7.6.6
`Test-Json` (JsonSchema.Net 7.0.0.0, draft 2020-12 selected for validation only)
matched all 21 declared schema-acceptance expectations. This includes the
documented differences between schema acceptance and capability success.

Repository closeout also requires `.\build.ps1`, full Shared and Tray tests,
and WinNode CLI tests for the changed command documentation. Isolate
`OPENCLAW_TRAY_DATA_DIR` for tests that use settings. Report actual totals and
environmental failures separately from the focused result.
