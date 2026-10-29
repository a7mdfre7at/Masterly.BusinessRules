// @ts-check

/**
 * A sidebar category whose label links to a generated overview page of cards (one per page, using each
 * page's front-matter description).
 * @param {string} label
 * @param {string} slug
 * @param {string} description
 * @param {string[]} items
 * @param {boolean} [collapsed]
 * @returns {import('@docusaurus/plugin-content-docs').SidebarItemCategory}
 */
function section(label, slug, description, items, collapsed = false) {
  return {
    type: 'category',
    label,
    collapsed,
    link: {type: 'generated-index', slug: `/category/${slug}`, title: label, description},
    items,
  };
}

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docs: [
    section('Getting Started', 'getting-started', 'Install the packages and write your first rule.', [
      'overview',
      'installation',
      'quick-start',
    ]),
    section('Writing Rules', 'writing-rules', 'Every way to define a rule, and how to combine rules into bigger ones.', [
      'writing-rules',
      'building-rules',
      'subject-typed-rules',
      'combining-rules',
    ]),
    section('Running Rules', 'running-rules', 'Check rules one at a time or in batches, and read what failed and why.', [
      'running-many-rules',
      'understanding-failures',
      'conditional-and-cached-rules',
    ]),
    section(
      'Advanced Topics',
      'advanced-topics',
      'Feature-flag rollout, per-tenant parameters, database specifications and text expressions.',
      ['feature-flags', 'per-tenant-rules', 'specifications', 'rule-expressions'],
    ),
    section('Integrations', 'integrations', 'Logging and tracing, dependency injection, and test tools.', [
      'logging-tracing-metrics',
      'dependency-injection',
      'testing',
    ]),
    'examples',
    section('Reference', 'reference', 'The public API of every package, and the 2.x to 3.0 upgrade guide.', [
      'api-reference',
      'upgrading-from-2x',
    ]),
  ],
};

export default sidebars;
