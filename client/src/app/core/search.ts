/** Case-insensitive "any field contains the query" match; an empty query matches everything. */
export function matchesQuery(query: string, ...fields: (string | null | undefined)[]): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  return fields.some((field) => !!field && field.toLowerCase().includes(q));
}
