import { cn } from "@/lib/utils";

type BrandLogoProps = {
  compact?: boolean;
  className?: string;
  markClassName?: string;
  textClassName?: string;
  taglineClassName?: string;
};

export function BrandLogo({
  compact = false,
  className,
  markClassName,
  textClassName,
  taglineClassName,
}: BrandLogoProps) {
  return (
    <div className={cn("flex min-w-0 items-center gap-3", className)}>
      <span
        aria-hidden="true"
        className={cn(
          "relative flex size-11 shrink-0 items-center justify-center rounded-full bg-white shadow-sm ring-1 ring-slate-200",
          "before:absolute before:inset-1.5 before:rounded-full before:border-[6px] before:border-r-transparent before:border-t-[#d01825] before:border-b-[#06122b] before:border-l-[#06122b]",
          "after:absolute after:right-1 after:top-1/2 after:size-3 after:-translate-y-1/2 after:rotate-45 after:rounded-[2px] after:bg-[#06122b]",
          markClassName,
        )}
      />
      {!compact ? (
        <span className="min-w-0">
          <span
            className={cn(
              "block font-display text-2xl font-black uppercase leading-none tracking-normal text-[#06122b] dark:text-white",
              textClassName,
            )}
          >
            CONECTA<span className="text-[#d01825]">360</span>
          </span>
          <span
            className={cn(
              "mt-1 block text-xs font-medium leading-snug text-slate-500 dark:text-slate-300",
              taglineClassName,
            )}
          >
            Da reunião reativa para a cultura de acompanhamento.
          </span>
        </span>
      ) : null}
    </div>
  );
}
