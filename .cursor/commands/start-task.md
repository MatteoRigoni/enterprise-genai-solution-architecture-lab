# Start Task (Plan + Branch + Checklist) 

You are working inside an enterprise GenAI Solution Architecture lab. 

Authoritative references (must be followed): 
- docs/architecture.md 
- docs/security.md 
- docs/cost-model.md 
- docs/governance.md 
- docs/compliance.md 
- docs/finops.md 

Authoritative ADRs (must be followed): 
- docs/adr/0001-*.md 
- docs/adr/0002-*.md 
- docs/adr/0003-*.md 
- docs/adr/0004-*.md 
- docs/adr/0006-*.md 
- docs/adr/0007-*.md 

Rules: 
- If anything conflicts with ADRs/docs, STOP and explain. 
- Do NOT write code yet. 
- Keep scope tight and incremental. 

Task: {{PASTE_TASK_HERE}} 

Deliverables for this message: 
1) High-level plan (4–6 steps max)
2) Explicit decomposition into sub-tasks (Txx.A, Txx.B, ...) 
3) Minimal tests per sub-task 
4) Risks/open questions 
5) Proposed branch name and commit message pattern 
6) Exact git commands to run (branch create + first commit placeholders), but DO NOT execute them Branch naming rules: - feature/TXX-short-slug (e.g., feature/T06A-agent-orchestration) Commit message rules: - feat(TXX): ... - chore(TXX): ... - fix(TXX): ...

Execution protocol (mandatory):
- After you output the plan in backlog/plans folder, STOP.
- Wait for my message "Implement TXX.A" before writing any code.
- When I ask to implement a sub-task, implement ONLY that sub-task with max 300–400 LOC changed.
- End implementation messages with: modified files + exact verification commands + suggested git commit message.
- Keep repo buildable
- Touch only files relevant to this sub-task
