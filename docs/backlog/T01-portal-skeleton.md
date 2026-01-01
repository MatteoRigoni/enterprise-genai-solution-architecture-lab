# T01 - Blazor Portal Skeleton + Chat (Mock LLM)

## Goal
Have a working portal with navigation and a Chat page calling /api/chat, using a mock LLM.

## Scope
- Blazor portal layout with left nav menu (Chat, Agent, Documents, Evaluations, Observability, Cost, Governance, Security/Compliance, Admin).
- Chat page with message list, input, send button.
- Minimal API: POST /api/chat
- Mock LLM provider: deterministic response.

## Acceptance Criteria
- UI loads, menu navigates to pages (placeholders ok).
- Chat flow works end-to-end: user message -> API -> mock response -> UI displays.
- API returns a correlation id in response.
- Telemetry: trace span "chat.request" with duration and success/failure.
- Test: API handler returns deterministic output for input "hello".

## Files / Areas
- src/AiSa.Host: layout, pages, api endpoints
- src/AiSa.Application: Chat use case interface
- src/AiSa.Infrastructure: MockLLMClient
- tests/AiSa.Tests

## DoD
- Portal nav implemented
- Chat works
- Unit test added

## Demo
1) Open portal -> Chat
2) Type "hello" -> send
3) See response "MOCK: hello ..." and correlation id visible in UI

## Sources (passive)
- Microsoft Learn: Blazor fundamentals + routing/layout
- YouTube: “Blazor .NET 8 Web App tutorial”
- Docs: ASP.NET Core Minimal APIs basics
