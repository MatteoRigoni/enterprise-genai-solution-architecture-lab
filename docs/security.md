# Security

## Principles
- No secrets in config files
- Key Vault + managed identity (later task)
- Tool calling allow-list + strict validation
- Prompt injection tests maintained
- Agent loop bounded (max steps/time/tokens)
- Content moderation for toxic/inappropriate inputs
- Audit trail for all tool calls and agent decisions

## Threat Model

| Threat | Impact | Mitigation | Residual Risk |
|--------|--------|------------|---------------|
| Prompt injection | System override, data exfiltration | Input validation, allow-list tools, bounded agent loops, injection tests | Low (tests + guardrails) |
| Data exfiltration via tool outputs | PII/secrets leaked | Tool output validation, size limits, redaction, no raw logs | Low (validation + redaction) |
| Excessive costs / abuse | Budget exceeded | Budget thresholds, degradation tiers, per-user limits | Low (ADR-0007) |
| Agent loop runaway | Availability risk, cost explosion | MaxSteps/MaxTokens/MaxRuntime, non-progress detection | Low (ADR-0006) |
| Toxic/inappropriate content | Reputation risk, compliance | Content moderation filter, logging | Medium (filter may have false positives) |
| Unauthorized tool access | Privilege escalation | Allow-list enforcement, role-based access | Low (strict allow-list) |
| Rate limiting bypass | Service abuse, DoS | Rate limiting per user/IP, token bucket algorithm | Low (implemented) |
| File upload attacks | Malicious content ingestion | File type validation, content scanning, name sanitization | Low (validation + scanning) |

## Implemented Security Controls

### Input Sanitization

**Prompt Injection Prevention**:
- Pattern detection per comuni tecniche di injection:
  - "ignore previous instructions"
  - "system:"
  - "### instruction"
  - "you are now"
  - "act as if"
  - "override", "bypass", "jailbreak"
- Sanitizzazione automatica di pattern pericolosi
- Logging di tentativi di injection (metadata only, no PII)
- Configurazione: `Security:AllowDetectedThreats` (default: true, log only)

**File Name Sanitization**:
- Rimozione caratteri pericolosi: `< > : " / \ | ? * \0`
- Normalizzazione nomi file
- Limitazione lunghezza (max 255 caratteri)

### Tool calling (allow-list and guardrails)

**Scope (lab / T05)**:
- Chat can optionally parse LLM-emitted `<tool_call>{...}</tool_call>` proposals and route only **allow-listed** tool names via `IToolRegistry`.
- **Config** (see `appsettings` / environment):
  - `ToolCalling:Enabled` — feature toggle (default off).
  - `ToolCalling:MaxToolCallsPerRequest` — hard cap per chat turn.
  - `ToolCalling:MaxToolOutputCharacters` — max length of tool output after redaction before returning to the user.

**Mitigations**:
| Control | Purpose |
|---------|---------|
| Allow-list (`IToolRegistry`) | Unknown or disallowed tool names are rejected; handlers never run. |
| Per-tool input validation (`IToolInputValidator`) | Schema, length, and allowed-character rules; invalid args never reach handlers. |
| Output sanitization | Truncation and pattern redaction on tool outputs before user-visible response. |
| Metadata-only audit | Structured `ToolProposalAudit` logs: tool name, args hash (SHA-256 of canonical JSON), outcome, correlation id; optional sanitized length / redaction counts for executed tools — **no** raw prompts, args, or tool outputs in logs. |
| Injection regression tests | `ToolInjectionTests` (integration) exercise blocked tools, bad args, and oversized output under the mock LLM. |

**Residual risk**: Real models may emit malformed or adversarial markup; reliance on prompt instructions plus server-side validation remains the primary defense.

### Rate Limiting

**Implementation**:
- Token bucket algorithm per rate limiting
- Limiti configurabili per endpoint:
  - Chat: 10 richieste/minuto per utente/IP
  - Document Upload: 5 uploads/minuto per utente/IP
- Identificazione: User ID (se autenticato) o IP address
- Response: `429 Too Many Requests` con header `Retry-After`

**Configurazione**:
```json
{
  "Security": {
    "RateLimiting": {
      "ChatRequestsPerMinute": 10,
      "DocumentUploadsPerMinute": 5
    }
  }
}
```

### Audit Logging

**Structured Logging**:
- Tutte le operazioni critiche sono loggate con:
  - User ID (non-PII)
  - Action (chat.request, document.upload, feedback.submit)
  - Success/failure
  - Correlation ID
  - Timestamp
  - Metadata aggiuntiva (no PII)

**Events Logged**:
- Chat requests (success/failure, message length, citation count)
- Document uploads (document ID, chunk count, file size)
- Document updates (old/new document IDs)
- Feedback submissions (message ID, rating)
- Rate limit violations
- Security validation failures

**Format**: JSON structured logs per facile querying e analisi

### File Validation

**Content Validation**:
- Tipo file: solo .txt (UTF-8 text)
- Dimensione: max 10MB
- Content scanning: pattern sospetti (script injection, javascript:)
- File name sanitization: rimozione caratteri pericolosi
- Encoding validation: verifica UTF-8 valido

## Security Observability
- **Security events**: Tool calls blocked, unsafe requests detected, agent terminations, injection attempts
- **Audit trail**: Structured logs for all operations (name, args hash, outcome, timestamp) - no raw payloads
- **Correlation**: Security events correlated with traces via correlationId
- **SIEM integration**: Export security events (future extension)
- **Anomaly detection**: Alert on unusual patterns (cost spikes, repeated tool failures, injection attempts, rate limit violations)

## Compliance anchors
- **GDPR**: Minimization, lawful basis, retention, transparency, DSR process
- **AI Act**: Risk classification, documentation, human oversight, incident response
- **Data residency**: Azure region configuration, geo-replication considerations

## References
- ADR-0004: Telemetry Policy (no PII in logs)
- ADR-0011: Security Hardening (da creare)
