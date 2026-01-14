# Observability con .NET Aspire Dashboard (Local Development)

## Panoramica

Questa guida descrive come utilizzare l'osservabilità locale con .NET Aspire Dashboard per visualizzare **traces** e **metrics** in tempo reale durante lo sviluppo, senza dipendere da console exporter.

## Architettura

- **Local Dev**: Traces e Metrics vengono esportati via **OTLP** verso Aspire Dashboard
- **Production/Azure**: Traces e Metrics vengono esportati verso **Application Insights / Azure Monitor**
- **Policy**: Nessun dato sensibile (PII, prompt raw, document text) viene esportato. Solo metadata, IDs, durations, counts.

## Avvio Rapido

### 1. Avviare AppHost

Avvia il progetto `AiSa.AppHost` come startup project:

```bash
dotnet run --project src/AiSa.AppHost/AiSa.AppHost.csproj
```

Oppure da Visual Studio:
- Imposta `AiSa.AppHost` come startup project
- Premi F5 o avvia il debugger

### 2. Aspire Dashboard

Quando AppHost si avvia:
- Si apre automaticamente la **Aspire Dashboard** nel browser (tipicamente `https://localhost:15000` o porta simile)
- La dashboard mostra:
  - **Resources**: Tutti i servizi registrati (es. `aisa-host`)
  - **Traces**: Traces HTTP in tempo reale
  - **Metrics**: Metriche runtime, HTTP, e custom

### 3. Verificare l'Esportazione

1. Nella Aspire Dashboard, vai alla sezione **Traces**
2. Esegui una richiesta HTTP (es. `POST /api/chat`)
3. Dovresti vedere:
   - Una trace per la richiesta HTTP `/api/chat`
   - Child spans (es. `chat.handle` se presente)
   - Tags sicuri: `correlation.id`, `http.method`, `http.status_code`, `chat.message.length`, `chat.response.length`
   - **NON** dovresti vedere: body content, prompt raw, document text

## Configurazione

### Environment Variables (impostate automaticamente da AppHost)

Quando avvii AppHost, le seguenti variabili d'ambiente vengono impostate automaticamente su `AiSa.Host`:

- `ASPIRE_ENABLED=true`: Abilita l'export OTLP verso Aspire Dashboard
- `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`: Endpoint OTLP (configurato automaticamente da Aspire)
- `OTEL_SERVICE_NAME=AiSa.Host`: Nome del servizio per OpenTelemetry Resource
- `ASPNETCORE_ENVIRONMENT=Development`: Ambiente di sviluppo

### Configurazione Manuale (se necessario)

Se avvii `AiSa.Host` direttamente (senza AppHost), puoi impostare manualmente:

```bash
$env:ASPIRE_ENABLED="true"
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
$env:OTEL_SERVICE_NAME="AiSa.Host"
$env:ASPNETCORE_ENVIRONMENT="Development"
```

## Cosa Aspettarsi nella Dashboard

### Traces

Per ogni richiesta HTTP `/api/*`, vedrai:

1. **HTTP Span** (automatico da AspNetCore instrumentation):
   - `http.method`: GET, POST, etc.
   - `http.status_code`: 200, 400, 500, etc.
   - `http.route`: `/api/chat`
   - `http.url`: URL completo (senza query params sensibili)

2. **Application Span** (custom, es. `chat.handle`):
   - `chat.message.length`: Lunghezza del messaggio (non il contenuto)
   - `chat.response.length`: Lunghezza della risposta (non il contenuto)
   - `correlation.id`: ID di correlazione per il request

3. **Child Spans** (se presenti):
   - `retrieval.query`: Query al vectorstore (solo metadata)
   - `llm.generate`: Chiamata LLM (solo token counts, duration, provider, model)
   - `tool.call`: Chiamata tool (solo tool name, status, duration)

### Metrics

Nella sezione **Metrics** della dashboard, vedrai:

