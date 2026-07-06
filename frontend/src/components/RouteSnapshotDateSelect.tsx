import { Input } from "@/components/ui/input";

type RouteSnapshotDateSelectProps = {
  value: string;
  onValueChange: (date: string) => void;
};

export function RouteSnapshotDateSelect({
  value,
  onValueChange,
}: RouteSnapshotDateSelectProps) {
  return (
    <label className="flex w-full flex-col gap-1.5 text-xs font-medium text-foreground sm:w-auto">
      Data de referência
      <Input
        type="date"
        aria-label="Data do snapshot"
        value={value}
        onChange={(event) => onValueChange(event.target.value)}
        className="min-w-44 bg-surface text-foreground [color-scheme:light] dark:[color-scheme:dark]"
      />
    </label>
  );
}
