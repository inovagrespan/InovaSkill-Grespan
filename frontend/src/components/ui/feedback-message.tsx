import { X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type FeedbackMessageType = "error" | "success" | "info";

type FeedbackMessageProps = {
  message: string;
  type?: FeedbackMessageType;
  onDismiss?: () => void;
  className?: string;
};

const feedbackMessageStyles: Record<FeedbackMessageType, string> = {
  error: "border-destructive/25 bg-destructive/5 text-destructive",
  success: "border-emerald-500/25 bg-emerald-500/10 text-emerald-700",
  info: "border-sky-500/25 bg-sky-500/10 text-sky-700",
};

export function FeedbackMessage({
  message,
  type = "info",
  onDismiss,
  className,
}: FeedbackMessageProps) {
  if (!message) return null;

  return (
    <div
      role={type === "error" ? "alert" : "status"}
      className={cn(
        "flex items-start justify-between gap-3 rounded-lg border px-4 py-3 text-sm shadow-xs",
        feedbackMessageStyles[type],
        className,
      )}
    >
      <span className="min-w-0 flex-1">{message}</span>
      {onDismiss && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-6 w-6 shrink-0 text-current hover:bg-current/10"
          onClick={onDismiss}
          aria-label="Fechar mensagem"
        >
          <X className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
