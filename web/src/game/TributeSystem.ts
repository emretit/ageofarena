/**
 * TributeSystem.cs port — resource tribute between teams.
 * TaxRate=30% by default; Coinage→20%, Banking→0%.
 * Coinage/Banking are TechId values; pass researchSystem to get live tax rate.
 */
import { ResourceKind } from "../core/GameTypes";
import type { ResourceManager } from "../core/ResourceManager";

export const TaxRate      = 0.30;
export const CoinageTax   = 0.20;
export const BankingTax   = 0.00;

/** Compute how much the receiver gets from a raw tribute amount. */
export function tributeReceived(amount: number, tax: number): number {
  return Math.floor(amount * (1 - tax));
}

/**
 * Send `amount` of `kind` from `fromRm` to `toRm`.
 * Returns false if sender can't afford it.
 * tax: 0=Banking, 0.2=Coinage, 0.3=none
 */
export function tribute(
  fromRm: ResourceManager,
  toRm: ResourceManager,
  kind: ResourceKind,
  amount: number,
  tax = TaxRate,
): boolean {
  if (amount <= 0) return false;
  if (fromRm === toRm) return false;
  if (fromRm.get(kind) < amount) return false;

  const received = tributeReceived(amount, tax);
  fromRm.gain(kind, -amount);
  toRm.gain(kind, received);
  fromRm.onChange?.();
  toRm.onChange?.();
  return true;
}
