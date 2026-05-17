import { createFileRoute } from '@tanstack/react-router';
import { ScriptsPage } from './components/ScriptsPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/scripts/')({
  head: () => ({
    meta: [
      { title: localization.routes.scripts.index.title },
      {
        name: 'description',
        content: localization.routes.scripts.index.description,
      },
    ],
  }),
  component: ScriptsPage,
});
