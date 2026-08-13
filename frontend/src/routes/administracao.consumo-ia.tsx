import { useCallback, useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Bell, Bot, ChevronLeft, ChevronRight, DollarSign, Gauge, Search, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { addAiModelPrice, getAiConsumptionConfiguration, getAiConsumptionReport, listAiConsumptionAlerts, listAiConsumptionUsers, readAiConsumptionAlert, updateAiConsumptionConfiguration, updateAiUserLimit, type AiConsumptionAlert, type AiConsumptionConfiguration, type AiConsumptionReport, type AiConsumptionUser, type AiConsumptionUsersPage } from "@/lib/ai-consumption-api";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

export const Route = createFileRoute("/administracao/consumo-ia")({ component: AiConsumptionPage });

const formatTokens = (value: number) => new Intl.NumberFormat("pt-BR").format(value);
const formatCost = (value: number) => new Intl.NumberFormat("pt-BR", { style: "currency", currency: "USD", minimumFractionDigits: 4 }).format(value);
const today = new Date();
const defaultFrom = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10);
const defaultTo = new Date(today.getFullYear(), today.getMonth() + 1, 1).toISOString().slice(0, 10);
const USER_PAGE_SIZE = 20;
const DETAIL_PAGE_SIZE = 25;

function AiConsumptionPage() {
  const [configuration, setConfiguration] = useState<AiConsumptionConfiguration | null>(null);
  const [report, setReport] = useState<AiConsumptionReport | null>(null);
  const [alerts, setAlerts] = useState<AiConsumptionAlert[]>([]);
  const [from, setFrom] = useState(defaultFrom); const [to, setTo] = useState(defaultTo); const [userId, setUserId] = useState("");
  const [detailPage, setDetailPage] = useState(1);
  const [userSearch, setUserSearch] = useState("");
  const [userPage, setUserPage] = useState(1);
  const [users, setUsers] = useState<AiConsumptionUsersPage>({ page: 1, pageSize: USER_PAGE_SIZE, total: 0, items: [] });
  const debouncedUserSearch = useDebouncedValue(userSearch, TEXT_SEARCH_DEBOUNCE_MS);
  const [error, setError] = useState(""); const [saving, setSaving] = useState(false);
  const [price, setPrice] = useState({ model: "", input: "", output: "", effectiveFrom: new Date().toISOString().slice(0, 16) });

  const load = useCallback(async () => {
    try {
      setError("");
      const [config, usage, currentAlerts] = await Promise.all([getAiConsumptionConfiguration(), getAiConsumptionReport(`${from}T00:00:00Z`, `${to}T00:00:00Z`, userId || undefined, detailPage, DETAIL_PAGE_SIZE), listAiConsumptionAlerts()]);
      setConfiguration(config); setReport(usage); setAlerts(currentAlerts);
      setPrice((current) => ({ ...current, model: current.model || config.model }));
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao carregar o consumo."); }
  }, [from, to, userId, detailPage]);
  useEffect(() => { void load(); }, [load]);
  const loadUsers = useCallback(async () => {
    try { setUsers(await listAiConsumptionUsers({ search: debouncedUserSearch, page: userPage, pageSize: USER_PAGE_SIZE })); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao carregar usuários."); }
  }, [debouncedUserSearch, userPage]);
  useEffect(() => { void loadUsers(); }, [loadUsers]);

  async function saveSettings() {
    if (!configuration) return;
    setSaving(true); try { await updateAiConsumptionConfiguration(configuration); await load(); } catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao salvar."); } finally { setSaving(false); }
  }
  async function saveUserLimit(targetUserId: number, monthlyTokenLimit: string, alertPercentage: string) {
    await updateAiUserLimit(targetUserId, { monthlyTokenLimit: monthlyTokenLimit === "" ? null : Number(monthlyTokenLimit), alertPercentage: alertPercentage === "" ? null : Number(alertPercentage) }); await Promise.all([load(), loadUsers()]);
  }
  async function savePrice() {
    await addAiModelPrice({ model: price.model, inputPricePerMillionUsd: Number(price.input), outputPricePerMillionUsd: Number(price.output), effectiveFrom: new Date(price.effectiveFrom).toISOString() }); await load();
  }

  return <div className="page-shell space-y-6">
    <header><span className="page-header-kicker">Administração</span><h1 className="mt-2 text-4xl font-display tracking-tight">Consumo de IA</h1><p className="mt-2 text-sm text-muted-foreground">Tokens, custos estimados, limites mensais e alertas do assistente.</p></header>
    {error ? <div role="alert" className="rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">{error}</div> : null}
    <section className="grid gap-3 md:grid-cols-3">
      {[{ icon: Gauge, label: "Tokens", value: formatTokens(report?.total.totalTokens ?? 0) }, { icon: DollarSign, label: "Custo estimado", value: formatCost(report?.total.estimatedCostUsd ?? 0) }, { icon: Bot, label: "Respostas / chamadas", value: `${report?.total.responses ?? 0} / ${report?.total.calls ?? 0}` }].map(({ icon: Icon, label, value }) => <div key={label} className="rounded-xl border border-border bg-surface p-5"><Icon className="size-5 text-primary"/><p className="mt-3 text-xs uppercase text-muted-foreground">{label}</p><p className="mt-1 text-2xl font-semibold">{value}</p></div>)}
    </section>
    <section className="rounded-xl border border-border bg-surface p-5"><h2 className="font-display text-xl">Filtros do relatório</h2><div className="mt-4 grid gap-3 md:grid-cols-5"><Input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setDetailPage(1); }}/><Input type="date" value={to} onChange={(e) => { setTo(e.target.value); setDetailPage(1); }}/><div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"/><Input aria-label="Buscar usuário" className="pl-9" value={userSearch} onChange={(e) => { setUserSearch(e.target.value); setUserPage(1); }} placeholder="Buscar nome ou e-mail"/></div><Select value={userId || "all"} onValueChange={(value) => { setUserId(value === "all" ? "" : value); setDetailPage(1); }}><SelectTrigger><SelectValue placeholder="Todos os usuários"/></SelectTrigger><SelectContent><SelectItem value="all">Todos os usuários</SelectItem>{users.items.map((user) => <SelectItem key={user.userId} value={String(user.userId)}>{user.name} — {user.email}</SelectItem>)}</SelectContent></Select><Button onClick={() => void load()}>Atualizar</Button></div></section>
    <section className="grid gap-5 xl:grid-cols-2">
      <div className="rounded-xl border border-border bg-surface p-5"><h2 className="font-display text-xl">Configuração global</h2>{configuration ? <div className="mt-4 space-y-3"><label className="block text-sm">Modelo<Input className="mt-1" value={configuration.model} onChange={(e) => setConfiguration({ ...configuration, model: e.target.value })}/></label><label className="block text-sm">Limite mensal padrão<Input className="mt-1" type="number" min="0" value={configuration.defaultMonthlyTokenLimit} onChange={(e) => setConfiguration({ ...configuration, defaultMonthlyTokenLimit: Number(e.target.value) })}/></label><label className="block text-sm">Alerta padrão (%)<Input className="mt-1" type="number" min="1" max="100" value={configuration.defaultAlertPercentage} onChange={(e) => setConfiguration({ ...configuration, defaultAlertPercentage: Number(e.target.value) })}/></label><Button disabled={saving} onClick={() => void saveSettings()}><Save className="size-4"/>Salvar</Button></div> : null}</div>
      <div className="rounded-xl border border-border bg-surface p-5"><h2 className="font-display text-xl">Novo preço com vigência</h2><div className="mt-4 grid gap-3 md:grid-cols-2"><Input aria-label="Modelo do preço" value={price.model} onChange={(e) => setPrice({ ...price, model: e.target.value })}/><Input aria-label="Início da vigência" type="datetime-local" value={price.effectiveFrom} onChange={(e) => setPrice({ ...price, effectiveFrom: e.target.value })}/><Input aria-label="Preço de entrada" type="number" min="0" step="0.000001" placeholder="Entrada / 1M" value={price.input} onChange={(e) => setPrice({ ...price, input: e.target.value })}/><Input aria-label="Preço de saída" type="number" min="0" step="0.000001" placeholder="Saída / 1M" value={price.output} onChange={(e) => setPrice({ ...price, output: e.target.value })}/><Button onClick={() => void savePrice()}>Adicionar preço</Button></div></div>
    </section>
    <section className="rounded-xl border border-border bg-surface p-5"><div className="flex flex-wrap items-end justify-between gap-2"><div><h2 className="font-display text-xl">Limites por usuário</h2><p className="mt-1 text-sm text-muted-foreground">{users.total} usuário(s) encontrado(s). Use a busca acima para localizar nome ou e-mail.</p></div></div><div className="mt-4 overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">Usuário</th><th>Limite personalizado</th><th>Alerta (%)</th><th></th></tr></thead><tbody>{users.items.length === 0 ? <tr><td className="p-6 text-center text-muted-foreground" colSpan={4}>Nenhum usuário encontrado.</td></tr> : users.items.map((user) => <UserLimitRow key={user.userId} user={user} onSave={saveUserLimit}/>)}</tbody></table></div><UserPagination page={users.page} pageSize={users.pageSize} total={users.total} onPageChange={setUserPage}/></section>
    <section className="rounded-xl border border-border bg-surface p-5"><h2 className="flex items-center gap-2 font-display text-xl"><Bell className="size-5"/>Alertas internos</h2><div className="mt-4 space-y-2">{alerts.length === 0 ? <p className="text-sm text-muted-foreground">Nenhum alerta de consumo.</p> : alerts.map((alert) => <div key={alert.id} className={`flex items-center justify-between rounded-lg border p-3 text-sm ${alert.readAt ? "opacity-60" : "border-amber-400/50 bg-amber-50 dark:bg-amber-950/20"}`}><span><strong>{alert.userName}</strong> — {formatTokens(alert.consumedTokens)} de {formatTokens(alert.tokenLimit)} tokens ({alert.level === "LIMIT_REACHED" ? "limite atingido" : "aviso"})</span>{!alert.readAt ? <Button variant="outline" size="sm" onClick={async () => { await readAiConsumptionAlert(alert.id); await load(); }}>Marcar como lido</Button> : null}</div>)}</div></section>
    <section className="rounded-xl border border-border bg-surface p-5"><h2 className="font-display text-xl">Detalhamento de chamadas</h2><div className="mt-4 overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">Data</th><th>Usuário</th><th>Modelo</th><th>Finalidade</th><th>Tokens</th><th>Custo</th><th>Status</th></tr></thead><tbody>{report?.details.length === 0 ? <tr><td className="p-6 text-center text-muted-foreground" colSpan={7}>Nenhuma chamada no período.</td></tr> : report?.details.map((item) => <tr key={item.id} className="border-b"><td className="p-2">{new Date(item.createdAt).toLocaleString("pt-BR")}</td><td>{item.userName}</td><td>{item.model}</td><td>{item.purpose}</td><td>{formatTokens(item.totalTokens)}</td><td>{formatCost(item.estimatedCostUsd)}</td><td>{item.status}</td></tr>)}</tbody></table></div>{report ? <PagedNavigation page={report.detailPage} pageSize={report.detailPageSize} total={report.detailTotal} itemLabel="chamadas" onPageChange={setDetailPage}/> : null}</section>
  </div>;
}

