# T16 - MCP Orchestrator in Chat (Discovery + Gate + User Approvals)

## Goal
Define and then implement (in later phases) a practical MCP-driven orchestration flow inside the Chat experience, including tenant-aware tool discovery, policy gating, explicit user confirmation for mutative actions, and auditable execution.

## Scope (for this ticket)
- Documentation and architecture definition only.
- No runtime endpoint or orchestration code changes.

## Business scenario
User asks: **"Chiudi i ticket vecchi del progetto A"**.

Expected behavior:
1. Chat orchestrator identifies relevant MCP providers/tools.
2. LLM proposes a structured tool call.
3. Orchestrator validates and classifies risk.
4. User confirmation required before execution.
5. Orchestrator executes call against the correct app MCP server and reports result.

## Functional boundaries (target architecture)
- **Chat Orchestrator**: central control plane for auth context, discovery, gatekeeping, approvals, execution routing, and response shaping.
- **MCP Registry**: tenant-aware provider discovery.
- **App MCP Servers**: app-scoped tool execution with local auth and business logic.
- **Chat UI (Blazor)**: renders assistant messages + safe action cards/modals for confirmation.

## Key requirements
- Multi-tenant tool discovery and filtering.
- AuthN/AuthZ chain from user token to app-scoped execution token (OBO).
- Tool calls are proposals first, never blind execution.
- Mandatory confirmation for mutative/high-impact operations.
- Operation IDs bound to user+tenant, with TTL and replay protection.
- Structured audit trail for request, approval, execution, and outcome.

## Lightweight auth requirement (showcase-friendly)
To stay consistent with other playground sections, auth for this chapter should be intentionally light:
- Assume authenticated user context already available in Chat.
- Evaluate MCP access with simple claims/flags (e.g. `allowedMcpServers`, role/scope).
- Example: User A can use `app1` MCP tools, User B cannot.
- Hide non-authorized MCP providers/tools before prompting the model.

## Chat UX requirement
- Add a **toggle** in Chat to enable/disable MCP mode.
- MCP toggle OFF keeps default RAG-only behavior.
- MCP toggle ON allows discovery + proposal + approval flow.

## Suggested APIs (future)
- `POST /api/chat` -> may receive `enableMcp=true|false` and may return `pendingApproval` payload with `operationId`.
- `POST /api/chat/confirm` -> executes pending operation after ownership + TTL checks.
- `GET /api/mcp/providers` (internal) -> registry lookup by tenant.
- `POST /api/mcp/tools/discover` (internal) -> routed discovery for relevant providers.

## Deliverables
- ADR documenting architecture decisions and trade-offs.
- Detailed implementation plan and phased execution.
- UI chapter/menu item in portal linking this capability.

## Acceptance Criteria
- Documentation describes complete flow from user prompt to confirmed execution.
- Integration strategy with existing Chat + RAG is explicit.
- Guardrails, approval policies, audit, and auth/OBO model are documented.
- Lightweight auth baseline and Chat MCP toggle are explicitly documented.
- Backlog and ADR are consistent with existing architecture principles.

## DoD
- New ADR committed.
- New plan committed.
- New showcase document committed.
- Portal navigation includes MCP chapter entry.

## Demo (documentation)
1. Open "MCP Integration" in portal.
2. Review reference flow and linked docs.
3. Show Chat decision point via MCP toggle and lightweight auth examples.
