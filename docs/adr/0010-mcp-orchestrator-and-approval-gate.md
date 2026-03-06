# ADR 0010: MCP Orchestrator in Chat with Approval Gate for Mutative Actions

## Status
Accepted

## Context
The playground already covers RAG chat and controlled agent loops, but it lacks a concrete enterprise-ready pattern for MCP server integration where the model can propose tool calls and the platform enforces a hard safety gate before execution.

We need a portfolio-quality architecture that demonstrates:
- tenant-aware tool discovery,
- explicit authorization filtering,
- confirmation workflows for mutative operations,
- app-scoped execution with proper token audience/scopes,
- auditable and observable end-to-end behavior.

A new requirement is to keep this showcase aligned with the rest of the project style:
- lightweight auth storytelling (not a full IAM program),
- explicit Chat-level toggle to turn MCP behavior on/off.

## Decision
Adopt a **central Chat Orchestrator** pattern for MCP tool usage:

1. **MCP mode is explicit in Chat** via request/session toggle (`enableMcp`), default OFF.
2. **Discover providers/tools dynamically** through a tenant-aware MCP Registry when MCP mode is ON.
3. **Filter tools pre-LLM** based on lightweight user auth context and policy.
4. **Treat model tool calls as proposals only**.
5. **Enforce a gatekeeper stage** for schema validation and risk policy checks.
6. **Require explicit user confirmation** for mutative/high-impact operations.
7. **Execute with OBO token exchange** against target app MCP servers.
8. **Return structured outcomes** to Chat UI with safe, allow-listed UI actions.

## Lightweight auth baseline (showcase)
To remain coherent with other sections, authorization is modeled with a small, understandable claim set:
- `userId`, `tenantId`
- `roles/scopes`
- `allowedMcpServers` (example: `app1`, `app2`)

If a server is not in `allowedMcpServers`, its tools are not discovered/exposed to the model.

## Rationale
- Keeps authorization and policy enforcement out of prompt instructions and inside deterministic backend controls.
- Avoids "silent side effects" from direct model-issued tool execution.
- Preserves user trust by making impactful operations explicit and reversible until confirmation.
- Keeps the chapter pragmatic and portfolio-friendly, without over-engineering identity complexity.

## Architecture implications
### New logical components
- `McpRegistryClient` (provider discovery per tenant).
- `ToolCatalogService` (routing + authz filtering).
- `ApprovalOperationStore` (pending operation state, TTL, ownership).
- `McpExecutionAdapter` (OBO + tools/call dispatch).

### Chat contract evolution
Chat contracts must support:
- request toggle (`enableMcp`),
- plain answer mode,
- tool proposal requiring approval (`pendingApproval`).

### UI implications
Chat page adds:
- MCP toggle (on/off),
- confirmation UX (modal/card) linked to `operationId` for policy-gated operations.

## Security and compliance implications
- Dual-layer authz: orchestrator filtering + app MCP local verification.
- Operation ownership checks (`userId`, `tenantId`) mandatory at confirm time.
- Replay prevention and expiration on pending operations.
- Audit events for request/proposal/approval/denial/execution/outcome.
- No raw sensitive payload persistence in telemetry.

## Observability implications
Minimum telemetry:
- trace continuity from `/chat` through discovery, gate, confirm, execution,
- counter for MCP mode usage,
- counters for approval required/confirmed/denied,
- denial/error metrics by reason class.

## Alternatives considered
1. **Direct model-to-tool execution** (rejected): too risky, weak control.
2. **Manual static tool registration only** (rejected): not scalable for multi-app tenants.
3. **Approval for every tool call** (partially rejected): safest but high UX friction; kept as policy option for regulated contexts.
4. **Heavy IAM-first design** (rejected for now): valuable later, too complex for this showcase phase.

## Consequences
### Positive
- Strong governance story for enterprise recruiter demos.
- Reusable orchestration pattern across multiple business apps.
- Clear extension path from existing Chat+RAG architecture.
- Clear UX control over MCP behavior through Chat toggle.

### Trade-offs
- Added orchestration complexity and state handling.
- Additional latency in discovery/gate/approval flow.
- Requires robust operation lifecycle management.

## Links
- `docs/mcp-integration-showcase.md`
- `docs/backlog/T16-mcp-orchestrator-chat-approvals.md`
- `docs/backlog/plans/T16-plan.md`
- `docs/adr/0006-agent-loop-constraints.md`
