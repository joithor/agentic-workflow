import { api } from "../api-client";

export async function AuditReportScreen(from: string, to: string) {
  const counts = await api<Record<string, number>>("GET", `/api/audit/report?from=${from}&to=${to}`);
  return { title: "Audit report", rows: Object.entries(counts).map(([action, n]) => ({ action, n })) };
}
