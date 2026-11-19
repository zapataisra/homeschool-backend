// Resolución de URLs de recursos estáticos (p. ej. las láminas SVG de las lecciones).
// Si VITE_ASSETS_BASE_URL está definida (un CDN u origen de assets), se antepone; si no,
// se devuelve la ruta relativa (mismo origen). Nunca produce doble slash.

export function resolveAssetUrl(path: string): string {
  const base = (import.meta.env.VITE_ASSETS_BASE_URL as string | undefined)?.trim();
  if (!base) return path;
  const baseWithoutTrailingSlash = base.replace(/\/+$/, "");
  const pathWithoutLeadingSlash = path.replace(/^\/+/, "");
  return `${baseWithoutTrailingSlash}/${pathWithoutLeadingSlash}`;
}