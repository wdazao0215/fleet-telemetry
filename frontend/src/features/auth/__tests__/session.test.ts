import { describe, expect, it } from "vitest";
import { isExpired } from "../domain/types";

describe("isExpired", () => {
  const now = new Date("2026-01-01T10:00:00Z");

  it("acepta un token con vida por delante", () => {
    expect(isExpired({ accessToken: "t", expiresAt: "2026-01-01T11:00:00Z" }, now)).toBe(false);
  });

  it("rechaza un token ya caducado", () => {
    expect(isExpired({ accessToken: "t", expiresAt: "2026-01-01T09:59:00Z" }, now)).toBe(true);
  });

  it("trata como caducado el que expira dentro del margen", () => {
    // Sin margen, una petición ya en vuelo recibiría un 401 por unos segundos de diferencia.
    expect(isExpired({ accessToken: "t", expiresAt: "2026-01-01T10:00:20Z" }, now)).toBe(true);
  });
});
