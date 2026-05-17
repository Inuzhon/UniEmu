import { createBrowserHistory, createRouter } from '@tanstack/react-router';
import { DefaultErrorComponent } from './components/DefaultErrorComponent';
import { routeTree } from './routeTree.gen';

export const router = createRouter({
  routeTree,
  context: {},
  scrollRestoration: true,
  defaultPreloadStaleTime: 0,
  defaultErrorComponent: DefaultErrorComponent,
  history: typeof window !== 'undefined' ? createBrowserHistory() : undefined,
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
