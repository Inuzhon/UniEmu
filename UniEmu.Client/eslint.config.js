import js from '@eslint/js';
import ts from 'typescript-eslint';
import react from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import prettier from 'eslint-config-prettier';
import importPlugin from 'eslint-plugin-import';
import localRules from 'eslint-plugin-local-rules';
import simpleImportSort from 'eslint-plugin-simple-import-sort';
import globals from 'globals';

export default [
  {
    ignores: ['.bun-cache/**', '.vscode/**', 'dist/**', 'node_modules/**', 'src/routeTree.gen.ts'],
  },
  js.configs.recommended,
  ...ts.configs.recommended,
  {
    files: ['eslint-local-rules.cjs'],
    languageOptions: {
      globals: globals.node,
    },
  },
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      parser: ts.parser,
      parserOptions: {
        ecmaVersion: 2020,
        sourceType: 'module',
        project: './tsconfig.json',
      },
      globals: globals.browser,
    },
    plugins: {
      '@typescript-eslint': ts.plugin,
      react: react,
      'react-hooks': reactHooks,
      import: importPlugin,
      'local-rules': localRules,
      'simple-import-sort': simpleImportSort,
    },
    settings: {
      'import/resolver': {
        typescript: {
          project: './tsconfig.json',
        },
        node: {
          extensions: ['.js', '.jsx', '.ts', '.tsx'],
        },
      },
    },
    rules: {
      'no-unused-vars': 'off',
      '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
      'react/prop-types': 'off',
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',

      'import/no-extraneous-dependencies': ['error', { optionalDependencies: true }],
      'import/no-unresolved': ['error', { ignore: ['^@/', '^umi/'] }],
      quotes: ['error', 'single'],
      'spaced-comment': ['error', 'always'],
    },
  },
  {
    files: ['src/**/*.{jsx,tsx}'],
    rules: {
      'local-rules/no-russian-localization-in-markup': 'error',
    },
  },
  prettier,
];
