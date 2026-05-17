import { createFileRoute } from '@tanstack/react-router';
import { SettingsPage } from './components/SettingsPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/settings/')({
  head: () => ({ meta: [{ title: localization.routes.settings.index.title }] }),
  component: SettingsPage,
});
