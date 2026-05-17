import { localization } from '@/localization';
const TEXT_EXTENSIONS = [
  '.nc',
  '.gcode',
  '.g',
  '.tap',
  '.cnc',
  '.mpf',
  '.spf',
  '.ngc',
  '.eia',
  '.txt',
  '.prg',
  '.min',
  '.pim',
  '.sub',
];

export const isTextByName = (name: string) =>
  TEXT_EXTENSIONS.some((ext) => name.toLowerCase().endsWith(ext));

export const fmtSize = (bytes: number) => {
  if (bytes < 1024) return localization.routes.cnc.utils.index.text1(bytes);
  if (bytes < 1024 * 1024)
    return localization.routes.cnc.utils.index.text2((bytes / 1024).toFixed(1));
  return localization.routes.cnc.utils.index.text3((bytes / (1024 * 1024)).toFixed(2));
};
