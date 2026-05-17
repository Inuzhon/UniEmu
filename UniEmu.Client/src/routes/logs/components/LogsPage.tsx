import { PagePlaceholder } from '@/components/PagePlaceholder';
import { localization } from '@/localization';

export function LogsPage() {
  return (
    <PagePlaceholder
      title={localization.routes.logs.components.logsPage.title}
      description={localization.routes.logs.components.logsPage.description}
    />
  );
}
