import { createFileRoute } from '@tanstack/react-router';
import { DashboardPage } from './components/DashboardPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/')({
  head: () => ({
    meta: [
      { title: localization.routes.index.text1 },
      { name: 'description', content: localization.routes.index.text2 },
    ],
  }),
  component: DashboardPage,
});
