import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Empaqueta el servidor con solo sus dependencias reales, para que la imagen Docker no arrastre
  // todo node_modules.
  output: "standalone",
  /* config options here */
};

export default nextConfig;
