import { ArrowRight, FlaskConical, Scale, Truck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonModalContent } from "@/components/ui/skeleton";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import type { ImportedRouteDetail, VehicleTypeItem } from "@/lib/importer-api";
import { formatCapacityKg, formatOccupancy, formatRouteLoadKg } from "@/lib/route-occupancy";
import { simulateRouteVehicle } from "@/lib/route-vehicle-simulation";

type RouteVehicleSimulationDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  route: ImportedRouteDetail | null;
  vehicleTypes: VehicleTypeItem[];
  selectedVehicleTypeId: string;
  onVehicleTypeChange: (vehicleTypeId: string) => void;
  loading: boolean;
  error: string | null;
};

function formatSignedPercent(value: number | null): string {
  if (value === null) return "Sem base atual";
  const percentagePoints = value * 100;
  const sign = percentagePoints > 0 ? "+" : "";
  return `${sign}${percentagePoints.toLocaleString("pt-BR", { maximumFractionDigits: 1 })} p.p.`;
}

function formatSignedCapacity(value: number | null): string {
  if (value === null) return "Sem base atual";
  const sign = value > 0 ? "+" : "";
  return `${sign}${value.toLocaleString("pt-BR")} kg`;
}

export function RouteVehicleSimulationDialog({
  open,
  onOpenChange,
  route,
  vehicleTypes,
  selectedVehicleTypeId,
  onVehicleTypeChange,
  loading,
  error,
}: RouteVehicleSimulationDialogProps) {
  const selectedVehicle = vehicleTypes.find((vehicle) => vehicle.id === selectedVehicleTypeId) ?? null;
  const simulation = route && selectedVehicle
    ? simulateRouteVehicle(
        route.totalWeightKg,
        route.overallOccupancy,
        route.vehicleCapacityKg,
        selectedVehicle.capacityKg,
      )
    : null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto border-border bg-surface">
        <DialogHeader>
          <div className="flex flex-wrap items-center gap-2">
            <DialogTitle>Simulação de veículo</DialogTitle>
            <Badge variant="outline">Não salva alterações</Badge>
          </div>
          <DialogDescription>
            Compare a ocupação da rota com outro tipo de veículo usando a mesma carga.
          </DialogDescription>
        </DialogHeader>

        {loading && <SkeletonModalContent />}

        {!loading && error && (
          <p className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
            {error}
          </p>
        )}

        {!loading && !error && route && (
          <div className="space-y-5">
            <div className="grid gap-3 sm:grid-cols-3">
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">Rota</p>
                <p className="font-medium">{route.name}</p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="flex items-center gap-1 text-xs text-muted-foreground">
                  <Scale className="size-3.5" />
                  Carga mantida
                </p>
                <p className="font-medium">{formatRouteLoadKg(route.totalWeightKg)}</p>
              </div>
              <label className="rounded-lg border border-primary/30 bg-primary/[0.04] p-3 text-xs font-medium">
                Veículo para simular
                <Select value={selectedVehicleTypeId} onValueChange={onVehicleTypeChange}>
                  <SelectTrigger aria-label="Veículo para simular" className="mt-1.5 bg-surface">
                    <SelectValue placeholder="Selecione um veículo" />
                  </SelectTrigger>
                  <SelectContent>
                    {vehicleTypes.map((vehicle) => (
                      <SelectItem
                        key={vehicle.id}
                        value={vehicle.id}
                        disabled={vehicle.capacityKg === null || vehicle.capacityKg <= 0}
                      >
                        {vehicle.name} · {formatCapacityKg(vehicle.capacityKg)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>
            </div>

            <div className="grid items-stretch gap-3 md:grid-cols-[1fr_auto_1fr]">
              <section className="space-y-3 rounded-xl border border-border p-4">
                <div className="flex items-center gap-2">
                  <Truck className="size-4 text-muted-foreground" />
                  <div>
                    <p className="text-xs uppercase tracking-wide text-muted-foreground">Cenário atual</p>
                    <p className="font-semibold">{route.vehicleType}</p>
                  </div>
                </div>
                <p className="text-sm text-muted-foreground">{formatCapacityKg(route.vehicleCapacityKg)}</p>
                <RouteOccupancyIndicator value={route.overallOccupancy} />
              </section>

              <div className="hidden items-center justify-center md:flex">
                <span className="grid size-10 place-items-center rounded-full border border-border bg-muted">
                  <ArrowRight className="size-4" />
                </span>
              </div>

              <section className="space-y-3 rounded-xl border border-primary/40 bg-primary/[0.03] p-4">
                <div className="flex items-center gap-2">
                  <FlaskConical className="size-4 text-primary" />
                  <div>
                    <p className="text-xs uppercase tracking-wide text-muted-foreground">Resultado do cenário</p>
                    <p className="font-semibold">{selectedVehicle?.name ?? "Selecione um veículo"}</p>
                  </div>
                </div>
                <p className="text-sm text-muted-foreground">
                  {formatCapacityKg(selectedVehicle?.capacityKg ?? null)}
                </p>
                <RouteOccupancyIndicator value={simulation?.occupancy ?? null} />
              </section>
            </div>

            {selectedVehicle && simulation && (
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-lg border border-border p-3 text-center">
                  <p className="text-xs text-muted-foreground">Nova ocupação</p>
                  <p className="mt-1 text-lg font-semibold">{formatOccupancy(simulation.occupancy)}</p>
                </div>
                <div className="rounded-lg border border-border p-3 text-center">
                  <p className="text-xs text-muted-foreground">Variação da ocupação</p>
                  <p className="mt-1 text-lg font-semibold">{formatSignedPercent(simulation.occupancyChange)}</p>
                </div>
                <div className="rounded-lg border border-border p-3 text-center">
                  <p className="text-xs text-muted-foreground">Variação da capacidade</p>
                  <p className="mt-1 text-lg font-semibold">{formatSignedCapacity(simulation.capacityChangeKg)}</p>
                </div>
              </div>
            )}

            <p className="text-center text-xs text-muted-foreground">
              Simulação informativa: cidades, entregas e peso permanecem iguais. Nenhuma alteração é gravada.
            </p>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
