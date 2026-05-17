import { createFileRoute } from '@tanstack/react-router';
import { EmulatorsListPage } from './components/EmulatorsListPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/emulators/')({
  head: () => ({
    meta: [
      { title: localization.routes.emulators.index.text1 },
      { name: 'description', content: localization.routes.emulators.index.text2 },
    ],
  }),
  component: EmulatorsListPage,
});
