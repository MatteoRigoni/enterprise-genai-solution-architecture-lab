# T01 - Portal Skeleton + Chat (Mock LLM) - Implementation Plan

## Task Overview
Build a working Blazor portal with navigation and a Chat page that calls `/api/chat` using a mock LLM provider.

## High-Level Plan (6 Steps)

1. **UI Foundation & Styling Setup** - Configure Fluent UI Blazor, create CSS architecture (tokens, layout, components), style application shell
2. **Navigation & Placeholder Pages** - Set up left navigation menu with all required items, create placeholder pages with consistent styling
3. **Chat UI** - Build Chat page with message list, input field, and send button following styleguide
4. **Application Layer** - Define Chat use case interface and DTOs
5. **Infrastructure Layer** - Implement MockLLMClient with deterministic responses
6. **API Endpoint & Telemetry** - Create POST `/api/chat` endpoint with OpenTelemetry tracing and correlation ID

## Sub-Task Decomposition

### T01.A0: UI Foundation & Styling Setup
**Scope:**
- Add Fluent UI Blazor NuGet package to `AiSa.Host`
- Create CSS architecture following styleguide:
  - `wwwroot/css/tokens.css` - Design tokens (CSS variables for colors, spacing, radius, shadows)
  - `wwwroot/css/layout.css` - Application shell layout (sidebar, top bar, main content)
  - `wwwroot/css/components.css` - Surface cards, hero/banner styles, utilities
  - `wwwroot/css/app.css` - Imports + minimal overrides (update existing)
- Configure Fluent UI Blazor in `Program.cs` and `App.razor`
- Style `MainLayout.razor` to match dark premium dashboard pattern:
  - Fixed vertical sidebar surface (dark, soft contrast)
  - Horizontal top bar surface
  - Scrollable main content area
- Reference `docs/ui-reference/dashboard-reference.png` for visual inspiration
- Follow clean architecture: CSS isolated in wwwroot, no inline styles

**Files to touch:**
- `src/AiSa.Host/AiSa.Host.csproj` (add Fluent UI Blazor package)
- `src/AiSa.Host/Program.cs` (register Fluent UI services)
- `src/AiSa.Host/Components/App.razor` (add Fluent UI CSS/JS references)
- `src/AiSa.Host/wwwroot/css/tokens.css` (new)
- `src/AiSa.Host/wwwroot/css/layout.css` (new)
- `src/AiSa.Host/wwwroot/css/components.css` (new)
- `src/AiSa.Host/wwwroot/app.css` (update imports)
- `src/AiSa.Host/Components/Layout/MainLayout.razor` (update structure and classes)
- `src/AiSa.Host/Components/Layout/MainLayout.razor.css` (update styling)

**Design Tokens to implement:**
- Backgrounds: `--bg-0`, `--bg-1`, `--bg-2`, `--bg-3` (dark premium palette)
- Borders: `--border-0`, `--border-1`
- Text: `--text-0`, `--text-1`, `--text-2`
- Accents: `--accent-primary`, `--accent-secondary`, `--accent-tertiary`
- Status: `--success-0`, `--warning-0`, `--danger-0`
- Spacing: `--s-1` through `--s-5` (8px grid)
- Radius: `--r-1`, `--r-2`, `--r-3`
- Shadows: `--shadow-1`, `--shadow-2`
- Glow: `--glow-primary` (subtle, decorative only)

**Minimal test:**
- Manual: Application loads with dark theme, sidebar visible, layout matches styleguide pattern

**Acceptance:**
- Fluent UI Blazor configured and working
- CSS architecture in place (4 files: tokens, layout, components, app)
- MainLayout follows dark premium dashboard pattern
- All design tokens defined and used
- No inline styles, all styling via CSS classes

---

### T01.A: Navigation Menu + Placeholder Pages
**Scope:**
- Update `NavMenu.razor` with all 9 navigation items (Chat, Agent, Documents, Evaluations, Observability, Cost, Governance, Security/Compliance, Admin)
- Style navigation menu following styleguide:
  - Use Fluent UI components where appropriate
  - Dark sidebar styling with proper contrast
  - Hover/focus states visible
  - Consistent spacing and typography
