export interface AuditEntry { at: string; actor: string; action: string; target: string }

const entries: AuditEntry[] = [];

export function record(actor: string, action: string, target: string): void {
  entries.push({ at: new Date().toISOString(), actor, action, target });
}

export function allEntries(): readonly AuditEntry[] {
  return entries;
}
