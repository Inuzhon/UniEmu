import type { ITextModel } from './types';

const navigationEnabledModelUris = new Set<string>();

export function setModelNavigationEnabled(model: ITextModel, enabled: boolean) {
  const uri = model.uri.toString();
  if (enabled) {
    navigationEnabledModelUris.add(uri);
    return;
  }

  navigationEnabledModelUris.delete(uri);
}

export function clearModelNavigationEnabled(model: ITextModel) {
  navigationEnabledModelUris.delete(model.uri.toString());
}

export function isNavigationEnabledForModel(model: ITextModel) {
  return navigationEnabledModelUris.has(model.uri.toString());
}
