/** Drops every trailing `/`, so a configured base URL joins with a path the same way with or without one. */
export function trimTrailingSlashes(value: string): string {
  let end = value.length;
  while (end > 0 && value[end - 1] === '/') {
    end--;
  }
  return value.slice(0, end);
}