- **AspNetCore Metrics**:
  - `http.server.request.duration`: Durata delle richieste HTTP
  - `http.server.active_requests`: Richieste attive
  - `http.server.request.count`: Conteggio richieste per status code

- **HttpClient Metrics**:
  - `http.client.request.duration`: Durata delle chiamate HTTP client
  - `http.client.request.count`: Conteggio chiamate HTTP client

- **Runtime Metrics**:
  - `process.cpu.time`: CPU usage
  - `process.memory.usage`: Memory usage
  - `dotnet.gc.collections`: Garbage collection stats

## Production / Azure

In produzione, quando `APPLICATIONINSIGHTS_CONNECTION_STRING` è impostata e l'ambiente è `Production`:

- L'export va automaticamente ad **Azure Monitor / Application Insights**
- **NON** viene usato OTLP exporter verso Aspire
- Le stesse policy di sicurezza (no PII) si applicano

Per abilitare Azure Monitor:

```bash
$env:APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=..."
$env:ASPNETCORE_ENVIRONMENT="Production"
```

## PII Guardrail Checklist

Verifica che non vengano esportati dati sensibili:

### ✅ Cosa DOVREBBE essere presente (sicuro):
- `correlation.id`: UUID generato
- `http.method`: GET, POST, etc.
- `http.status_code`: 200, 400, etc.
- `chat.message.length`: Numero intero
- `chat.response.length`: Numero intero
- `token.count.input`: Numero intero
- `token.count.output`: Numero intero
- `duration.ms`: Numero intero
- `provider`: "openai", "azure-openai", etc.
- `model`: "gpt-4", "gpt-3.5-turbo", etc.

### ❌ Cosa NON DOVREBBE essere presente (PII):
- `request.body`: Contenuto raw della richiesta
- `response.body`: Contenuto raw della risposta
- `prompt`: Testo del prompt
- `completion`: Testo della completion
- `document.text`: Contenuto del documento
- `document.chunk`: Chunk del documento
- `user.email`: Email dell'utente
- `user.name`: Nome dell'utente (non hashato)
- `query.text`: Testo della query (solo metadata come length)

### Come Verificare

1. **Aspire Dashboard**:
   - Apri una trace nella dashboard
   - Controlla tutti i **tags** e **attributes**
   - Verifica che non ci siano valori che contengano testo sensibile

2. **Logs** (se abilitati):
   - Cerca pattern come `"message": "..."` o `"body": "..."` nei log
   - Verifica che non ci siano email, nomi, o contenuti documentali

3. **Application Insights** (Production):
   - Vai a **Transaction Search** o **Logs**
   - Cerca traces con `customDimensions` o `properties`
   - Verifica che non ci siano dati sensibili

## Troubleshooting

### Non vedo traces nella dashboard

1. Verifica che `ASPIRE_ENABLED=true` sia impostato
2. Verifica che `OTEL_EXPORTER_OTLP_ENDPOINT` sia corretto
3. Controlla i log di `AiSa.Host` per errori di connessione OTLP
4. Assicurati che AppHost sia avviato e la dashboard sia accessibile

### Vedo solo HTTP spans, non application spans

1. Verifica che `ActivitySource` sia registrato: `builder.Services.AddSingleton(new ActivitySource("AiSa.Host"))`
2. Verifica che il source sia aggiunto in OpenTelemetry: `tracing.AddSource("AiSa.Host")` (già fatto in ServiceDefaults)
3. Controlla che gli endpoint usino `activitySource.StartActivity(...)`

### Console Exporter non funziona

Il Console Exporter è **disabilitato di default** per evitare log verbosi. Per abilitarlo (solo per debug):

```bash
$env:OTEL_CONSOLE_EXPORTER="true"
```

## Riferimenti

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [ADR-0004: Telemetry Policy](../adr/0004-telemetry-policy.md)
- [T06: Observability Task](../backlog/T06-otel-observability.md)
