import { test } from "node:test";
import assert from "node:assert/strict";
import { parseArgs } from "../src/args.js";

test("defaults to words", () => assert.equal(parseArgs(["a.txt"]).mode, "words"));
test("reads the mode", () => assert.equal(parseArgs(["--lines", "a.txt"]).mode, "lines"));
test("rejects unknown options", () => assert.throws(() => parseArgs(["--nope"])));
