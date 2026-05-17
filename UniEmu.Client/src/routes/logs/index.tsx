import { createFileRoute } from '@tanstack/react-router';
import { LogsPage } from './components/LogsPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/logs/')({
  head: () => ({ meta: [{ title: localization.routes.logs.index.text1 }] }),
  component: LogsPage,
});
