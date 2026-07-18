import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2, Loader2, Plus, Truck, XCircle } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter,
  DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { SkeletonList } from "@/components/ui/skeleton";
import {
  fetchVehicleTypes,
  createVehicleType,
  updateVehicleType,
  deleteVehicleType,
  type VehicleTypeItem,
} from "@/lib/importer-api";
import { getCurrentUserRole } from "@/lib/auth";

export const Route = createFileRoute("/veiculos/tipos")({ component: VeiculosTiposPage });

const weekdayLabels: Record<string, string> = {
  MONDAY: "Segunda",
  TUESDAY: "Terça",
  WEDNESDAY: "Quarta",
  THURSDAY: "Quinta",
  FRIDAY: "Sexta",
};

function VeiculosTiposPage() {
  const currentRole = getCurrentUserRole();
  const canManage = currentRole === "logistica" || currentRole === "admin" || currentRole === "admin_system";
  const [types, setTypes] = useState<VehicleTypeItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [editing, setEditing] = useState<VehicleTypeItem | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [formName, setFormName] = useState("");
  const [formCapacity, setFormCapacity] = useState("");
  const [saving, setSaving] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<VehicleTypeItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  async function load() {
    setLoading(true);
    try {
      const data = await fetchVehicleTypes();
      setTypes(data);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, []);

  function openCreate() {
    setEditing(null);
    setFormName("");
    setFormCapacity("");
    setShowForm(true);
  }

  function openEdit(item: VehicleTypeItem) {
    setEditing(item);
    setFormName(item.name);
    setFormCapacity(item.capacityKg === null ? "" : String(item.capacityKg));
    setShowForm(true);
  }

  async function handleSave() {
    if (!formName.trim()) {
      setMessage("O nome do tipo de veículo é obrigatório.");
      return;
    }
    const capacity = parseFloat(formCapacity.replace(",", "."));
    if (Number.isNaN(capacity) || capacity < 0) {
      setMessage("Capacidade deve ser um número maior ou igual a zero.");
      return;
    }
    setSaving(true);
    setMessage("");
    try {
      if (editing) {
        await updateVehicleType(editing.id, formName.trim(), capacity);
      } else {
        await createVehicleType(formName.trim(), capacity);
      }
      setShowForm(false);
      await load();
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!deleteConfirm) return;
    setDeleting(true);
    setMessage("");
    try {
      await deleteVehicleType(deleteConfirm.id);
      setDeleteConfirm(null);
      await load();
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setDeleting(false);
    }
  }

  function formatKg(value: number | null): string {
    return value === null ? "Não configurada" : `${value.toLocaleString("pt-BR")} kg`;
  }

  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Configurações</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Tipos de Veículo</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          {canManage
            ? "Gerencie os tipos de caminhão disponíveis no sistema. Cadastre, edite ou exclua tipos conforme necessário."
            : "Consulte os tipos de caminhão e suas capacidades disponíveis no sistema."}
        </p>
      </header>

      {message && (
        <Alert>
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="animate-soft-enter border-border bg-surface">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Tipos cadastrados</CardTitle>
            {canManage && (
              <Button size="sm" onClick={openCreate}>
                <Plus className="mr-2 size-4" />
                Novo tipo
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent>
          {loading && <SkeletonList rows={4} />}

          {!loading && types.length === 0 && (
            <p className="text-sm text-muted-foreground">Nenhum tipo de veículo cadastrado.</p>
          )}

          {!loading && types.length > 0 && (
            <div className="space-y-2">
              {types.map((t) => (
                <div key={t.id} className="flex items-center justify-between rounded-lg border border-border/80 p-3">
                  <div className="flex items-center gap-3">
                    <Truck className="size-5 text-muted-foreground" />
                    <div>
                      <p className="text-sm font-medium">{t.name}</p>
                      <p className="text-xs text-muted-foreground">
                        Capacidade: {formatKg(t.capacityKg)}
                        {t.routeCount > 0 && ` · ${t.routeCount} rota(s)`}
                      </p>
                    </div>
                  </div>
                  {canManage && (
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="outline" onClick={() => openEdit(t)}>
                        Editar
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="text-destructive"
                        onClick={() => setDeleteConfirm(t)}
                      >
                        Excluir
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={showForm} onOpenChange={setShowForm}>
        <DialogContent className="border-border bg-surface sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editing ? "Editar tipo de veículo" : "Novo tipo de veículo"}</DialogTitle>
            <DialogDescription>
              {editing
                ? "Altere o nome e a capacidade do tipo de veículo."
                : "Cadastre um novo tipo de veículo com nome e capacidade."}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">Nome</label>
              <input
                type="text"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                placeholder="Ex: Truck, Toco, Carreta"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Capacidade (kg)</label>
              <input
                type="text"
                value={formCapacity}
                onChange={(e) => setFormCapacity(e.target.value)}
                placeholder="Ex: 10300"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowForm(false)}>Cancelar</Button>
            <Button onClick={handleSave} disabled={saving}>
              {saving ? <Loader2 className="mr-2 size-4 animate-spin" /> : null}
              {editing ? "Salvar" : "Criar"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={deleteConfirm !== null} onOpenChange={() => setDeleteConfirm(null)}>
        <DialogContent className="border-border bg-surface sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Excluir tipo de veículo</DialogTitle>
            <DialogDescription>
              Tem certeza que deseja excluir "{deleteConfirm?.name}"? Esta ação não pode ser desfeita.
            </DialogDescription>
          </DialogHeader>
          {deleteConfirm && deleteConfirm.routeCount > 0 && (
            <Alert variant="destructive">
              <AlertDescription>
                Este tipo está vinculado a {deleteConfirm.routeCount} rota(s) e não pode ser excluído.
              </AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirm(null)}>Cancelar</Button>
            <Button
              variant="destructive"
              onClick={handleDelete}
              disabled={deleting || (deleteConfirm?.routeCount ?? 0) > 0}
            >
              {deleting ? <Loader2 className="mr-2 size-4 animate-spin" /> : null}
              Excluir
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
