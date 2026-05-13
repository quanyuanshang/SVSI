import type { ReactNode } from "react";

interface AppShellProps {
  header: ReactNode;
  sidebar: ReactNode;
  content: ReactNode;
  detail: ReactNode;
}

export function AppShell({
  header,
  sidebar,
  content,
  detail,
}: AppShellProps) {
  return (
    <main className="page-shell">
      <div className="app-layout">
        <div className="app-layout__header">{header}</div>
        <div className="app-layout__sidebar">{sidebar}</div>
        <div className="app-layout__content">{content}</div>
        <div className="app-layout__detail">{detail}</div>
      </div>
    </main>
  );
}
