import { createFileRoute } from '@tanstack/react-router';
import { CncStoragePage } from './components/CncStoragePage';
import { localization } from '@/localization';

export const Route = createFileRoute('/cnc/')({
  head: () => ({
    meta: [
      { title: localization.routes.cnc.index.text1 },
      {
        name: 'description',
        content: localization.routes.cnc.index.text2,
      },
    ],
  }),
  component: CncStoragePage,
});
