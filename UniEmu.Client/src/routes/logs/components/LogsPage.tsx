import { PagePlaceholder } from '@/components/PagePlaceholder';
import { localization } from '@/localization';

export function LogsPage() {
  return (
    <PagePlaceholder
      title={localization.routes.logs.components.logsPage.text1}
      description={localization.routes.logs.components.logsPage.text2}
    />
  );
}
