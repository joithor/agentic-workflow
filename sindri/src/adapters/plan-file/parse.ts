export interface PlanTask {
  number: number;
  title: string;
  body: string;
  stepsDone: number;
  stepsTotal: number;
  files: string[];
  codeLines: number;
  hasFilesBlock: boolean;
}

const FENCE = /^\s*(`{3,}|~{3,})/;
const TASK = /^### Task (\d+):\s*(.+)$/;
const STEP = /^\s*- \[( |x|X)\] /;
const FILE_LINE = /^- (?:Create|Modify|Test)(?: \([^)]*\))?:\s*(.+)$/;

// Plan markdown → tasks. Anything inside a fenced block (``` or ~~~, any length,
// closed only by a bare fence of the same char and at least the same length) is
// code: never a task heading and never a step (Review Focus 3).
export function parsePlan(md: string): { title: string; tasks: PlanTask[] } {
  let title = "";
  let fence: string | null = null;
  let inFiles = false;
  const tasks: PlanTask[] = [];
  let cur: { task: PlanTask; lines: string[] } | null = null;
  const close = (): void => {
    if (cur !== null) tasks.push({ ...cur.task, body: cur.lines.join("\n").trim() });
    cur = null;
  };
  for (const line of md.split("\n")) {
    const f = FENCE.exec(line);
    if (fence !== null) {
      const closes = f !== null && f[1][0] === fence[0] && f[1].length >= fence.length && line.trim() === f[1];
      if (closes) fence = null;
      else if (cur !== null) cur.task.codeLines++;
      cur?.lines.push(line);
      continue;
    }
    if (f !== null) {
      fence = f[1];
      cur?.lines.push(line);
      continue;
    }
    if (title === "" && line.startsWith("# ")) {
      title = line.slice(2).trim();
      continue;
    }
    const t = TASK.exec(line);
    if (t !== null) {
      close();
      cur = { task: { number: Number(t[1]), title: t[2].trim(), body: "", stepsDone: 0, stepsTotal: 0, files: [], codeLines: 0, hasFilesBlock: false }, lines: [] };
      inFiles = false;
      continue;
    }
    if (/^#{1,2} /.test(line)) {
      close();
      continue;
    }
    if (cur === null) continue;
    cur.lines.push(line);
    const step = STEP.exec(line);
    if (step !== null) {
      cur.task.stepsTotal++;
      if (step[1] !== " ") cur.task.stepsDone++;
    }
    if (line.startsWith("**Files:**")) {
      inFiles = true;
      cur.task.hasFilesBlock = true;
    } else if (inFiles) {
      const m = FILE_LINE.exec(line);
      if (m !== null) for (const p of m[1].matchAll(/`([^`]+)`/g)) cur.task.files.push(p[1].replace(/:\d+(?:-\d+)?$/, ""));
      else if (line.trim() !== "") inFiles = false;
    }
  }
  close();
  return { title, tasks };
}
