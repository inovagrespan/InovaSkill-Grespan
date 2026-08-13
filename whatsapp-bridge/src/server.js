import http from "node:http";
import { mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import makeWASocket, {
  Browsers,
  DisconnectReason,
  downloadMediaMessage,
  useMultiFileAuthState,
} from "@whiskeysockets/baileys";
import pino from "pino";
import QRCode from "qrcode";

const dirname = path.dirname(fileURLToPath(import.meta.url));
const authDirectory = process.env.WHATSAPP_AUTH_PATH ?? path.resolve(dirname, "../.data/auth");
const port = Number(process.env.WHATSAPP_BRIDGE_PORT ?? 8081);
const webhookUrl = process.env.WHATSAPP_WEBHOOK_URL ?? "http://localhost:5279/api/integrations/whatsapp/webhook";
const webhookSecret = process.env.WHATSAPP_WEBHOOK_SECRET ?? "local-whatsapp-bridge-secret";
const logger = pino({ level: process.env.WHATSAPP_LOG_LEVEL ?? "warn" });
const maximumBodyBytes = 20 * 1024 * 1024;
const qrCodeImageWidthPixels = 360;
const qrCodeQuietZoneModules = 4;

let socket;
let connectionStatus = "disconnected";
let qrDataUrl;
let connectedPhone;
let starting;

async function startSocket() {
  if (starting) return starting;
  if (socket && ["connecting", "connected"].includes(connectionStatus)) return;
  starting = (async () => {
    await mkdir(authDirectory, { recursive: true });
    const { state, saveCreds } = await useMultiFileAuthState(authDirectory);
    connectionStatus = "connecting";
    socket = makeWASocket({
      auth: state,
      browser: Browsers.ubuntu("InovaSkill Grespan"),
      logger,
      markOnlineOnConnect: false,
      syncFullHistory: false,
    });
    socket.ev.on("creds.update", saveCreds);
    socket.ev.on("connection.update", async ({ connection, qr, lastDisconnect }) => {
      if (qr) {
        qrDataUrl = await QRCode.toDataURL(qr, {
          width: qrCodeImageWidthPixels,
          margin: qrCodeQuietZoneModules,
        });
      }
      if (connection === "open") {
        connectionStatus = "connected";
        qrDataUrl = undefined;
        connectedPhone = socket.user?.id?.split(":")[0] ?? socket.user?.id?.split("@")[0];
        await notifyWebhook("connection_update", { state: "connected", phone: connectedPhone });
      }
      if (connection === "close") {
        const statusCode = lastDisconnect?.error?.output?.statusCode;
        const loggedOut = statusCode === DisconnectReason.loggedOut;
        connectionStatus = "disconnected";
        socket = undefined;
        await notifyWebhook("connection_update", { state: "disconnected" });
        if (!loggedOut) setTimeout(() => void startSocket(), 2_000);
      }
    });
    socket.ev.on("messages.upsert", async ({ messages, type }) => {
      if (type !== "notify") return;
      for (const message of messages) await notifyWebhook("messages_upsert", message);
    });
  })().finally(() => { starting = undefined; });
  return starting;
}

async function notifyWebhook(event, data) {
  try {
    await fetch(webhookUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Webhook-Secret": webhookSecret },
      body: JSON.stringify({ event, data }),
    });
  } catch (error) { logger.warn({ error }, "Falha ao notificar o backend"); }
}

async function readJson(request) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > maximumBodyBytes) throw new Error("Payload excede o limite permitido");
    chunks.push(chunk);
  }
  return chunks.length ? JSON.parse(Buffer.concat(chunks).toString("utf8")) : {};
}

function json(response, status, body) {
  response.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(body));
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url, `http://${request.headers.host}`);
    if (request.method === "GET" && url.pathname === "/health") return json(response, 200, { status: "ok" });
    if (request.method === "GET" && url.pathname === "/connection")
      return json(response, 200, { status: connectionStatus, phone: connectedPhone ?? null });
    if (request.method === "POST" && url.pathname === "/connection/start") {
      await startSocket();
      return json(response, 202, { status: connectionStatus, phone: connectedPhone ?? null });
    }
    if (request.method === "GET" && url.pathname === "/connection/qr")
      return qrDataUrl ? json(response, 200, { dataUrl: qrDataUrl }) : json(response, 404, { detail: "QR Code ainda não disponível." });
    if (request.method === "DELETE" && url.pathname === "/connection") {
      if (socket) await socket.logout();
      socket = undefined; qrDataUrl = undefined; connectedPhone = undefined; connectionStatus = "disconnected";
      await rm(authDirectory, { recursive: true, force: true });
      return json(response, 200, { status: connectionStatus });
    }
    if (request.method === "POST" && url.pathname === "/messages/text") {
      if (!socket || connectionStatus !== "connected") return json(response, 409, { detail: "WhatsApp não conectado." });
      const body = await readJson(request);
      const sent = await socket.sendMessage(`${String(body.phone).replace(/\D/g, "")}@s.whatsapp.net`, { text: String(body.text ?? "") });
      return json(response, 200, { id: sent?.key?.id });
    }
    if (request.method === "POST" && url.pathname === "/messages/media") {
      if (!socket || connectionStatus !== "connected") return json(response, 409, { detail: "WhatsApp não conectado." });
      const body = await readJson(request);
      const buffer = await downloadMediaMessage(body.message, "buffer", {}, { logger, reuploadRequest: socket.updateMediaMessage });
      return json(response, 200, { base64: Buffer.from(buffer).toString("base64") });
    }
    return json(response, 404, { detail: "Rota não encontrada." });
  } catch (error) {
    logger.error({ error }, "Falha no bridge do WhatsApp");
    return json(response, 500, { detail: error instanceof Error ? error.message : "Falha interna." });
  }
});

server.listen(port, "127.0.0.1", () => {
  logger.info(`Bridge do WhatsApp disponível em http://127.0.0.1:${port}`);
});
