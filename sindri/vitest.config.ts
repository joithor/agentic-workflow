import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    globals: false,
    testTimeout: 10_000,
    setupFiles: ["tests/setup.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // Thin wiring files: process entry, doc generator, and the two real
      // host/git implementations (each has a smoke test in tests/real.test.ts).
      exclude: ["src/cli.ts", "src/gen.ts", "src/system-real.ts", "src/git-real.ts", "src/index/sandbox-real.ts"],
      thresholds: { lines: 100, functions: 100, branches: 100, statements: 100 },
    },
  },
});
