# T16 - MCP Orchestrator in Chat with Approval Gate - Implementation Plan

## Task overview
Design and introduce a production-style MCP orchestration capability in Chat while preserving current RAG behavior and governance constraints. This plan defines phased implementation after documentation approval.

## High-level plan (8 steps)
1. **Chat toggle contract** to explicitly enable/disable MCP behavior.
2. **Lightweight auth profile** aligned with existing playground sections.
3. **MCP discovery layer** (registry lookup + routing heuristics by intent/domain).
4. **Tool policy gate** (allow-list + schema validation + risk classification).
5. **Approval orchestration** (operation store, TTL, ownership checks, idempotency).
6. **Execution adapter** for app MCP servers with OBO token exchange.
7. **UI integration in Chat** (approval cards/modal + confirm/deny UX).
8. **Observability + evals** for approval rate, tool success/failure, and safety events.

## Sub-task decomposition

### T16.A - Chat contract extension + MCP toggle
- Extend chat request/response contract with explicit MCP mode:
  - request: `enableMcp: bool` (default `false`)
  - response: optional `pendingApproval` block (`operationId`, `summary`, `riskLevel`, `expiresAt`, `preview`).
- Preserve backward compatibility for normal RAG text responses.

### T16.B - Lightweight auth profile
- Reuse existing user context style from the app and avoid heavy IAM redesign.
- Define minimal claims/flags for MCP filtering:
  - `tenantId`, `roles/scopes`, `allowedMcpServers`.
- Add simple authz rule examples (e.g., user enabled for App1 only).

### T16.C - Registry and discovery
- Add internal service to query MCP registry by `tenantId`.
- Add lightweight routing strategy (keyword/domain mapping + fallback broad lookup).
- Fetch `tools/list` only from relevant providers when possible.

### T16.D - Fine-grained authorization and filtering
- Filter discovered tools by user scopes/roles and `allowedMcpServers`.
- Ensure unauthorized tools are never sent to model context.
- Record denial reason (auditable metadata only).

### T16.E - Gatekeeper and approval policy
- Validate tool args against strict schemas.
- Classify operation type (`read`, `mutative`, `bulk`, `high-impact`).
- Force approval for mutative/high-impact classes.
- Persist pending operation state with TTL and user+tenant binding.

### T16.F - Confirm endpoint
- Add `POST /api/chat/confirm` endpoint with `operationId` and decision.
- Validate ownership, TTL, and one-time execution semantics.
- Emit audit events for confirm/deny paths.

### T16.G - MCP execution adapter
- Execute `tools/call` on target app MCP server.
- Perform token exchange for app audience and required scopes.
- Normalize tool result for chat response rendering.

### T16.H - UI and quality gates
- Add Chat UI toggle (`Enable MCP Tools`) and approval interaction pattern (modal/card).
- Add metrics/traces:
  - `mcp_mode_enabled_total`
  - `mcp_discovery_latency_ms`
  - `tool_calls_total`
  - `tool_calls_denied_total`
  - `approval_required_total`
  - `approval_confirmed_total`
- Add eval scenarios for:
  - correct toggle behavior (off = no discovery/tool path),
  - correct approval prompting,
  - unauthorized tool suppression,
  - successful confirm + execution.

## Risks and mitigations
- **Risk:** Tool explosion in large tenants.  
  **Mitigation:** routing heuristics + capped provider fan-out.
- **Risk:** Approval fatigue.  
  **Mitigation:** policy tiers, batched summaries, and clear risk signals.
- **Risk:** Security drift between orchestrator and app MCP servers.  
  **Mitigation:** defense-in-depth with both central and local auth checks.
- **Risk:** Chat UX confusion between RAG and MCP behavior.  
  **Mitigation:** explicit MCP toggle and clear status copy in UI.

## Proposed commit strategy (future)
- `feat(T16.A): extend chat contracts with MCP toggle and pending approvals`
- `feat(T16.B): add lightweight MCP auth profile and filtering rules`
- `feat(T16.C/T16.D): add tenant-aware MCP discovery and authz tool filtering`
- `feat(T16.E/T16.F): add approval gate and confirm endpoint`
- `feat(T16.G): execute MCP tool calls with OBO token exchange`
- `feat(T16.H): add chat toggle UX and telemetry/eval coverage`

## Demo story (future runtime)
1. User opens Chat and enables MCP toggle.
2. User asks to close old tickets.
3. Assistant returns confirmation request with impacted count preview.
4. User confirms.
5. Orchestrator executes via app MCP and returns human-readable outcome + action suggestion.
