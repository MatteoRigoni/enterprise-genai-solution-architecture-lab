# T06 - Observability (OpenTelemetry + Dashboards + Runbooks) — Implementation Plan

## Task Overview
Portare il sistema in uno stato **operabile**: tracce correlate end-to-end, metriche esposte via OpenTelemetry (coerenti con Aspire Dashboard / OTLP e, in produzione, Azure Monitor), pagina **Observability** nel portale con riepiloghi **sicuri** (solo metadata) e link ai runbook. Completare il runbook **LLM degradation** e allineare `docs/quality.md` agli SLO dove richiesto dal task.

**Riferimenti obbligatori:** `docs/architecture.md` (Observability), `docs/observability-local.md`, `docs/adr/0004-telemetry-policy.md`, `docs/adr/0007-finops-budgeting.md` (solo per naming/interpretazione costi stimati, non per introdurre budget engine qui).

## Stato attuale (baseline)
- **ServiceDefaults** (`ConfigureOpenTelemetry`): ASP.NET Core + HttpClient + runtime metrics; exporter OTLP (Aspire) o Azure Monitor in produzione; sampling configurato.
- **Span applicativi parziali:** `chat.handle` / `chat.stream` (ChatEndpoints), `retrieval.query` (RetrievalService), `llm.generate` (ChatService), altri per eval/documenti.
- **ActivitySource** registrato come singleton `"AiSa.Host"` in `Program.cs`; OTel usa `AddSource(ApplicationName)` — verificare allineamento nome sorgente in implementazione.
- **Pagina Observability:** placeholder (“Coming soon”).
- **Runbook:** presenti `incident-latency.md` e `incident-cost-spike.md`; **manca** `incident-llm-degradation.md` (richiesto da T06).
- **ApiCallStore:** traccia chiamate API per sessione (base utile per UI, da estendere/integrare con “recent summaries” senza loggare contenuti sensibili).

---

## High-Level Plan (6 steps)
1. Registrare **Meter** e metriche core chat (richieste, errori, latenza) e collegarle al percorso `/api/chat` (e stream se in scope).
2. Arricchire **span** esistenti e aggiungere **tool span** dove manca; tag sicuri (durate, conteggi, hash/id) — niente prompt/raw docs.
3. Esporre metriche **token stimati / costo stimato** e **security_events** (blocchi tool, terminazioni “unsafe”) in linea con ADR-0004 e FinOps (solo contatori/metadata).
4. Implementare la **pagina Observability** (riepiloghi recenti + riepilogo eventi sicurezza + link runbook).
5. Aggiungere runbook **`incident-llm-degradation.md`** e verificare che i tre runbook siano referenziati dalla UI.
6. Aggiornare **`docs/quality.md`** (riferimento SLO / telemetria attesa) solo dove necessario per chiudere il DoD del task.

---

## Sub-Task Decomposition

### T06.A — Meter OpenTelemetry + metriche core chat (requests, errors, latency)
**Scope:**
- Definire un `Meter` (es. nome `AiSa.Chat` o `AiSa.Host`) e strumenti:
  - `chat_requests_total` (counter, label es. `outcome` o `http.status_class` se utile e non sensibile)
  - `chat_errors_total` (counter)
  - `chat_latency_ms` (histogram o export come gauge/distribution compatibile con OTLP)
- Instrumentare il flusso HTTP chat (minimal API) per iniziare/stopare il timer e incrementare i contatori in modo coerente con successo/errore.
- Verificare che `ActivitySource("AiSa.Host")` sia incluso in `AddOpenTelemetry().WithTracing(... AddSource(...))` (allineamento nome con `Program.cs`).

**File attesi (indicativi):** `AiSa.ServiceDefaults/Extensions.cs` o nuovo file dedicato metriche in `AiSa.Host`; `ChatEndpoints.cs` (o middleware dedicato sottile).

**Test minimi:**
- Test unit/integration che dopo una chiamata mock a `/api/chat` le metriche registrino almeno un incremento (pattern comune: `MeterListener` / `MetricCollector` in test, o verifica indiretta tramite endpoint di test interno se già presente nel repo).
- Test esistenti `dotnet test` senza regressioni.

**Acceptance:** metriche esportabili in dev via Aspire Dashboard; nessun dato sensibile nelle dimensioni.

---

### T06.B — Span retrieval / LLM / tool: correlazione e tag sicuri
**Scope:**
- Verificare catena **HTTP → chat.handle → retrieval.query → llm.generate** sotto un’unica trace; aggiungere/adattare **span `tool.execute`** (o nome equivalente) nel percorso tool-calling dopo T05.
- Aggiungere tag sicuri agli span LLM (es. `gen_ai.request.model` se disponibile, durata, **lunghezze** oppure range di token se derivabili dal provider senza loggare testo).
- `retrieval.query`: tag con `top_k`, durata, esito — **mai** testo query o chunk.

**File attesi:** `ChatService.cs`, `RetrievalService.cs`, router tool / handler Infrastructure.

**Test minimi:**
- Test che verifica presenza di `Activity` figli quando si usa `ActivitySource` in test harness (opzionale se già coperto da integrazione).
- Regression suite esistente chat/tool.

