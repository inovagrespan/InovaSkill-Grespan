// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { RouteOptimizationRemediation, RouteOptimizationSummary } from "@/lib/importer-api";
import { RouteOptimizationRemediationDialog } from "./RouteOptimizationRemediationDialog";

const api = vi.hoisted(() => ({
  fetchDiagnostic: vi.fn(),
  searchCandidates: vi.fn(),
  createRemediation: vi.fn(),
  fetchStatus: vi.fn(),
  retry: vi.fn(),
}));

vi.mock("@/lib/importer-api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/importer-api")>()),
  fetchRouteOptimizationRemediation: api.fetchDiagnostic,
  searchMunicipalityCandidates: api.searchCandidates,
  createRouteOptimizationRemediation: api.createRemediation,
  fetchRouteOptimizationRemediationStatus: api.fetchStatus,
  retryRouteOptimizationRemediation: api.retry,
}));

const summary: RouteOptimizationSummary = {
  id: "result-1", routeImportId: "snapshot-1", weekday: "MONDAY",
  status: "InsufficientData", reason: "Dados insuficientes", issueCount: 4,
  currentDistanceMeters: 0, currentDurationSeconds: 0, proposedDistanceMeters: 0,
  proposedDurationSeconds: 0, currentVehicleCount: 1, proposedVehicleCount: 0,
  additionalVehicleCount: 0, additionalCapacityKg: 0, totalWeightKg: 0,
  inheritedFromResultId: null, isInherited: false, createdAt: "2026-09-05T12:00:00Z",
};

function diagnostic(readOnly = false): RouteOptimizationRemediation {
  const weightIssue = { code: "INVALID_STOP_WEIGHT" as const, routeId: "route-1", routeEntryId: "entry-1", municipalityId: null, vehicleTypeId: null, message: "Peso inválido", currentValue: "0", canResolve: true };
  const municipalityIssue = { code: "MUNICIPALITY_NOT_LINKED" as const, routeId: "route-1", routeEntryId: "entry-1", municipalityId: null, vehicleTypeId: null, message: "Município sem vínculo", currentValue: "CIDADE", canResolve: true };
  const coordinateIssue = { code: "MUNICIPALITY_COORDINATE_MISSING" as const, routeId: "route-1", routeEntryId: "entry-2", municipalityId: "municipality-1", vehicleTypeId: null, message: "Coordenada ausente", currentValue: "MARÍLIA", canResolve: true };
  const capacityIssue = { code: "VEHICLE_CAPACITY_MISSING" as const, routeId: "route-1", routeEntryId: null, municipalityId: null, vehicleTypeId: "vehicle-1", message: "Capacidade ausente", currentValue: null, canResolve: true };
  return {
    resultId: "result-1", snapshotId: "snapshot-1", expectedSnapshotId: "snapshot-1",
    weekday: "MONDAY", isCurrentSnapshot: !readOnly, readOnly, canResolve: !readOnly,
    issueCount: 4, reason: "quatro pendências",
    routes: [{
      routeId: "route-1", routeName: "ROTA A", sourceSheetName: "SEGUNDA",
      sourceHeaderRowNumber: 2, vehicleTypeId: "vehicle-1", vehicleType: "Truck",
      capacityKg: null, issues: [weightIssue, municipalityIssue, coordinateIssue, capacityIssue],
      stops: [
        { routeEntryId: "entry-1", sequence: 1, sourceRowNumber: 3, name: "CIDADE", municipalityId: null, municipality: null, weightKg: 0, isExcludedFromOptimization: false, issues: [weightIssue, municipalityIssue] },
        { routeEntryId: "entry-2", sequence: 2, sourceRowNumber: 4, name: "MARÍLIA", municipalityId: "municipality-1", municipality: "Marília", weightKg: 10, isExcludedFromOptimization: false, issues: [coordinateIssue] },
      ],
    }],
  };
}

