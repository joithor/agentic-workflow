import { test } from "node:test";
import assert from "node:assert/strict";
import { count } from "../src/count.js";

test("counts words", () => assert.equal(count("a b  c", "words"), 3));
test("counts lines", () => assert.equal(count("a\nb", "lines"), 2));
test("counts chars", () => assert.equal(count("abc", "chars"), 3));
