const CLIENT_ID_RADIX = 36;

let fallbackSequence = 0;

export function createClientMessageId(): string {
  const randomUuid = globalThis.crypto?.randomUUID;
  if (typeof randomUuid === "function") {
    return randomUuid.call(globalThis.crypto);
  }

  fallbackSequence += 1;
  return [
    "message",
    Date.now().toString(CLIENT_ID_RADIX),
    fallbackSequence.toString(CLIENT_ID_RADIX),
    Math.random().toString(CLIENT_ID_RADIX).slice(2),
  ].join("-");
}
