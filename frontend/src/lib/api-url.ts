const DEFAULT_API_BASE_URL = "http://localhost:5279";

function trimTrailingSlashes(value: string): string {
  return value.replace(/\/+$/, "");
}

function trimLeadingSlashes(value: string): string {
  return value.replace(/^\/+/, "");
}

export function getApiGatewayBaseUrl(): string {
  const configured = import.meta.env.VITE_API_URL?.trim();
  return trimTrailingSlashes(configured || DEFAULT_API_BASE_URL);
}

export function getApiServiceBaseUrl(): string {
  return getApiGatewayBaseUrl().replace(/\/api$/, "");
}

export function buildGatewayUrl(path: string): string {
  return `${getApiGatewayBaseUrl()}/${trimLeadingSlashes(path)}`;
}

export function buildServiceUrl(path: string): string {
  return `${getApiServiceBaseUrl()}/${trimLeadingSlashes(path)}`;
}
