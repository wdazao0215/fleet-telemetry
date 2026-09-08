import { STATUS_PRESENTATION } from "../domain/vehiclePresentation";
import type { VehicleStatus } from "../domain/types";

/** Estado con color y texto: el color solo no es accesible ni se lee con prisa. */
export function StatusChip({ status }: { status: VehicleStatus }) {
  const presentation = STATUS_PRESENTATION[status];

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset ${presentation.chip}`}
    >
      <span className={`size-1.5 rounded-full ${presentation.dot}`} aria-hidden />
      {presentation.label}
    </span>
  );
}
