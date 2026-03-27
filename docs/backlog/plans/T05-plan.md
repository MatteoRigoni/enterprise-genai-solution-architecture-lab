# T05 - Tool/Function Calling + Guardrails + Injection Tests - Implementation Plan

## Task Overview
Add allow-listed tool calling to chat with strict input/output validation, metadata-only audit logs, and a deterministic prompt-injection test suite. Also surface a tool-calling enabled/disabled indicator in the Admin panel and update `docs/security.md` to map mitigations for tool abuse and injection attempts.

## High-Level Plan (5 Steps)
1. Add tool-calling configuration + scaffolding (2 mock tools + tool registry).
2. Implement tool-call proposal parsing + allow-list enforcement (block forbidden tools).
3. Implement guardrails: input validation (schema, length, allowed chars) + output validation/redaction (size limit, redaction).
4. Add injection regression suite (10 prompts) with deterministic behavior using the mock LLM/retrieval in tests.
5. Wire Admin indicator + update `docs/security.md` mitigation mapping.

## Sub-Task Decomposition

### T05.A: Tool-calling scaffolding + enable/disable config + proposal parsing
**Scope:**
- Add configuration:
  - `ToolCalling:Enabled` (default `false`)
  - `ToolCalling:MaxToolCallsPerRequest` (default `1` or `2` to keep bounded behavior)
- Add Application-layer abstractions:
  - `ToolCallProposal` (parsed from LLM output)
  - `IToolHandler` / `IToolRegistry` (allow-list of tool names)
  - (optional) `IToolCallParser` interface for deterministic parsing + unit tests
- Modify chat execution path:
  - Update prompt composition (in `ChatService`) when `ToolCalling:Enabled` is true to instruct the model to emit a tool call proposal in a well-defined marker format (e.g. `<tool_call>{...}</tool_call>`).
  - Parse the LLM response; if a tool call proposal is present, route it to the Tool Router; otherwise fall back to normal chat response behavior.
- Add Infrastructure mock tool handlers:
  - `GetOrderStatus(orderId)`
  - `CreateSupportTicket(subject, details)`
  - Tools should return small, deterministic outputs suitable for tests/demo.

**Files to touch (expected):**
- `src/AiSa.Host/Program.cs` (register tool-calling options + enable services)
- `src/AiSa.Application/ChatService.cs` (conditionally parse/route tool calls)
- `src/AiSa.Application/Models/*` (new tool-call proposal DTOs, if needed)
- `src/AiSa.Application/*` (new tool router/registry interfaces)
- `src/AiSa.Infrastructure/*` (mock tool implementations, if needed)
- `tests/AiSa.Tests/*` (parser unit tests + minimal integration test)

**Minimal test:**
- Unit: `ToolCallParserTests` validates parsing of a valid `<tool_call>{...}</tool_call>` payload and rejects malformed payloads.
- Integration (mock retrieval with results): when `ToolCalling:Enabled=true`, a prompt that causes a `GetOrderStatus` proposal results in an HTTP 200 and a response containing a safe tool-derived message (no raw args).

**Acceptance:**
- Tool calling can be turned on/off via config without breaking existing chat behavior when disabled.
- Chat execution remains bounded by tool-call count.

---

### T05.B: Input validation guardrails (schema + length + allowed chars)
**Scope:**
- Implement strict validation for each allowed tool:
  - `GetOrderStatus(orderId)`
    - `orderId`: length limit (e.g. <= 64) + allowed characters regex (e.g. alnum + `-` `_`)
  - `CreateSupportTicket(subject, details)`
    - `subject`: length limit + allowed chars
    - `details`: length limit + allowed chars (or allow broader chars but still block control chars)
- Add validation behavior:
  - If the tool name is not in allow-list => block and return a safe refusal message.
  - If args fail validation => block and return a safe refusal message.
  - Never execute a tool with invalid args.
- Ensure validation is deterministic and unit-testable (avoid depending on LLM output randomness).

**Files to touch (expected):**
- `src/AiSa.Application/*` (new `IToolInputValidator` / validators per tool)
- `src/AiSa.Application/*` (tool router updates to call validators)
- `tests/AiSa.Tests/*` (validator unit tests)

**Minimal test:**
- Unit tests:
  - Valid args are accepted for both tools.
  - Forbidden tool name is blocked.
  - Invalid `orderId` / `subject` / `details` fail validation and tool execution does not occur.

**Acceptance:**
- Allow-list is enforced before tool execution.
- Args schema/length/allowed-chars validation is applied consistently.

---

### T05.C: Output validation, redaction, and metadata-only audit trail
**Scope:**
- Implement output guardrails:
  - Max tool output length (e.g. <= 2k chars).
  - Redaction for suspicious patterns in tool outputs (PII/secrets/unsafe payloads).
- Implement metadata-only audit events for every tool proposal:
  - tool name
  - args hash (hash of normalized args, never raw args)
  - outcome (allowed/executed/blocked/validation_failed/tool_error)
  - timestamp + correlation id
- Ensure audit logs never contain:
  - raw user prompt
  - raw tool args
  - raw tool outputs

**Files to touch (expected):**
- `src/AiSa.Application/*` (output sanitizer + args hashing helper)
- `src/AiSa.Application/*` (audit event creation + logging integration)
- `tests/AiSa.Tests/*` (redaction + args-hash determinism tests)

**Minimal test:**
- Unit tests:
  - Output sanitizer truncates and redacts patterns deterministically.
  - Args hashing is stable given equivalent arg objects (tests should use canonical JSON or deterministic serialization approach).

**Acceptance:**
- Audit events are emitted for tool proposals and do not leak sensitive content.

---

