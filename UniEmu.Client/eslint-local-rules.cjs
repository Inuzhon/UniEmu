'use strict';

const CYRILLIC_PATTERN = /[\u0400-\u04FF]/u;

const DEFAULT_ALLOWED_FILE_PATTERNS = [
  String.raw`(^|/)src/(i18n|locale|locales|messages|translations)(/|$)`,
  String.raw`(^|/)src/localization\.[cm]?[jt]sx?$`,
  String.raw`(^|/)[^/]+\.(i18n|locale|locales|messages|translations)\.[cm]?[jt]sx?$`,
];

function normalizeFilename(filename) {
  return filename.replace(/\\/g, '/');
}

function matchesAnyPattern(filename, patterns) {
  return patterns.some((pattern) => new RegExp(pattern, 'u').test(filename));
}

function hasRussianText(value) {
  return typeof value === 'string' && CYRILLIC_PATTERN.test(value);
}

function isJsxAttributeLiteral(node) {
  return node.parent?.type === 'JSXAttribute';
}

function isDirectiveLiteral(node) {
  return node.parent?.type === 'ExpressionStatement' && node.parent.directive;
}

module.exports = {
  'no-russian-localization-in-markup': {
    meta: {
      type: 'problem',
      docs: {
        description: 'disallow Russian UI copy in JSX markup and component string literals',
      },
      messages: {
        russianText:
          'Russian UI text must be moved to localization files instead of being written directly in markup.',
      },
      schema: [
        {
          type: 'object',
          additionalProperties: false,
          properties: {
            allowedFiles: {
              type: 'array',
              items: { type: 'string' },
            },
          },
        },
      ],
    },
    create(context) {
      const filename = normalizeFilename(
        context.filename ?? context.getFilename?.() ?? context.physicalFilename ?? ''
      );
      const options = context.options[0] ?? {};
      const allowedFiles = [...DEFAULT_ALLOWED_FILE_PATTERNS, ...(options.allowedFiles ?? [])];

      if (matchesAnyPattern(filename, allowedFiles)) {
        return {};
      }

      function reportIfRussian(node, value) {
        if (!hasRussianText(value)) {
          return;
        }

        context.report({
          node,
          messageId: 'russianText',
        });
      }

      return {
        JSXAttribute(node) {
          if (node.value?.type !== 'Literal') {
            return;
          }

          reportIfRussian(node.value, node.value.value);
        },
        JSXText(node) {
          reportIfRussian(node, node.value);
        },
        Literal(node) {
          if (isJsxAttributeLiteral(node) || isDirectiveLiteral(node)) {
            return;
          }

          reportIfRussian(node, node.value);
        },
        TemplateElement(node) {
          reportIfRussian(node, node.value.raw);
        },
      };
    },
  },
};
