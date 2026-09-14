import * as React from "react";
import { Check } from "lucide-react";

import { cn } from "@/lib/utils";

export type CheckboxProps = Omit<React.ComponentProps<"input">, "type">;

const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  ({ checked, defaultChecked, className, onChange, ...props }, ref) => {
    const [internalChecked, setInternalChecked] = React.useState(Boolean(defaultChecked));
    const isChecked = checked === undefined ? internalChecked : Boolean(checked);

    return (
      <span className="relative inline-grid size-5 shrink-0 place-items-center rounded-[5px]">
        <input
          ref={ref}
          type="checkbox"
          checked={checked}
          defaultChecked={defaultChecked}
          className={cn(
            "absolute inset-0 z-0 size-5 cursor-pointer appearance-none rounded-[5px] border border-input bg-background shadow-xs transition-all duration-200",
            "checked:border-primary checked:bg-primary hover:border-primary/60",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/70 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
            "disabled:cursor-not-allowed disabled:opacity-50",
            className,
          )}
          onChange={(event) => {
            if (checked === undefined) setInternalChecked(event.currentTarget.checked);
            onChange?.(event);
          }}
          {...props}
        />
        {isChecked && (
          <Check
            aria-hidden="true"
            data-slot="checkbox-indicator"
            className="pointer-events-none relative z-10 size-3.5 text-primary-foreground"
            strokeWidth={3}
          />
        )}
      </span>
    );
  },
);
Checkbox.displayName = "Checkbox";

export { Checkbox };
