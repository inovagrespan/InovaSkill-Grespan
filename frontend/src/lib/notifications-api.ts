import { authFetch } from "@/lib/auth";
import { getApiGatewayBaseUrl } from "@/lib/api-url";

export type NotificationDto = {
  id: number;
  userId: number;
  title: string;
  message: string;
  type: string;
  priority: string;
  status: "nao_lida" | "lida" | "arquivada" | string;
  relatedLink?: string | null;
  relatedEntity?: string | null;
  relatedEntityId?: number | null;
  createdAt: string;
  readAt?: string | null;
};

export type NotificationListDto = {
  total: number;
  unreadCount: number;
  notifications: NotificationDto[];
};

export async function fetchNotifications(status?: string, page = 1, pageSize = 50): Promise<NotificationListDto> {
  const query = new URLSearchParams();
  if (status) query.set("status", status);
  query.set("page", String(page));
  query.set("pageSize", String(pageSize));

  const response = await authFetch(`${getApiGatewayBaseUrl()}/api/notifications?${query.toString()}`);
  if (!response.ok) throw new Error("Falha ao buscar notificações.");
  return (await response.json()) as NotificationListDto;
}

export async function fetchUnreadCount(): Promise<number> {
  const response = await authFetch(`${getApiGatewayBaseUrl()}/api/notifications/unread-count`);
  if (!response.ok) throw new Error("Falha ao buscar quantidade de notificações não lidas.");
  const data = (await response.json()) as { count: number; Count?: number };
  return data.count ?? data.Count ?? 0;
}

export async function markNotificationAsRead(id: number): Promise<void> {
  const response = await authFetch(`${getApiGatewayBaseUrl()}/api/notifications/${id}/read`, {
    method: "PUT",
  });
  if (!response.ok) throw new Error("Falha ao marcar notificação como lida.");
}

export async function markAllNotificationsAsRead(): Promise<void> {
  const response = await authFetch(`${getApiGatewayBaseUrl()}/api/notifications/read-all`, {
    method: "PUT",
  });
  if (!response.ok) throw new Error("Falha ao marcar todas as notificações como lidas.");
}
