import { RuleConfigSeverity } from '@commitlint/types';

module.exports = {
  extends: ['@commitlint/config-conventional'],
  parserPreset: 'conventional-changelog-conventionalcommits',
  rules: {
    'scope-enum': [RuleConfigSeverity.Error, 'always', [
        '',
        'deps',
        'controller-container',
        'fizzbuzz-chart',
        'fizzbuzz-crds-chart'
    ]]
  }
};
