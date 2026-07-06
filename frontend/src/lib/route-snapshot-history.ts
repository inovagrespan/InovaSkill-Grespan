export function getCurrentLocalDate(now = new Date()): string {
  const timezoneOffsetMilliseconds = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - timezoneOffsetMilliseconds)
    .toISOString()
    .slice(0, 10);
}