describe("RouteOptimizationRemediationDialog", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    api.fetchDiagnostic.mockResolvedValue(diagnostic());
    api.searchCandidates.mockResolvedValue([]);
    api.createRemediation.mockResolvedValue({ derivedImportId: "derived-1", jobExecutionId: "job-1", status: "QUEUED" });
    api.fetchStatus.mockResolvedValue({ derivedImportId: "derived-1", status: "Processing", isPublished: false, affectedWeekdays: ["MONDAY"], jobs: [{ id: "job-1", jobType: "PROCESS_IMPORT", status: "Processing", progressPercent: 40, progressMessage: "Clonando", errorMessage: null }] });
  });

  afterEach(() => cleanup());

  it("abre todas as categorias e oferece os quatro editores", async () => {
    render(<RouteOptimizationRemediationDialog summary={summary} open onOpenChange={vi.fn()} onCompleted={vi.fn()} />);

    expect(await screen.findByText("Resolver pendências")).toBeTruthy();
    expect(screen.getByLabelText("Capacidade de Truck")).toBeTruthy();
    expect(screen.getByLabelText("Peso de CIDADE")).toBeTruthy();
    expect(screen.getByLabelText("Município oficial de CIDADE")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Usar base oficial" })).toBeTruthy();
  });

  it("aguarda o debounce compartilhado antes de pesquisar outro município", async () => {
    const user = userEvent.setup();
    render(<RouteOptimizationRemediationDialog summary={summary} open onOpenChange={vi.fn()} onCompleted={vi.fn()} />);
    const search = await screen.findByLabelText("Município oficial de CIDADE");
    await waitFor(() => expect(api.searchCandidates).toHaveBeenCalled());
    api.searchCandidates.mockClear();

    await user.clear(search);
    await user.type(search, "Bauru");
    expect(api.searchCandidates).not.toHaveBeenCalled();
    await new Promise((resolve) => window.setTimeout(resolve, 250));
    expect(api.searchCandidates).not.toHaveBeenCalled();
    await waitFor(() => expect(api.searchCandidates).toHaveBeenCalledWith("Bauru"));
  });

  it("permite excluir peso zero e envia uma única correção sem exigir município", async () => {
    const user = userEvent.setup();
    render(<RouteOptimizationRemediationDialog summary={summary} open onOpenChange={vi.fn()} onCompleted={vi.fn()} />);
    await screen.findByLabelText("Peso de CIDADE");
    const exclusion = screen.getByLabelText("Fora da simulação");
    expect(exclusion.className).toContain("appearance-none");
    await user.click(exclusion);
    expect(exclusion.parentElement?.querySelector('[data-slot="checkbox-indicator"]')).toBeTruthy();
    await user.type(screen.getByLabelText("Capacidade de Truck"), "10000");
    await user.click(screen.getByRole("button", { name: "Salvar e recalcular" }));

    await waitFor(() => expect(api.createRemediation).toHaveBeenCalledOnce());
    const resolutions = api.createRemediation.mock.calls[0][2];
    expect(resolutions).toEqual(expect.arrayContaining([
      expect.objectContaining({ action: "EXCLUDE_STOP", routeEntryId: "entry-1" }),
      expect.objectContaining({ action: "SET_VEHICLE_TYPE_CAPACITY", vehicleTypeId: "vehicle-1" }),
      expect.objectContaining({ action: "CONFIRM_OFFICIAL_COORDINATE", municipalityId: "municipality-1" }),
    ]));
    expect(resolutions).not.toEqual(expect.arrayContaining([expect.objectContaining({ action: "LINK_MUNICIPALITY" })]));
  });

  it("preserva o formulário após falha e respeita o modo somente leitura", async () => {
    const user = userEvent.setup();
    api.fetchDiagnostic.mockResolvedValueOnce(diagnostic());
    api.createRemediation.mockRejectedValueOnce(new Error("Conflito de snapshot"));
    const view = render(<RouteOptimizationRemediationDialog summary={summary} open onOpenChange={vi.fn()} onCompleted={vi.fn()} />);
    const input = await screen.findByLabelText("Peso de CIDADE");
    await user.clear(input);
    await user.type(input, "25,125");
    await user.type(screen.getByLabelText("Capacidade de Truck"), "10000");
    await user.click(screen.getByRole("button", { name: "Salvar e recalcular" }));
    expect((await screen.findByRole("alert")).textContent).toContain("Selecione o município oficial");
    expect((screen.getByLabelText("Peso de CIDADE") as HTMLInputElement).value).toBe("25,125");

    api.fetchDiagnostic.mockResolvedValueOnce(diagnostic(true));
    view.rerender(<RouteOptimizationRemediationDialog summary={{ ...summary, id: "historical" }} open onOpenChange={vi.fn()} onCompleted={vi.fn()} />);
    expect(await screen.findByText(/somente leitura/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Salvar e recalcular" })).toBeNull();
  });

  it("acompanha falha do processamento e permite retry sem criar outra versão", async () => {
    const user = userEvent.setup();
    const onlyCapacity = diagnostic();
    onlyCapacity.issueCount = 1;
    onlyCapacity.routes[0].issues = onlyCapacity.routes[0].issues.filter((issue) => issue.code === "VEHICLE_CAPACITY_MISSING");
    onlyCapacity.routes[0].stops = [];
    api.fetchDiagnostic.mockResolvedValueOnce(onlyCapacity);
    api.fetchStatus.mockResolvedValue({
      derivedImportId: "derived-1", status: "Failed", failureMessage: "Falha antes da publicação",
      isPublished: false, affectedWeekdays: ["MONDAY"],
      jobs: [{ id: "job-1", jobType: "PROCESS_IMPORT", status: "Failed", progressPercent: 20, progressMessage: "Falha", errorMessage: "Falha antes da publicação" }],
    });
    api.retry.mockResolvedValue({ jobExecutionId: "job-2", status: "QUEUED" });
    render(<RouteOptimizationRemediationDialog summary={summary} open onOpenChange={vi.fn()} onCompleted={vi.fn()} />);
    await user.type(await screen.findByLabelText("Capacidade de Truck"), "10000");
    await user.click(screen.getByRole("button", { name: "Salvar e recalcular" }));

    await screen.findByRole("button", { name: /Tentar novamente/ });
    await user.click(screen.getByRole("button", { name: /Tentar novamente/ }));

    await waitFor(() => expect(api.retry).toHaveBeenCalledWith("derived-1"));
  });
});