- Create placeholder pages for all non-Chat pages with consistent styling:
  - Use elevated surface cards (following components.css patterns)
  - "Coming soon" or page title with proper typography hierarchy
  - Consistent spacing and layout
- Ensure navigation works and routes correctly
- Update existing pages (Home, Counter, Weather) to match new styling or remove if not needed

**Files to touch:**
- `src/AiSa.Host/Components/Layout/NavMenu.razor` (update with all items + styling)
- `src/AiSa.Host/Components/Layout/NavMenu.razor.css` (update styling)
- `src/AiSa.Host/Components/Pages/Agent.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Documents.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Evaluations.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Observability.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Cost.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Governance.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/SecurityCompliance.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Admin.razor` (new, styled placeholder)
- `src/AiSa.Host/Components/Pages/Home.razor` (update styling to match styleguide)

**Minimal test:**
- Manual: Navigate to each page, verify menu items work, verify consistent styling

**Acceptance:**
- All 9 menu items visible and clickable
- Navigation menu styled according to styleguide (dark sidebar, proper contrast)
- Each placeholder page loads without errors and follows consistent styling
- All pages use design tokens (no hardcoded colors/spacing)

---

### T01.B: Chat Page UI
**Scope:**
- Create `Chat.razor` page with:
  - Message list (display user messages and assistant responses)
  - Input field for user messages (using Fluent UI components)
  - Send button (using Fluent UI components)
  - Display correlation ID when response received
- Style following styleguide and ui-reference:
  - Use elevated surface cards for message containers
  - Proper typography hierarchy
  - Generous spacing (8px grid system)
  - Rounded surfaces (using radius tokens)
  - Subtle shadows for elevation
  - Dark premium aesthetic
  - Fluent UI components for controls (TextField, Button)
- Follow clean architecture: component-specific CSS in Chat.razor.css, use design tokens

**Files to touch:**
- `src/AiSa.Host/Components/Pages/Chat.razor` (new, with Fluent UI components)
- `src/AiSa.Host/Components/Pages/Chat.razor.css` (component-specific styling using tokens)

**Minimal test:**
- Manual: Page loads, input field and send button visible, styling matches styleguide

**Acceptance:**
- Chat page renders correctly with dark premium styling
- Input and send button functional (UI only, no API call yet)
- Uses Fluent UI components for controls
- Follows design tokens (no hardcoded values)
- Message list area properly styled with elevated surfaces

---

### T01.C: Application Layer - Chat Use Case Interface
**Scope:**
- Define `IChatService` interface in `AiSa.Application`
- Define DTOs:
  - `ChatRequest` (message, correlationId)
  - `ChatResponse` (response, correlationId)
- Follow ADR-0002 (provider-agnostic interface pattern)

**Files to touch:**
- `src/AiSa.Application/IChatService.cs` (new)
- `src/AiSa.Application/Models/ChatRequest.cs` (new)
- `src/AiSa.Application/Models/ChatResponse.cs` (new)

**Minimal test:**
- Unit test: Verify DTOs can be instantiated with required properties

**Acceptance:**
- Interface defined with method signature matching requirements
- DTOs include correlationId fields

---

### T01.D: Infrastructure Layer - MockLLMClient
**Scope:**
- Implement `MockLLMClient` in `AiSa.Infrastructure`
- Implement `ILLMClient` interface (per ADR-0002)
- Deterministic response: for input "hello", return "MOCK: hello ..." (with some suffix)
- Return correlation ID in response

**Files to touch:**
- `src/AiSa.Infrastructure/ILLMClient.cs` (new, interface)
- `src/AiSa.Infrastructure/MockLLMClient.cs` (new, implementation)
- `src/AiSa.Infrastructure/Models/LLMRequest.cs` (new, if needed)
- `src/AiSa.Infrastructure/Models/LLMResponse.cs` (new, if needed)

**Minimal test:**
- Unit test: `MockLLMClient.GenerateAsync("hello")` returns deterministic response containing "MOCK: hello"