**Acceptance:** trace visibile in Aspire con span figli per RAG + LLM + tool; policy ADR-0004 rispettata.

---

### T06.C — Metriche estese: token, costo stimato, security_events
**Scope:**
- Estendere il modello di risposta LLM o il livello client per **usage** (token in/out) quando il provider lo espone; in assenza, usare **stime conservative** documentate (senso unico, no PII).
- Contatore `security_events_total` con dimensioni sicure (es. `reason=tool_blocked|input_rejected|...`) allineato a eventi già emessi da guardrail T05.
- Opzionale: metriche derivate da eval aggregate (come da T06 backlog) — **solo** se implementabile come export batch/placeholder senza ingrandire troppo lo scope (altrimenti defer a nota in plan).

**File attesi:** `ILLMClient` / implementazioni Azure e Mock, `ChatService`, punto centralizzato per cost estimate (config peso per token da `IOptions`).

**Test minimi:**
- Unit test su calcolo costo stimato da token fittizi (deterministico).
- Verifica incremento `security_events_total` su scenario di tool bloccato (test integrazione esistente o nuovo caso minimo).

**Acceptance:** dashboard mostra serie coerenti; nessun log di prompt o argomenti tool.

---

### T06.D — Pagina Observability (UI) + API di lettura metadata-only
**Scope:**
- Sostituire il placeholder con:
  - elenco **ultime richieste** (correlation id, timestamp, latenza, esito HTTP, route — **no** body)
  - riepilogo **security events** (conteggi o ultime voci metadata-only)
  - link a `docs/runbooks/incident-latency.md`, `incident-cost-spike.md`, `incident-llm-degradation.md` (path pubblici o route statiche mappate — decidere pattern coerente con Evaluations)
- Riutilizzare/estendere `IApiCallStore` o introdurre `IObservabilitySummaryStore` singleton con buffer globale bounded per demo.

**File attesi:** `Observability.razor` (+ code-behind/css se necessario), eventuale endpoint minimal API read-only, registrazione servizi.

**Test minimi:**
- Test componente o test integrazione leggero che la pagina risponda 200 e mostri link runbook (se il progetto ha pattern bUnit; altrimenti smoke test manuale documentato nel commit).

**Acceptance:** dopo N chiamate chat, la pagina mostra riepiloghi coerenti con ADR-0004.

---

### T06.E — Runbook LLM degradation + allineamento quality.md
**Scope:**
- Creare `docs/runbooks/incident-llm-degradation.md` (SLO, sintomi, fallback, rollback modello/indice, riferimento a eval e quality).
- Verificare contenuti di `incident-latency.md` e `incident-cost-spike.md` per link dalla UI e coerenza con metriche implementate.
- Aggiornare `docs/quality.md` con una riga/sezione che colleghi SLO alle metriche/trace (se mancante).

**Test minimi:** revisione markdown (link relativi validi); `dotnet build` / `dotnet test`.

**Acceptance:** tre runbook referenziati dalla Observability page; quality aggiornato senza contraddire ADR.

---

## Minimal Tests per Sub-Task (riepilogo)
| Sub-task | Test |
|----------|------|
| T06.A | MeterListener/metriche + test regressione |
| T06.B | Integrazione chat esistente + ispezione trace (manuale Aspire accettabile per DoD demo) |
| T06.C | Unit cost + integrazione security counter |
| T06.D | Smoke UI/API o test componente se presente |
| T06.E | Build + link check |

---

## Risks / Open Questions
1. **Allineamento `ActivitySource` name** tra `AddSource` e `"AiSa.Host"`: se disallineato, gli span custom non appaiono in sampling/export — da verificare per prima cosa in T06.A.
2. **Token/costo:** provider Mock potrebbe non esporre usage reale; serve strategia esplicita (stima vs. zero) per evitare metriche fuorvianti.
3. **Observability globale vs. per sessione:** T06 chiede “recent request summaries” — definire se **globali** (demo) o **per utente/sessione** (più realistico); impatto privacy ancora metadata-only ma va scelto chiaramente.
4. **Cardinality:** evitare label ad alta cardinalità (user id, correlation id come dimensione metrica).
5. **Metriche eval in tempo reale:** il backlog menziona precision/recall aggregati da eval — probabile **non** real-time; meglio documentare come “derived offline” se non implementato.

---

## Branch e commit
- **Branch proposto:** `feature/T06-otel-observability`
- **Pattern commit:** `feat(T06): ...`, `chore(T06): ...`, `fix(T06): ...` (con suffisso sub-task in messaggio se utile, es. `feat(T06.A): register chat metrics`)

---

## Git commands (non eseguire da agent in start-task; l’utente può lanciare)
```bash
git checkout main
git pull
git checkout -b feature/T06-otel-observability
# dopo il primo incremento utile:
git add docs/backlog/plans/T06-plan.md
git commit -m "chore(T06): add T06 observability implementation plan"
```

---

## Execution protocol
- Implementare **solo** su richiesta esplicita: `Implement T06.A`, `Implement T06.B`, …
- Limite indicativo: **300–400 LOC** per sub-task; repo sempre buildabile.
