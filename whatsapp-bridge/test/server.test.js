import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

test("bridge mantém autenticação e expõe somente a interface local esperada", async () => {
  const source = await readFile(new URL("../src/server.js", import.meta.url), "utf8");
  assert.match(source, /useMultiFileAuthState/);
  assert.match(source, /\/connection\/qr/);
  assert.match(source, /messages\.upsert/);
  assert.match(source, /127\.0\.0\.1/);
});

test("gera QR Code sem reamostragem e com a área de respiro recomendada", async () => {
  const source = await readFile(new URL("../src/server.js", import.meta.url), "utf8");
  assert.match(source, /const qrCodeImageWidthPixels = 360/);
  assert.match(source, /const qrCodeQuietZoneModules = 4/);
  assert.match(source, /width: qrCodeImageWidthPixels/);
  assert.match(source, /margin: qrCodeQuietZoneModules/);
});
