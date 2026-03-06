# MCP Integration Showcase (Documentation Blueprint)

## Purpose
Create a portfolio-grade chapter showing practical enterprise usage of MCP servers and tool calling, with a clean orchestrated flow that includes discovery, authorization, guardrails, confirmation, and auditability.

> Scope for this ticket: **documentation only** (no runtime implementation yet).

## Why this chapter matters
- Demonstrates real orchestration patterns beyond basic "LLM + prompt" usage.
- Shows clear separation of responsibilities between Chat UI, Orchestrator, Registry, IdP, and app-specific MCP servers.
- Highlights governance and safety controls that recruiters and architects expect in enterprise systems.

## Target demo narrative
User asks in chat: _"Chiudi i ticket vecchi del progetto A"_.

The system executes a controlled 2-step workflow:
1. Proposed tool call (not executed yet).
2. User confirmation before mutative execution.

## Reference flow (end-to-end)
1. **Chat request** (`POST /chat`) with user token.
2. **AuthN/AuthZ bootstrap** (token validation, user/tenant/scopes extraction).
3. **Discovery + routing** via MCP Registry for tenant-allowed providers.
4. **Toolset filtering** (fine-grained authorization, hide unauthorized tools from model).
5. **LLM call** with user prompt + filtered tools.
6. **Tool proposal** returned by model (structured call only).
7. **Gatekeeper stage** in orchestrator: schema validation, risk policy, audit event.
8. **Approval step** for mutative/high-impact action (`POST /confirm`).
9. **Execution stage** with OBO token exchange and `tools/call` on target MCP server.
10. **Final response** to UI (+ optional model rephrasing), constrained UI actions.

## Integration strategy for current playground

### 1) Keep Chat as primary entrypoint
The existing Chat + RAG area remains the natural UX entrypoint. MCP/tooling should be introduced as an additional execution mode in Chat, not as a disconnected feature.

### 2) Add a lightweight MCP toggle in Chat
For showcase simplicity, Chat should include a **light toggle** (for example: `Enable MCP Tools`) so MCP behavior can be turned on/off per request/session.

- Toggle **OFF**: current RAG flow only.
- Toggle **ON**: orchestrator may run discovery + tool proposal/approval flow.

This keeps the baseline demo stable and makes MCP behavior explicit to interviewers.

### 3) Add a dedicated chapter in navigation
A dedicated menu section (**MCP Integration**) explains architecture, flow, controls, auth model, and phased delivery while Chat hosts future runtime behavior.

### 4) Position vs existing Agent page
- **Chat + MCP mode**: single-turn or short multi-step business operations with explicit user confirmation.
- **Agent page**: longer autonomous loops with planner/step traces and stricter run limits.

### 5) Example provider set
For showcase clarity, define two example app MCP providers:
- **App1 Ticketing MCP**: `app1.tickets.listOld`, `app1.tickets.closeOld`.
- **App2 Messaging MCP**: `app2.email.sendSummary`.

This enables cross-app narrative: close tickets, then optionally send summary email.

## Lightweight auth model for showcase (aligned with current app style)
Keep auth intentionally simple and consistent with the rest of the playground:

- Assume a logged-in user context with a small set of claims:
  - `userId`, `tenantId`
  - `roles` (e.g. `User`, `Admin`)
  - `allowedMcpServers` (e.g. `app1`, `app2`)
- Tool discovery is filtered by `allowedMcpServers` + role scopes.
- If user is not enabled for a server, MCP tools from that server are not exposed to the model.
- Keep this lightweight in docs and UX copy; avoid heavy IAM complexity in this phase.

Example showcase statement:
> "User X is authenticated and enabled for App1 MCP tools, but not for App2."

## Non-functional controls to showcase
- Multi-tenant isolation (`tenantId` on registry discovery + operation ownership checks).
- Auth chain (`user token` -> `orchestrator` -> `OBO token` for app audience).
- Validation chain (tool allow-list, schema validation, risk policy classification).
- Approval orchestration with operation TTL and idempotency.
- Full observability (trace correlation from `/chat` to `tools/call`).

## Deliverables in this documentation package
- Backlog ticket: `docs/backlog/T16-mcp-orchestrator-chat-approvals.md`
- Implementation plan: `docs/backlog/plans/T16-plan.md`
- ADR: `docs/adr/0010-mcp-orchestrator-and-approval-gate.md`

## Suggested phased rollout (future implementation)
1. **Phase A**: Documentation + UI chapter (this scope).
2. **Phase B**: Chat toggle + mocked orchestrator flow (proposal + confirm UX, no real MCP calls).
3. **Phase C**: Real MCP registry + one App MCP server with lightweight authz filtering.
4. **Phase D**: OBO auth + second app + telemetry dashboards + eval scenarios.
