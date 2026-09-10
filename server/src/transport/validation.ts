import type { ZodError } from "zod";

/** Extrai uma mensagem legível do primeiro erro de campo, para complementar `details` no corpo da resposta. */
export function firstValidationMessage(error: ZodError): string {
  const fieldErrors = Object.values(error.flatten().fieldErrors).flat();
  return fieldErrors[0] ?? "Dados inválidos.";
}
