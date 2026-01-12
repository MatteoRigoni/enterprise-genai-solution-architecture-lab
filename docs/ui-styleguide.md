# docs/ui-styleguide.md

# UI Styleguide – Fluent UI Blazor (Dark Premium Dashboard)

## Purpose
Define a **purely visual and stylistic contract** for the Blazor portal UI using **Fluent UI Blazor**.  
This document captures **look & feel only** (colors, spacing, surfaces, layout patterns).

> **Important**
> - Reference screenshots are **style inspiration only**
> - **No layout implies content, features, or navigation structure**
> - Texts, labels, and menu items in screenshots are **not authoritative**

This styleguide is **binding** for all UI-related implementation tasks.

---

## Visual Direction (High-level)
A **dark, premium, calm dashboard UI** characterized by:
- soft contrast (not pure black)
- rounded surfaces
- subtle gradients and glow
- strong typographic hierarchy
- breathable spacing
- modern SaaS/AI-product aesthetic

The UI must feel:
- professional
- enterprise-grade
- future-proof
- non-experimental

---

## Non-Negotiable Principles
1. **Consistency over creativity**
2. **Visual calmness** (no aggressive colors or motion)
3. **Accessibility first** (contrast, focus visibility)
4. **Token-driven styling only**
5. **Minimal custom CSS**, focused on layout and surfaces
6. **Dark/Light theme support** - MAI usare colori hardcoded, SEMPRE usare variabili token (vedi sezione "Dark/Light Theme Support")

---

## Layout Patterns (Abstract)
> These are **structural patterns**, not page definitions.

### Application Shell
- Fixed vertical sidebar surface
- Horizontal top bar surface
- Scrollable main content area

### Content Composition
- Large elevated surface (hero/banner-like)
- Grid of medium-sized surface cards
- Compact metric/summary surface cards
- Sections clearly separated by spacing, not lines

### Responsive Behavior
- Desktop-first layout
- Progressive collapse and stacking on smaller screens
- No layout breakage or visual noise on resize

---

## Component Philosophy (Fluent UI Blazor)
- Use Fluent components for **controls**
- Do **not** restyle Fluent internals aggressively
- Use custom CSS only to:
  - define layout containers
  - create surface cards/panels
  - apply glow/shadow effects

### Loading States (Centralized)
**Note:** The application uses a centralized loading service (`ILoadingService`) with a global overlay component (`LoadingOverlay`) that displays `FluentProgressRing` during async operations. See `docs/loading-service-guide.md` for usage patterns and best practices. All loading states should use this service instead of local loading flags.

### Toast Notifications (Centralized)
**Note:** The application uses a centralized toast notification service (`IToastNotificationService`) that wraps Fluent UI's `IToastService`. The service provides convenience methods (`ShowSuccess`, `ShowInfo`, `ShowWarning`, `ShowError`) with consistent timeout defaults based on intent severity. Inject `IToastNotificationService` in components to display user-facing notifications. `FluentToastProvider` is already integrated in `MainLayout`.

---

## CSS Architecture (Mandatory)
All custom styling must be isolated and token-driven.

> **⚠️ CRITICAL**: Prima di scrivere qualsiasi CSS, leggere la sezione **"Dark/Light Theme Support"** per capire come gestire correttamente i colori e i temi.

### File Structure
Place under `src/AiSa.Host/wwwroot/css/`:
tokens.css // design tokens (variables only) - contiene definizioni dark/light theme
layout.css // shell and layout containers
components.css // surfaces, cards, hero, utilities
app.css // imports + minimal overrides


No inline styles.  
No random hex values outside `tokens.css`.
**Tutti i colori devono usare variabili token** (vedi sezione "Dark/Light Theme Support").

---

## Design Tokens (CSS Variables)

### Backgrounds
- `--bg-0`: primary page background (dark, soft)
- `--bg-1`: sidebar / secondary background
- `--bg-2`: elevated surfaces (cards)
- `--bg-3`: highest elevation surface (hero/banner)

### Borders
- `--border-0`: default subtle border
- `--border-1`: hover / focus border

### Text
- `--text-0`: primary text
- `--text-1`: secondary text
- `--text-2`: muted text

