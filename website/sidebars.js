// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docs: [
    {
      type: 'category',
      label: 'Getting Started',
      collapsed: false,
      items: ['overview', 'installation', 'quick-start'],
    },
    {
      type: 'category',
      label: 'Writing Rules',
      collapsed: false,
      items: ['writing-rules', 'building-rules', 'subject-typed-rules', 'combining-rules'],
    },
    {
      type: 'category',
      label: 'Running Rules',
      collapsed: false,
      items: ['running-many-rules', 'understanding-failures', 'conditional-and-cached-rules'],
    },
    {
      type: 'category',
      label: 'Advanced Topics',
      items: ['feature-flags', 'per-tenant-rules', 'specifications', 'rule-expressions'],
    },
    {
      type: 'category',
      label: 'Integrations',
      items: ['logging-tracing-metrics', 'dependency-injection', 'testing'],
    },
    'examples',
    {
      type: 'category',
      label: 'Reference',
      items: ['api-reference', 'upgrading-from-2x'],
    },
  ],
};

export default sidebars;
