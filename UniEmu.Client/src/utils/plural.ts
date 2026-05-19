type RussianCountForms = readonly [one: string, few: string, many: string];

const pluralRules = new Intl.PluralRules('ru-RU');

export function getRussianCountForm(count: number, forms: RussianCountForms): string {
  const category = pluralRules.select(Math.abs(count));

  if (category === 'one') return forms[0];
  if (category === 'few') return forms[1];

  return forms[2];
}

export function formatCount(count: number, forms: RussianCountForms): string {
  return `${count} ${getRussianCountForm(count, forms)}`;
}
