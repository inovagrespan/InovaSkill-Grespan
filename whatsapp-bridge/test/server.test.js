import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

test("bridge mantém autenticação e usa interface local por padrão", async () => {
  const source = await readFile(new URL("../src/server.js", import.meta.url), "utf8");
  assert.match(source, /useMultiFileAuthState/);
  assert.match(source, /\/connection\/qr/);
  assert.match(source, /messages\.upsert/);
  assert.match(source, /process\.env\.WHATSAPP_BRIDGE_HOST \?\? "127\.0\.0\.1"/);
  assert.match(source, /server\.listen\(port, host/);
});

test("compose inicia o bridge automaticamente e mantém a autenticação em volume", async () => {
  const compose = await readFile(new URL("../../docker-compose.yml", import.meta.url), "utf8");
  assert.match(compose, /whatsapp-bridge:\s*[\s\S]*WHATSAPP_BRIDGE_HOST: 0\.0\.0\.0/);
  assert.match(compose, /whatsapp_auth_data:\/app\/\.data\/auth/);
  assert.match(compose, /WhatsApp__BaseUrl: http:\/\/whatsapp-bridge:8081/);
  assert.match(compose, /WHATSAPP_WEBHOOK_URL: http:\/\/api:8080\/api\/integrations\/whatsapp\/webhook/);
});

test("gera QR Code sem reamostragem e com a área de respiro recomendada", async () => {
  const source = await readFile(new URL("../src/server.js", import.meta.url), "utf8");
  assert.match(source, /const qrCodeImageWidthPixels = 360/);
  assert.match(source, /const qrCodeQuietZoneModules = 4/);
  assert.match(source, /width: qrCodeImageWidthPixels/);
  assert.match(source, /margin: qrCodeQuietZoneModules/);
});
