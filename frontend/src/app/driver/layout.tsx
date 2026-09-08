import type { Metadata, Viewport } from "next";
import { ServiceWorkerRegistration } from "@/features/driver/ui/ServiceWorkerRegistration";

export const metadata: Metadata = {
  title: "Conductor — Fleet Telemetry",
  manifest: "/manifest.webmanifest",
  appleWebApp: {
    capable: true,
    statusBarStyle: "black-translucent",
    title: "Conductor",
  },
};

export const viewport: Viewport = {
  themeColor: "#020617",
  // El panel se usa con una mano y en movimiento: se bloquea el zoom accidental por doble toque,
  // que en un móvil dentro de un vehículo es más estorbo que ayuda.
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
};

export default function DriverLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <ServiceWorkerRegistration />
      {children}
    </>
  );
}
