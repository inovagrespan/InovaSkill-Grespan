import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("notification center", () => {
  it("mantem a lista de notificacoes fora do menu lateral", () => {
    const rootSource = readSource("src/routes/__root.tsx");
    const sidebarSource = readSource("src/components/AppSidebar.tsx");
    const notificationSource = readSource("src/components/NotificationCenter.tsx");

    expect(rootSource).toContain("NotificationCenter");
    expect(rootSource).toContain("fixed right-0 top-0");
    expect(rootSource).toContain("transition-[left]");
    expect(rootSource).toContain('sidebarCollapsed ? "left-0 md:left-[76px]" : "left-0 md:left-[264px]"');
    expect(rootSource).toContain('canRenderPrivateApp && "pt-[57px]"');
    expect(rootSource).toContain("justify-end");

    expect(sidebarSource).not.toContain("NotificationCenter");
    expect(sidebarSource).not.toContain("fetchNotifications");

    expect(notificationSource).toContain("Central de atenção");
    expect(notificationSource).toContain("fetchNotifications");
    expect(notificationSource).toContain("fetchUnreadCount");
    expect(notificationSource).toContain("fetchUnresolvedPendencies");
    expect(notificationSource).toContain("markNotificationAsRead");
    expect(notificationSource).toContain('to="/alertas"');
    expect(notificationSource).toContain('to="/notificacoes"');
    expect(notificationSource).toContain("custom-scrollbar max-h-[min(420px,calc(100vh-96px))]");
  });
});
