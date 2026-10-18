import { test } from "node:test";
import assert from "node:assert/strict";
import { format } from "../src/format.js";

test("pads the count", () => assert.equal(format([{ file: "a", count: 7 }]), "       7 a"));
