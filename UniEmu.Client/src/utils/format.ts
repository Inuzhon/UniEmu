import { formatDistanceToNow } from 'date-fns';
import { ru } from 'date-fns/locale';

export function timeAgo(iso: string | null): string {
  if (!iso) return '-';
  return formatDistanceToNow(new Date(iso), { addSuffix: true, locale: ru });
}

export function formatUptime(seconds: number): string {
  if (!seconds) return '-';
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (d) return `${d}д ${h}ч`;
  if (h) return `${h}ч ${m}м`;
  return `${m}м`;
}

export function formatNumber(n: number): string {
  return new Intl.NumberFormat('ru-RU').format(n);
}