**Acceptance:**
- MockLLMClient implements ILLMClient
- Returns deterministic response for "hello" input
- Response format matches expected pattern

---

### T01.E: API Endpoint + Telemetry Integration
**Scope:**
- Create POST `/api/chat` minimal API endpoint in `Program.cs`
- Wire up `IChatService` → `MockLLMClient`
- Add OpenTelemetry tracing:
  - Create span named "chat.request"
  - Record duration
  - Record success/failure status
- Return correlation ID in response
- Follow ADR-0004 (no raw user prompts in logs, only metadata)

**Files to touch:**
- `src/AiSa.Host/Program.cs` (add API endpoint, service registration, OpenTelemetry setup)
- `src/AiSa.Application/ChatService.cs` (new, implements IChatService)
- `src/AiSa.Host/AiSa.Host.csproj` (add OpenTelemetry packages if needed)

**Minimal test:**
- Integration test: POST to `/api/chat` with "hello" returns deterministic response with correlation ID
- Verify telemetry span is created (can check via OpenTelemetry exporter or manual inspection)

**Acceptance:**
- API endpoint responds correctly
- Correlation ID present in response
- Telemetry span "chat.request" created with duration and status

---

### T01.F: Chat UI Integration + End-to-End Test
**Scope:**
- Wire up Chat.razor to call `/api/chat` endpoint
- Display response in message list
- Show correlation ID in UI
- Add unit test for API handler (deterministic "hello" response)

**Files to touch:**
- `src/AiSa.Host/Components/Pages/Chat.razor` (add HTTP client call)
- `tests/AiSa.Tests/ChatApiTests.cs` (new)

**Minimal test:**
- Unit test: API handler returns expected response for "hello" input
- Manual: End-to-end flow: type "hello" → send → see response and correlation ID

**Acceptance:**
- Chat flow works end-to-end
- Unit test passes
- Correlation ID visible in UI

---

## Minimal Tests Per Sub-Task

| Sub-Task | Test Type | Test Description |
|----------|-----------|-------------------|
| T01.A0 | Manual | Application loads with dark theme, layout matches styleguide |
| T01.A | Manual | Navigate to all 9 pages, verify menu works, verify consistent styling |
| T01.B | Manual | Chat page renders with proper styling, input/send visible |
| T01.C | Unit | DTOs instantiate correctly |
| T01.D | Unit | MockLLMClient.GenerateAsync("hello") returns deterministic response |
| T01.E | Integration | POST /api/chat with "hello" returns correct response + correlation ID |
| T01.F | Unit | API handler test for "hello" input |
| T01.F | Manual | E2E: user message → API → response → UI display |

---

## Risks & Open Questions

### Risks
1. **OpenTelemetry setup complexity** - May need to configure exporters/collectors. Mitigation: Start with console exporter for dev, minimal setup.
2. **Blazor HTTP client configuration** - Need to ensure CORS and HTTP client properly configured. Mitigation: Use built-in HttpClient injection.
3. **Correlation ID generation** - Need consistent ID generation strategy. Mitigation: Use `Activity.Current?.Id` or `Guid.NewGuid()`.
4. **Fluent UI Blazor setup** - Package version compatibility and configuration. Mitigation: Use latest stable version, follow official docs.
5. **CSS architecture complexity** - Ensuring tokens are used consistently. Mitigation: Strict review, no inline styles policy.
6. **Visual consistency** - Matching ui-reference mockup exactly. Mitigation: Use as inspiration, focus on styleguide principles (tokens, spacing, dark theme).

### Open Questions
1. Should correlation ID be generated client-side or server-side? **Decision: Server-side** (more reliable, ensures trace correlation)
2. Should we use Fluent UI Blazor components immediately or start simple? **Decision: Yes, implement full Fluent UI styling from T01.A0** (required per styleguide)
3. Telemetry backend: console exporter sufficient for T01? **Decision: Yes** (console exporter for dev, proper backend in T06)
4. Should we remove default Blazor template pages (Counter, Weather)? **Decision: Update Home page styling, remove or update Counter/Weather to match styleguide**
5. CSS organization: component-scoped CSS vs global? **Decision: Hybrid** (tokens/layout/components global, page-specific in .razor.css files)

