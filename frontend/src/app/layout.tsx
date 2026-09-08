import type { Metadata } from "next";
import { SessionProvider } from "@/features/auth/hooks/useSession";
import "./globals.css";

export const metadata: Metadata = {
  title: "Fleet Telemetry",
  description: "Monitoreo y telemetría de flotas en tiempo real",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="es">
      <body className="min-h-dvh bg-slate-950 text-slate-100 antialiased">
        <SessionProvider>{children}</SessionProvider>
      </body>
    </html>
  );
}
