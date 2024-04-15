import { RuleConfigSeverity } from '@commitlint/types';

export default {
  extends: ['@commitlint/config-conventional'],
  parserPreset: 'conventional-changelog-conventionalcommits',
  rules: {
    'scope-enum': [RuleConfigSeverity.Error, 'always', [
        '',
        'deps',
        'controller-container',
        'trino-container',
        'fizzbuzz-chart',
        'fizzbuzz-crds-chart'
    ]],
    'subject-case': [RuleConfigSeverity.Error, 'never', []],
  }
};
