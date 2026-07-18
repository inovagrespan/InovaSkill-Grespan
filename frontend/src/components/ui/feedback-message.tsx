import { useEffect, useState } from "react";
import { AlertCircle, AlertTriangle, CheckCircle2, Info, X, type LucideIcon } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type FeedbackMessageType = "error" | "success" | "warning" | "info";

type FeedbackMessageProps = {
  message: string | null;
  type?: FeedbackMessageType;
  onDismiss?: () => void;
  autoHide?: number;
  className?: string;
};

const feedbackMessageStyles: Record<FeedbackMessageType, string> = {
  error: "border-destructive/25 bg-destructive/5 text-destructive",
  success: "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  warning: "border-amber-500/25 bg-amber-500/10 text-amber-800 dark:text-amber-300",
  info: "border-sky-500/25 bg-sky-500/10 text-sky-700 dark:text-sky-300",
};

const feedbackMessageIcons: Record<FeedbackMessageType, LucideIcon> = {
  error: AlertCircle,
  success: CheckCircle2,
  warning: AlertTriangle,
  info: Info,
};

export function FeedbackMessage({
  message,
  type = "info",
  onDismiss,
  autoHide = 0,
  className,
}: FeedbackMessageProps) {
  const [visible, setVisible] = useState(Boolean(message));

  useEffect(() => {
    setVisible(Boolean(message));
  }, [message]);

  useEffect(() => {
    if (!visible || autoHide <= 0) return;

    const timer = window.setTimeout(() => {
      setVisible(false);
      onDismiss?.();
    }, autoHide);

    return () => window.clearTimeout(timer);
  }, [autoHide, onDismiss, visible]);

  if (!message || !visible) return null;

  const Icon = feedbackMessageIcons[type];

  return (
    <div
      role={type === "error" ? "alert" : "status"}
      className={cn(
        "flex items-start justify-between gap-3 rounded-lg border px-4 py-3 text-sm shadow-xs",
        feedbackMessageStyles[type],
        className,
      )}
    >
      <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <span className="min-w-0 flex-1 leading-relaxed">{message}</span>
      {(onDismiss || autoHide > 0) && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-6 w-6 shrink-0 text-current hover:bg-current/10"
          onClick={() => {
            setVisible(false);
            onDismiss?.();
          }}
          aria-label="Fechar mensagem"
        >
          <X className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
