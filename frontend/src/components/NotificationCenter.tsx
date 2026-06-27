import { Link } from "@tanstack/react-router";
import { BellRing } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import {
  fetchNotifications,
  fetchUnreadCount,
  fetchUnresolvedPendencies,
  formatPendingStatus,
  markNotificationAsRead,
} from "@/lib/meetings-api";
import type { CriticalPendingSummaryDto, NotificationDto } from "@/lib/meetings-api";

const NOTIFICATION_REFRESH_INTERVAL_MS = 30_000;
const NOTIFICATION_PREVIEW_LIMIT = 10;
const PENDING_PREVIEW_LIMIT = 5;

export function NotificationCenter() {
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [pendencies, setPendencies] = useState<CriticalPendingSummaryDto[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    async function loadUnreadSummary() {
      try {
        const [count, unresolved] = await Promise.all([
          fetchUnreadCount(),
          fetchUnresolvedPendencies().catch(() => [] as CriticalPendingSummaryDto[]),
        ]);
        setUnreadCount(count + unresolved.length);
        setPendencies(unresolved);
      } catch {
        // Mantem a navegação utilizável mesmo se a API de notificações falhar.
      }
    }

    void loadUnreadSummary();
    const interval = window.setInterval(() => void loadUnreadSummary(), NOTIFICATION_REFRESH_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, []);

  async function loadNotificationPreview() {
    try {
      const [notifData, pendingData] = await Promise.all([
        fetchNotifications("nao_lida", 1).catch(() => ({ notifications: [], total: 0, unreadCount: 0 })),
        fetchUnresolvedPendencies().catch(() => [] as CriticalPendingSummaryDto[]),
      ]);

      setNotifications(notifData.notifications.slice(0, NOTIFICATION_PREVIEW_LIMIT));
      setPendencies(pendingData);
      setUnreadCount(notifData.unreadCount + pendingData.length);
    } catch {
      // Falhas temporarias nao devem bloquear a abertura do painel.
    }
  }

  function toggleNotifications() {
    setOpen((current) => {
      const next = !current;
      if (next) void loadNotificationPreview();
      return next;
    });
  }

  async function handleMarkAsRead(id: number) {
    try {
      await markNotificationAsRead(id);
      setNotifications((prev) => prev.filter((notification) => notification.id !== id));
      setUnreadCount((prev) => Math.max(0, prev - 1));
    } catch {
      // Ignora erro pontual para nao quebrar a experiencia do usuario.
    }
  }

  return (
    <div ref={containerRef} className="fixed right-[max(1rem,env(safe-area-inset-right))] top-3 z-[60]">
      <button
        type="button"
        onClick={toggleNotifications}
        className="relative inline-flex size-10 items-center justify-center rounded-md border border-border bg-surface text-muted-foreground shadow-sm outline-none ring-primary/40 transition-colors hover:bg-muted/70 hover:text-foreground focus-visible:ring-2"
        aria-expanded={open}
        aria-label="Notificações"
      >
        <BellRing className="size-4" />
        {unreadCount > 0 && (
          <span className="absolute -right-1 -top-1 inline-flex min-w-[18px] items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold leading-[18px] text-white">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-[calc(100%+8px)] z-50 w-[min(360px,calc(100vw-24px))] overflow-hidden rounded-lg border border-border bg-surface shadow-xl">
          <div className="border-b border-border bg-surface px-3 py-2">
            <div className="flex items-center justify-between gap-3">
              <p className="truncate text-sm font-semibold">Central de atenção</p>
              <Link
                to="/notificacoes"
                className="shrink-0 text-xs text-primary hover:underline"
                onClick={() => setOpen(false)}
              >
                Ver tudo
              </Link>
            </div>
          </div>

          <div className="custom-scrollbar max-h-[min(420px,calc(100vh-96px))] overflow-y-auto">
            {notifications.length === 0 && pendencies.length === 0 ? (
              <p className="px-3 py-4 text-center text-xs text-muted-foreground">Tudo em dia.</p>
            ) : (
              <>
                {pendencies.slice(0, PENDING_PREVIEW_LIMIT).map((pending) => (
                  <div
                    key={`pending-${pending.id}`}
                    className="flex min-w-0 items-start gap-2 border-b border-border/50 px-3 py-2.5 text-xs transition-colors hover:bg-muted/30"
                  >
                    <span className="mt-0.5 size-2 shrink-0 rounded-full bg-red-500" />
                    <div className="min-w-0 flex-1">
                      <Link to="/alertas" className="block" onClick={() => setOpen(false)}>
                        <p className="truncate font-medium text-foreground">{pending.title}</p>
                        <p className="mt-0.5 line-clamp-1 text-muted-foreground">
                          {pending.sector} &middot; {formatPendingStatus(pending.status)}
                        </p>
                      </Link>
                    </div>
                  </div>
                ))}

                {notifications.map((notification) => (
                  <div
                    key={`notification-${notification.id}`}
                    className="flex min-w-0 items-start gap-2 border-b border-border/50 px-3 py-2.5 text-xs transition-colors hover:bg-muted/30"
                  >
                    <span className="mt-0.5 size-2 shrink-0 rounded-full bg-primary" />
                    <div className="min-w-0 flex-1">
                      <Link
                        to={notification.relatedLink || "/notificacoes"}
                        className="block"
                        onClick={() => {
                          void handleMarkAsRead(notification.id);
                          setOpen(false);
                        }}
                      >
                        <p className="truncate font-medium text-foreground">{notification.title}</p>
                        <p className="mt-0.5 line-clamp-2 text-muted-foreground">{notification.message}</p>
                      </Link>
                    </div>
                    <button
                      type="button"
                      onClick={() => void handleMarkAsRead(notification.id)}
                      className="mt-0.5 shrink-0 rounded p-1 text-muted-foreground hover:bg-muted/80"
                      aria-label="Marcar como lida"
                    >
                      <span className="block size-2 rounded-full bg-primary/50" />
                    </button>
                  </div>
                ))}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
