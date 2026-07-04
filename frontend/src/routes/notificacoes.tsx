import { createFileRoute, Link } from "@tanstack/react-router";
import { useState, useEffect, useCallback } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { AlertTriangle, BellRing, CheckCheck, Eye, Filter, Inbox } from "lucide-react";
import { fetchNotifications, markNotificationAsRead, markAllNotificationsAsRead, type NotificationDto } from "@/lib/notifications-api";

export const Route = createFileRoute("/notificacoes")({
  component: NotificacoesPage,
});

function NotificacoesPage() {
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [total, setTotal] = useState(0);
  const [filter, setFilter] = useState<string | undefined>();
  const [loading, setLoading] = useState(true);

  const loadNotifications = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchNotifications(filter);
      setNotifications(data.notifications);
      setTotal(data.total);
      setUnreadCount(data.unreadCount);
    } catch { /* ignore */ } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => { void loadNotifications(); }, [loadNotifications]);

  async function handleMarkAsRead(id: number) {
    try {
      await markNotificationAsRead(id);
      setNotifications((prev) => prev.map((n) => n.id === id ? { ...n, status: "lida" } : n));
      setUnreadCount((prev) => Math.max(0, prev - 1));
    } catch { /* ignore */ }
  }

  async function handleMarkAllAsRead() {
    try {
      await markAllNotificationsAsRead();
      setNotifications((prev) => prev.map((n) => ({ ...n, status: "lida" })));
      setUnreadCount(0);
    } catch { /* ignore */ }
  }

  const priorityVariant: Record<string, "default" | "secondary" | "destructive" | "outline"> = {
    critica: "destructive",
    alta: "secondary",
    media: "outline",
    baixa: "outline",
  };

  return (
    <div className="page-shell">
      <header className="animate-soft-enter mb-8 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Notificações</span>
          <h1 className="mt-2 mb-2 flex items-center gap-3 text-4xl font-display tracking-tight">
            <BellRing className="size-8 text-primary" />
            Central de Notificações
          </h1>
          <p className="max-w-2xl text-sm text-muted-foreground">
            {unreadCount > 0 ? `Você tem ${unreadCount} notificação(ões) não lida(s).` : "Todas as notificações estão lidas."}
          </p>
        </div>
        <div className="flex gap-2">
          <select
            className="rounded-lg border border-border bg-surface px-3 py-2 text-xs"
            value={filter ?? ""}
            onChange={(e) => setFilter(e.target.value || undefined)}
          >
            <option value="">Todas</option>
            <option value="nao_lida">Não lidas</option>
            <option value="lida">Lidas</option>
          </select>
          {unreadCount > 0 && (
            <Button variant="outline" size="sm" onClick={() => void handleMarkAllAsRead()}>
              <CheckCheck className="mr-2 size-4" /> Marcar todas como lidas
            </Button>
          )}
        </div>
      </header>

      <section className="space-y-2">
        {loading ? (
          <p className="text-sm text-muted-foreground">Carregando...</p>
        ) : notifications.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-surface p-12">
            <Inbox className="mb-4 size-12 text-muted-foreground/50" />
            <p className="text-sm text-muted-foreground">Nenhuma notificação encontrada.</p>
          </div>
        ) : (
          notifications.map((n) => (
            <div
              key={n.id}
              className={`rounded-xl border p-4 transition-colors ${
                n.status === "nao_lida"
                  ? "border-primary/20 bg-primary/5"
                  : "border-border bg-surface"
              }`}
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    {n.status === "nao_lida" && <span className="size-2 rounded-full bg-primary shrink-0" />}
                    <p className={`text-sm font-medium ${n.status === "nao_lida" ? "text-foreground" : "text-muted-foreground"}`}>
                      {n.title}
                    </p>
                    <Badge variant={priorityVariant[n.priority] ?? "outline"} className="text-[10px]">
                      {n.priority}
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">{n.message}</p>
                  <div className="mt-2 flex items-center gap-3 text-[10px] text-muted-foreground">
                    <span>{formatDate(n.createdAt)}</span>
                    <Badge variant="outline" className="text-[10px]">{n.type}</Badge>
                  </div>
                </div>
                <div className="flex shrink-0 gap-1">
                  {n.relatedLink && (
                    <Button variant="ghost" size="sm" className="size-8 p-0" asChild>
                      <Link to={n.relatedLink}>
                        <Eye className="size-4" />
                      </Link>
                    </Button>
                  )}
                  {n.status === "nao_lida" && (
                    <Button variant="ghost" size="sm" className="size-8 p-0" onClick={() => void handleMarkAsRead(n.id)}>
                      <CheckCheck className="size-4" />
                    </Button>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </section>
    </div>
  );
}

function formatDate(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}