function UserLimitRow({ user, onSave }: { user: AiConsumptionUser; onSave: (id: number, limit: string, alert: string) => Promise<void> }) {
  const [limit, setLimit] = useState(user.monthlyTokenLimit?.toString() ?? ""); const [alert, setAlert] = useState(user.alertPercentage?.toString() ?? "");
  return <tr className="border-b"><td className="p-2"><strong>{user.name}</strong><div className="text-xs text-muted-foreground">{user.email}</div></td><td><Input aria-label={`Limite de ${user.name}`} type="number" min="0" value={limit} onChange={(e) => setLimit(e.target.value)} placeholder="Usar padrão"/></td><td><Input aria-label={`Alerta de ${user.name}`} type="number" min="1" max="100" value={alert} onChange={(e) => setAlert(e.target.value)} placeholder="Usar padrão"/></td><td><Button variant="outline" size="sm" onClick={() => void onSave(user.userId, limit, alert)}>Salvar</Button></td></tr>;
}

function UserPagination({ page, pageSize, total, onPageChange }: { page: number; pageSize: number; total: number; onPageChange: (page: number) => void }) {
  return <PagedNavigation page={page} pageSize={pageSize} total={total} itemLabel="usuários" onPageChange={onPageChange}/>;
}

function PagedNavigation({ page, pageSize, total, itemLabel, onPageChange }: { page: number; pageSize: number; total: number; itemLabel: string; onPageChange: (page: number) => void }) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages === 1) return null;
  return <div className="mt-4 flex items-center justify-between gap-3 border-t border-border pt-4 text-sm"><span className="text-muted-foreground">Página {page} de {totalPages}</span><div className="flex gap-2"><Button aria-label={`Página anterior de ${itemLabel}`} variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}><ChevronLeft className="size-4"/>Anterior</Button><Button aria-label={`Próxima página de ${itemLabel}`} variant="outline" size="sm" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Próxima<ChevronRight className="size-4"/></Button></div></div>;
}
