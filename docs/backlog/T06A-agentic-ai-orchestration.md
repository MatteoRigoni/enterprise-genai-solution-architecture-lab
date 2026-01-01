# T06A - Agentic AI Orchestration (Planner + Loop + Memory) with Guardrails

## Goal
Add an agent mode that can solve multi-step requests via planning + safe tool execution.

## Scope
- Endpoint: POST /api/agent/run
- Agent loop:
  - creates a plan (steps)
  - executes step-by-step with tool calls
  - stops on success or max steps/time
- Memory:
  - short-lived session memory (summary only)
  - store only safe metadata (no raw docs, no PII)
- Portal:
  - Agent page OR toggle in Chat (prefer dedicated Agent page)
  - show plan + step trace (tool called, outcome, time)

## Guardrails
- MaxSteps (default 5)
- MaxToolCalls (default 5)
- MaxTokensPerRun (budget)
- Tool allow-list (reuse T05)
- Strict tool input/output validation

## Observability
- One trace per agent run with child spans per step and tool call.
- Metrics:
  - agent_runs_total
  - agent_runs_success_total
  - agent_steps_total
  - agent_tool_errors_total

## Evaluation
- Agent dataset (10–20 tasks):
  - "Check order 123 and open ticket if delayed"
  - "Summarize doc X and create a support ticket with key points"
- Measure:
  - success rate
  - average steps
  - cost per run
- Add ADR: constraints + stop conditions.

## Acceptance Criteria
- Agent executes multi-step requests reliably.
- Plan is visible in UI.
- Agent stops safely on loops/timeouts.
- Agent eval report exists and repeatable.

## Files / Areas
- src/AiSa.Application: Agent orchestration service
- src/AiSa.Infrastructure: planner + memory store (safe)
- src/AiSa.Host: endpoint + Agent page
- eval/: agent dataset + harness extension
- docs/adr/0006-agent-loop-constraints.md

## DoD
- Guardrails enforced
- Observability spans for steps
- Basic agent eval integrated (even manual run)

## Demo
1) Portal -> Agent
2) Ask: "Check order 123; if delayed open a ticket"
3) UI shows plan + steps + final outcome

## Sources (passive)
- YouTube: “What are AI agents really? (tool-calling vs agent loop)”
- Docs: LangGraph / AutoGen concepts (planner, state, tool routing)
- OWASP: agent/tool risks & mitigations (prompt injection, tool abuse)
