import { createFileRoute, Link } from '@tanstack/react-router';
import { useUniEmuStore } from '@/store/uniemu-store';
import { EmulatorDetailPage } from './components/EmulatorDetailPage';
import { localization } from '@/localization';

export const Route = createFileRoute('/emulators/$id')({
  loader: ({ params }) => {
    return { emulatorId: params.id };
  },
  head: ({ loaderData }) => ({
    meta: [
      {
        title: `UniEmu - ${
          useUniEmuStore.getState().emulators.find((e) => e.id === loaderData?.emulatorId)?.name ??
          localization.routes.emulators.id.title
        }`,
      },
    ],
  }),
  notFoundComponent: () => (
    <div className="p-12 text-center">
      <p className="text-lg">{localization.routes.emulators.id.notFoundTitle}</p>
      <Link to="/emulators" className="mt-4 inline-block text-primary hover:underline">
        {localization.routes.emulators.id.backToListLabel}
      </Link>
    </div>
  ),
  errorComponent: ({ error }) => (
    <div className="p-12 text-center text-signal-offline">{error.message}</div>
  ),
  component: EmulatorDetailPage,
});
