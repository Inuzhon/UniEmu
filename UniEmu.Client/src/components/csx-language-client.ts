export function buildCsxDocumentUri(input: {
  id: string;
  name: string;
  scope: string;
  emulatorId?: string;
}) {
  const params = new URLSearchParams({
    name: input.name,
    scope: input.scope,
  });
  if (input.emulatorId) params.set('emulatorId', input.emulatorId);
  return `uniemu://scripts/${encodeURIComponent(input.id)}/${encodeURIComponent(input.name)}?${params.toString()}`;
}