### T05.D: Prompt injection regression suite (10 prompts) + deterministic mock behavior
**Scope:**
- Create a deterministic injection test suite with 10 prompts covering:
  - system instruction override attempts
  - attempts to exfiltrate document contents
  - attempts to request forbidden tool names
  - attempts to smuggle tool calls with malicious args patterns
  - attempts to coerce the tool router into executing multiple tools or oversized outputs
- Test harness options:
  - Prefer xUnit integration tests that call `/api/chat` with:
    - tool calling enabled
    - mock retrieval service that returns context (so ChatService actually calls the LLM)
    - a deterministic mock LLM response that emits tool call proposals based on the input prompt category
- Expected assertions:
  - forbidden tool calls are blocked
  - safe refusal responses are returned
  - responses do not contain forbidden tool outputs or unredacted content
  - audit outcome metadata is consistent (as far as unit-testable via an audit sink abstraction, if introduced)

**Files to touch (expected):**
- `tests/AiSa.Tests/*` (new `ToolInjectionTests.cs` and possibly deterministic mock LLM behavior)
- `src/AiSa.Infrastructure/MockLLMClient.cs` (extend to handle tool-call proposal format in a deterministic way for tests)
- (optional) new test helpers for categorizing prompts and toggling tool-calling config

**Minimal test:**
- Integration test that iterates 10 prompts:
  - each prompt returns HTTP 200 with a refusal/safe response (or a non-executed result)
  - at least one negative assertion per category (e.g. response does not contain forbidden tool name output marker).

**Acceptance:**
- Injection suite runs under `dotnet test` and produces pass/fail results.

---

### T05.E: (Optional) Content moderation pre-processing + metadata-only moderation events
**Scope:**
- Add a basic moderation filter (pattern-based is acceptable for the lab):
  - detect toxic/inappropriate content
  - if detected: block or down-rank tool-calling behavior and return a safe refusal
- Log moderation events as metadata only:
  - event type (moderation_blocked)
  - detected category count (no raw content)
  - correlation id + timestamp

**Files to touch (expected):**
- `src/AiSa.Application/*` (moderation helper)
- `src/AiSa.Application/*` (integration in tool-call path or security validation path)
- `tests/AiSa.Tests/*` (moderation unit tests)

**Minimal test:**
- Unit: moderation detects known toxic patterns and returns block decision without storing raw content.

**Acceptance:**
- Moderation never logs raw prompt/content.

---

### T05.F: Admin indicator + `docs/security.md` mapping updates
**Scope:**
- Update Admin panel UI (`AdminPanel.razor`) to show:
  - `Tool calling: enabled` or `disabled` (from config)
- Update `docs/security.md`:
  - extend the mitigations mapping to explicitly cover:
    - tool allow-list enforcement
    - tool input/output validation and redaction
    - audit logging boundaries (no raw payloads)
    - prompt injection test coverage for tool abuse

**Files to touch (expected):**
- `src/AiSa.Host/Components/AdminPanel.razor` (tool calling status row)
- `src/AiSa.Host/Components/AdminPanel.razor.css` (if layout tweaks required)
- `docs/security.md` (mitigation mapping update)

**Minimal test:**
- Manual: open Admin dialog and verify tool calling status matches `ToolCalling:Enabled`.
- Documentation review: `docs/security.md` clearly references the new controls.

**Acceptance:**
- Admin UI reflects tool-calling enablement.
- `docs/security.md` mapping aligns with implementation.

---

## Minimal Tests Per Sub-Task (Summary)
| Sub-Task | Test Type | Minimal Test |
|----------|-----------|---------------|
| T05.A | Unit + Integration | Parser unit test + tool call integration smoke |
| T05.B | Unit | Validator accepts valid args and blocks invalid/forbidden |
| T05.C | Unit | Output redaction + stable args-hash determinism |
| T05.D | Integration | 10-prompt injection suite (pass/fail) |
| T05.E | Unit | Moderation detection without raw logging |
| T05.F | Manual + Review | Admin status matches config + docs/security alignment |

---

## Risks & Open Questions
1. **Tool-call proposal format brittleness**: since the current `ILLMClient` returns only `string`, tool calls will likely be implemented via a deterministic markup/JSON-in-text protocol; real-provider compliance may require prompt tuning.
2. **Existing chat flow gating**: `ChatService` returns "I don't know..." when retrieval yields no context, so injection tests must use a retrieval mock that returns context.
3. **Audit logging leakage risk**: ensure hashing/redaction are applied before any log statement. If audit is implemented via structured logging scopes, tests should verify no raw payload fields are present.
4. **Args hashing stability**: must canonicalize args serialization (property ordering) to keep deterministic audit hashes and unit-test expectations.
5. **ADR gaps in repo snapshot**: the start-task template references additional ADR numbers (`0001`, `0003`), but this repo snapshot only contains certain ADR files under `docs/adr/`. We will align with the available ADRs (notably `0002`, `0004`, `0006`, `0007`) and with `docs/security.md`/`docs/architecture.md`.

## Proposed Branch Name
```
feature/T05-tool-calling-guardrails
```

## Commit Message Pattern
- `feat(T05.A): ...`
- `feat(T05.B): ...`
- `feat(T05.C): ...`
- `feat(T05.D): ...`
- `feat(T05.E): ...`
- `chore(T05.F): ...`

## Git Commands (DO NOT EXECUTE)
```bash
# Create branch
git checkout -b feature/T05-tool-calling-guardrails

# First commit placeholder (after T05.A)
# git add <files touched in T05.A>
# git commit -m "feat(T05.A): add tool-calling scaffolding, allow-list routing, and proposal parsing"
```

---

## Next Steps
1. After approval, wait for your message `Implement T05.A`.
2. When implementing, keep changes within ~300-400 LOC for that sub-task and keep the repo buildable.