### Accents
- `--accent-primary`: primary accent color
- `--accent-secondary`: optional secondary accent
- `--accent-tertiary`: optional tertiary accent

### Status
- `--success-0`
- `--warning-0`
- `--danger-0`

---

## Dark/Light Theme Support (CRITICAL)

> **⚠️ IMPORTANTE: Leggere questa sezione prima di qualsiasi modifica CSS**

L'applicazione supporta **dark e light theme** tramite toggle nella topbar. Il sistema è implementato tramite:

1. **Classi CSS**: `.theme-dark` e `.theme-light` vengono applicate a `:root`, `body`, e `html`
2. **Variabili CSS token**: Tutti i colori cambiano automaticamente in base al tema
3. **FluentUI BaseLayerLuminance**: Gestito programmaticamente nel `TopBar.razor`

### Regole OBBLIGATORIE

**❌ MAI fare:**
- Usare colori hardcoded (es: `#ffffff`, `#000000`, `rgba(255,255,255,0.8)`)
- Assumere che il tema sia sempre dark o sempre light
- Usare colori diretti nei CSS senza variabili token

**✅ SEMPRE fare:**
- Usare **SOLO** variabili CSS token (`--bg-0`, `--text-0`, `--border-0`, ecc.)
- Tutti i colori devono essere definiti tramite token in `tokens.css`
- I token cambiano automaticamente in base al tema (dark/light)

### Stilizzazione Componenti FluentUI Input

**✅ IMPORTANTE**: Le regole per **TUTTI i componenti input di FluentUI** sono **già globali** in `components.css` (sezione "GLOBAL FLUENTUI INPUT COMPONENTS THEME-AWARE STYLING"). 

Le regole si applicano automaticamente a:
- `FluentTextField` (text-field)
- `FluentTextArea` (text-area)
- `FluentNumberField` (number-field)
- `FluentSearch` (search)
- `FluentCombobox` (combobox)
- `FluentSelect` (select)
- E qualsiasi altro componente input di FluentUI

**NON serve aggiungere stili specifici** per ogni input - le regole globali si applicano automaticamente a tutti gli input nell'applicazione.

**Se necessario** (solo per casi speciali), si può fare override con selettori più specifici, ma **sempre usando variabili token**:

```css
/* Esempio: override solo se strettamente necessario */
.my-special-case fluent-text-field {
    --neutral-fill-input-rest: var(--bg-3) !important; /* Solo se serve un bg diverso */
}
```

**Regola generale**: Se un input non segue il tema, probabilmente c'è un override che la sovrascrive. Rimuovere l'override e lasciare che le regole globali funzionino.

### Esempio di Riferimento

Vedi `components.css` sezione "GLOBAL FLUENTUI INPUT COMPONENTS THEME-AWARE STYLING" per le regole globali applicate a tutti gli input.

### Verifica

Dopo ogni modifica CSS:
1. Testare in **dark theme** (default)
2. Testare in **light theme** (toggle nella topbar)
3. Verificare che tutti i colori siano leggibili e coerenti in entrambi i temi

---

## Spacing System (8px Grid)
- `--s-1`: 8px
- `--s-2`: 16px
- `--s-3`: 24px
- `--s-4`: 32px
- `--s-5`: 40px

Spacing must feel **generous and breathable**, never cramped.

---

## Radius System
- `--r-1`: small (≈10px)
- `--r-2`: medium (≈14px)
- `--r-3`: large (≈18px)

Use the same radius consistently across surfaces.

---

## Shadows & Glow
### Shadows
- `--shadow-1`: subtle elevation
- `--shadow-2`: card elevation

### Glow
- `--glow-primary`: soft radial glow
- `--glow-secondary`: optional variant

**Glow Rules**
- Extremely subtle
- Decorative only
- Used sparingly on:
  - background
  - large elevated surfaces
- Never on every card

---

## Typography (Visual Only)
- Clear hierarchy between:
  - large titles
  - section headers
  - body text
  - small labels
- Favor readability over density
- Avoid excessive font sizes or weights
- Use muted text for secondary information

---

## Interaction & Motion
- Transition duration: 120–180ms
- Easing: ease-out
- Hover effects: minimal
- Focus states: always visible

No:
- bouncing animations
- heavy parallax
- aggressive motion

---

## Reference Assets
All visual references must be stored under:



