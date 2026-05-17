import { createFileRoute } from '@tanstack/react-router';
import { DashboardPage } from './components/DashboardPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/')({
  head: () => ({
    meta: [
      { title: localization.routes.index.title },
      { name: 'description', content: localization.routes.index.description },
    ],
  }),
  component: DashboardPage,
});