---

## Branch & Commit Strategy

### Branch Name
```
feature/T01-portal-skeleton
```

### Commit Message Pattern
- `feat(T01.A0): setup Fluent UI Blazor and CSS architecture`
- `feat(T01.A): add navigation menu and styled placeholder pages`
- `feat(T01.B): add chat page UI with Fluent styling`
- `feat(T01.C): add chat service interface and DTOs`
- `feat(T01.D): implement mock LLM client`
- `feat(T01.E): add /api/chat endpoint with telemetry`
- `feat(T01.F): wire up chat UI and add tests`

---

## Git Commands (DO NOT EXECUTE)

```bash
# Create branch
git checkout -b feature/T01-portal-skeleton

# First commit (after T01.A0)
git add src/AiSa.Host/AiSa.Host.csproj src/AiSa.Host/Program.cs src/AiSa.Host/Components/App.razor src/AiSa.Host/wwwroot/css/*.css src/AiSa.Host/Components/Layout/MainLayout.razor*
git commit -m "feat(T01.A0): setup Fluent UI Blazor and CSS architecture"

# Second commit (after T01.A)
git add src/AiSa.Host/Components/Layout/NavMenu.razor* src/AiSa.Host/Components/Pages/*.razor*
git commit -m "feat(T01.A): add navigation menu and styled placeholder pages"
```

---

## Architecture Compliance Check

✅ **ADR-0001**: Single Host (API + Blazor Portal) - Compliant (using AiSa.Host for both)
✅ **ADR-0002**: Provider-agnostic LLM interface - Compliant (ILLMClient interface, MockLLMClient implementation)
✅ **ADR-0004**: Telemetry + no-PII logging - Compliant (trace spans, no raw prompts in logs)
✅ **Architecture**: AiSa.Host serves Blazor UI and minimal APIs - Compliant
✅ **Security**: No secrets in config, no raw prompts in logs - Compliant

---

## Dependencies & Prerequisites

- .NET 10 SDK installed
- Blazor Server components working (already in place)
- Fluent UI Blazor NuGet package (to be added in T01.A0)
- OpenTelemetry packages (to be added in T01.E)
- Reference: `docs/ui-reference/dashboard-reference.png` (visual inspiration)
- Reference: `docs/ui-styleguide.md` (binding styleguide)

---

## Verification Commands (Final)

After all sub-tasks complete:

```bash
# Build
dotnet build

# Run
dotnet run --project src/AiSa.Host

# Run tests
dotnet test

# Manual verification
# 1. Open browser to https://localhost:5001 (or configured port)
# 2. Navigate to Chat page
# 3. Type "hello" and send
# 4. Verify response contains "MOCK: hello" and correlation ID is displayed
```

---

## Clean Architecture Compliance

### CSS Architecture (Clean Separation)
- **Presentation Layer (Host)**: All CSS files in `wwwroot/css/`
- **Design Tokens**: Centralized in `tokens.css` (single source of truth)
- **Layout Concerns**: Isolated in `layout.css` (shell structure)
- **Component Styles**: Isolated in `components.css` (reusable patterns)
- **Page-Specific**: Scoped in `.razor.css` files (component isolation)
- **No Inline Styles**: Enforced policy (maintainability)

### Component Organization
- **Layout Components**: `Components/Layout/` (shell, navigation)
- **Page Components**: `Components/Pages/` (routeable pages)
- **Shared Components**: Future extension point
- **Styling**: Each component can have `.razor.css` for scoped styles

### Dependency Flow
- **Host → Application → Infrastructure**: Service registration follows clean architecture
- **CSS Dependencies**: `app.css` imports tokens → layout → components (cascade)
- **Fluent UI**: External dependency, used via components, not restyled aggressively

---

## Next Steps

After this plan is approved:
1. Wait for "Implement T01.A0" message
2. Implement only T01.A0 (UI Foundation)
3. Provide modified files + verification commands + commit message
4. Repeat for each sub-task sequentially (T01.A0 → T01.A → T01.B → ...)

