export function markdown(value?: string) {
  return value ? { value } : undefined;
}

export function hoverContents(signature: string, documentation?: string) {
  return [
    { value: `\`\`\`csharp\n${signature}\n\`\`\`` },
    ...(documentation ? [{ value: documentation.replace(/\n/g, '  \n') }] : []),
  ];
}
