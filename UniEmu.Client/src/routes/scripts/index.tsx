import { createFileRoute } from '@tanstack/react-router';
import { ScriptsPage } from './components/ScriptsPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/scripts/')({
  head: () => ({
    meta: [
      { title: localization.routes.scripts.index.text1 },
      {
        name: 'description',
        content: localization.routes.scripts.index.text2,
      },
    ],
  }),
  component: ScriptsPage,
});
