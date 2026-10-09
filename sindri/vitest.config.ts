import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    globals: false,
    testTimeout: 10_000,
    setupFiles: ["tests/setup.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // Thin wiring files: process entry, doc generator, and the real host probe (its answers
      // depend on the OS, so one platform's branches never run; smoke test in tests/real.test.ts), and the
      // real model and scope adapters (they spawn the Claude CLI and reach the network).
      exclude: ["src/cli.ts", "src/gen.ts", "src/system-real.ts", "src/scope/model-real.ts", "src/scope/io-real.ts"],
      thresholds: { lines: 100, functions: 100, branches: 100, statements: 100 },
    },
  },
});
