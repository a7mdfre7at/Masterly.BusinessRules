import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';
import Icon from '@site/src/components/home/Icon';
import CopyCommand from '@site/src/components/home/CopyCommand';
import ExplainDemo from '@site/src/components/home/ExplainDemo';
import styles from './index.module.css';

// Every sample below is copied from the docs pages it links to.
const ruleStyles = [
  {
    value: 'class',
    label: 'Rule class',
    caption: 'Derive from BaseBusinessRule. A rule is broken when its condition is true.',
    to: '/docs/writing-rules',
    code: `public sealed class OrderMustHaveItemsRule : BaseBusinessRule
{
    private readonly Order _order;

    public OrderMustHaveItemsRule(Order order)
    {
        _order = order;
    }

    public override string Code => "ORDER.NO_ITEMS";

    public override string Message => "An order must contain at least one item.";

    public override bool IsBroken() => _order.Items.Count == 0;
}`,
  },
  {
    value: 'builder',
    label: 'Builder',
    caption: 'Build a rule inline from a lambda, with every metadata field on the builder.',
    to: '/docs/building-rules',
    code: `IBusinessRule minimumTotalRule = BusinessRuleBuilder.Create("ORDER.BELOW_MINIMUM")
    .WithName("Minimum order total")
    .WithMessage("Order total must be at least 10.00.")
    .WithSeverity(RuleSeverity.Error)
    .WithCategory("Checkout")
    .WithTags("orders", "checkout")
    .When(() => order.Total < 10.00m)
    .Build();

minimumTotalRule.Check(); // throws BusinessRuleValidationException when broken`,
  },
  {
    value: 'typed',
    label: 'Subject-typed',
    caption: 'One stateless instance validates whichever order you pass. Ideal for DI singletons.',
    to: '/docs/subject-typed-rules',
    code: `public sealed class OrderItemsRequiredRule : BaseBusinessRule<Order>
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";

    public override bool IsBroken(Order order) => order.Items.Count == 0;
}

IBusinessRule<Order> rule = new OrderItemsRequiredRule();
bool isBroken = rule.IsBroken(order);`,
  },
  {
    value: 'expression',
    label: 'Text expression',
    caption: 'Keep conditions in configuration. Parameters resolve per tenant at evaluation time.',
    to: '/docs/rule-expressions',
    code: `IBusinessRule orderLimit = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithMessage("Order exceeds the customer's limit.")
    .WithCategory("Orders")
    .WithParameter("maxOrderAmount", 10_000m)
    .WhenExpression("amount > @maxOrderAmount")
    .Build();`,
  },
];

const features = [
  {
    icon: 'flag',
    title: 'Shadow rollout behind feature flags',
    text: 'Ship a new rule in shadow mode: violations are reported, never blocking. Then enforce it for a percentage of callers, then for everyone.',
    to: '/docs/feature-flags',
    size: 'wide',
    visual: 'rollout',
  },
  {
    icon: 'layers',
    title: 'Compose anything',
    text: 'And, Or and Not, plus All, Any, AtLeast and Exactly groups with short-circuit and parallel evaluation.',
    to: '/docs/combining-rules',
  },
  {
    icon: 'tree',
    title: 'Explain every verdict',
    text: 'One evaluation returns a tree of every node it visited, with outcome, reason and timing.',
    to: '/docs/understanding-failures',
  },
  {
    icon: 'checks',
    title: 'Batch checking',
    text: 'Run many rules in one call. Filter by severity, category or tag, pick a fault policy, read the ledger.',
    to: '/docs/running-many-rules',
  },
  {
    icon: 'database',
    title: 'Specifications to SQL',
    text: 'The same rule checks one object in memory and filters an IQueryable in the database.',
    to: '/docs/specifications',
  },
  {
    icon: 'braces',
    title: 'Text expressions',
    text: 'Write amount > @maxOrderAmount as text and see the exact values it was evaluated with.',
    to: '/docs/rule-expressions',
  },
  {
    icon: 'building',
    title: 'Per-tenant parameters',
    text: 'Externalize limits per tenant without touching rule code. The most specific matching scope wins.',
    to: '/docs/per-tenant-rules',
    size: 'wide',
    visual: 'tenants',
  },
  {
    icon: 'clock',
    title: 'Conditional and cached',
    text: 'Run a rule only when a precondition holds. Cache slow verdicts for a time, per rule or per key.',
    to: '/docs/conditional-and-cached-rules',
  },
  {
    icon: 'activity',
    title: 'Logs, traces, metrics',
    text: 'Structured ILogger entries, OpenTelemetry spans and Meter instruments for every evaluation.',
    to: '/docs/logging-tracing-metrics',
  },
  {
    icon: 'flask',
    title: 'Test tools built in',
    text: 'Outcome coverage, a boundary finder, CsCheck property checks and smoke-test mutants.',
    to: '/docs/testing',
  },
];

const packages = [
  {name: 'Masterly.BusinessRules', role: 'Core', text: 'Rules, builders, composition, batch checking, context, caching, feature gates, parameters and specifications.', target: 'netstandard2.1', deps: 'No dependencies', to: '/docs/overview'},
  {name: 'Masterly.BusinessRules.Expressions', role: 'Expressions', text: 'Rule conditions written as text, with explanations that show the bound values.', target: 'netstandard2.1', deps: 'Core only', to: '/docs/rule-expressions'},
  {name: 'Masterly.BusinessRules.Diagnostics', role: 'Diagnostics', text: 'Structured ILogger logging, OpenTelemetry spans and metrics for rule evaluation.', target: 'netstandard2.1', deps: 'Logging.Abstractions, DiagnosticSource', to: '/docs/logging-tracing-metrics'},
  {name: 'Masterly.BusinessRules.DependencyInjection', role: 'Dependency injection', text: 'Assembly scanning, injectable rule runners and per-tenant rule sets.', target: 'netstandard2.1', deps: 'DependencyInjection.Abstractions', to: '/docs/dependency-injection'},
  {name: 'Masterly.BusinessRules.FeatureManagement', role: 'Feature management', text: 'Drives feature-gated rules from Microsoft.FeatureManagement flags and variants.', target: 'netstandard2.1', deps: 'Microsoft.FeatureManagement', to: '/docs/feature-flags#using-microsoftfeaturemanagement'},
  {name: 'Masterly.BusinessRules.Testing', role: 'Testing', text: 'Outcome coverage, boundary finder, property checks and mutants for your test projects.', target: 'net8.0 · net10.0', deps: 'CsCheck', to: '/docs/testing'},
];

const rolloutSteps = [
  {label: 'Off', detail: 'Skipped, passes', tone: 'off'},
  {label: 'Shadow', detail: 'Reports, never blocks', tone: 'shadow'},
  {label: 'Enforced 10%', detail: 'Enforced for 10% of callers', tone: 'partial'},
  {label: 'Enforced', detail: 'Enforced for everyone', tone: 'full'},
];

// Values from the externalized-parameters example in docs/per-tenant-rules.md, for a request from ACME in the EU.
const tenantRows = [
  {scope: 'Global', value: '10,000'},
  {scope: 'tenant=acme', value: '50,000'},
  {scope: 'region=EU;tenant=acme', value: '70,000', winner: true},
];

function Hero() {
  const {siteConfig} = useDocusaurusContext();
  const version = siteConfig.customFields.packageVersion;
  return (
    <header className={styles.hero}>
      <div className={styles.heroBackdrop} aria-hidden="true" />
      <div className={clsx('container', styles.heroGrid)}>
        <div className={styles.heroCopy}>
          <Link className={styles.eyebrow} to="/docs/upgrading-from-2x">
            <span className={styles.eyebrowVersion}>v{version}</span>
            <span>Open source · MIT · .NET Standard 2.1</span>
            <Icon name="arrowRight" size={14} />
          </Link>
          <Heading as="h1" className={styles.heroTitle}>
            Business rules that <span className={styles.heroAccent}>explain themselves.</span>
          </Heading>
          <p className={styles.heroLead}>
            Write each rule once as a small, named unit with a code and a message. Compose rules, check them in batches
            and roll them out behind feature flags. When a rule fails, you get a tree that shows exactly why.
          </p>
          <div className={styles.heroActions}>
            <Link className={clsx('button button--primary button--lg', styles.primaryButton)} to="/docs/quick-start">
              Get started
              <Icon name="arrowRight" size={18} />
            </Link>
            <Link className={clsx('button button--lg', styles.secondaryButton)} to="/docs/overview">
              Read the docs
            </Link>
          </div>
          <CopyCommand command="dotnet add package Masterly.BusinessRules" className={styles.heroInstall} />
        </div>
        <div className={styles.heroDemo}>
          <ExplainDemo />
        </div>
      </div>
    </header>
  );
}

function Stats() {
  const stats = [
    {value: '0', label: 'dependencies in the core package'},
    {value: '6', label: 'packages, one shared version'},
    {value: 'Sync + async', label: 'every concept exists in both forms'},
    {value: '.NET Core 3.0+', label: 'and .NET 5+ through netstandard2.1'},
  ];
  return (
    <section className={styles.stats} aria-label="At a glance">
      <div className={clsx('container', styles.statsGrid)}>
        {stats.map((stat) => (
          <div key={stat.label} className={styles.stat}>
            <div className={styles.statValue}>{stat.value}</div>
            <div className={styles.statLabel}>{stat.label}</div>
          </div>
        ))}
      </div>
    </section>
  );
}

function SectionHeader({eyebrow, title, children}) {
  return (
    <div className={styles.sectionHeader}>
      <div className={styles.sectionEyebrow}>{eyebrow}</div>
      <Heading as="h2" className={styles.sectionTitle}>
        {title}
      </Heading>
      {children && <p className={styles.sectionLead}>{children}</p>}
    </div>
  );
}

function RuleStyles() {
  return (
    <section className={styles.section}>
      <div className="container">
        <SectionHeader eyebrow="Write" title="Four ways to write a rule, one way to run it">
          Pick the style that fits the job. Every rule has the same code, message and severity, and works with every
          combinator, checker and explain tree.
        </SectionHeader>
        <div className={styles.tabsCard}>
          <Tabs groupId="rule-style" className={styles.tabs}>
            {ruleStyles.map((style) => (
              <TabItem key={style.value} value={style.value} label={style.label}>
                <p className={styles.tabCaption}>
                  {style.caption}{' '}
                  <Link to={style.to} className={styles.inlineLink}>
                    Learn more
                    <Icon name="arrowRight" size={14} />
                  </Link>
                </p>
                <CodeBlock language="csharp">{style.code}</CodeBlock>
              </TabItem>
            ))}
          </Tabs>
        </div>
      </div>
    </section>
  );
}

function RolloutVisual() {
  return (
    <ol className={styles.rollout} aria-label="Rollout stages">
      {rolloutSteps.map((step, index) => (
        <li key={step.label} className={clsx(styles.rolloutStep, styles[`rollout_${step.tone}`])}>
          <span className={styles.rolloutIndex}>{index + 1}</span>
          <span className={styles.rolloutLabel}>{step.label}</span>
          <span className={styles.rolloutDetail}>{step.detail}</span>
        </li>
      ))}
    </ol>
  );
}

function TenantVisual() {
  return (
    <table className={styles.tenants}>
      <caption className={styles.srOnly}>maxOrderAmount by scope</caption>
      <thead>
        <tr>
          <th scope="col">Scope</th>
          <th scope="col">@maxOrderAmount</th>
        </tr>
      </thead>
      <tbody>
        {tenantRows.map((row) => (
          <tr key={row.scope} className={row.winner ? styles.tenantWinner : undefined}>
            <td>{row.scope}</td>
            <td>
              {row.value}
              {row.winner && <span className={styles.tenantBadge}>wins</span>}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Features() {
  return (
    <section className={clsx(styles.section, styles.sectionAlt)}>
      <div className="container">
        <SectionHeader eyebrow="Capabilities" title="Everything a rule needs in production">
          Start with one rule. Add composition, rollout, tenancy and telemetry when you need them, without rewriting
          what you have.
        </SectionHeader>
        <div className={styles.bento}>
          {features.map((feature) => (
            <Link
              key={feature.title}
              to={feature.to}
              className={clsx(styles.featureCard, feature.size === 'wide' && styles.featureWide)}>
              <div className={styles.featureBody}>
                <span className={styles.featureIcon}>
                  <Icon name={feature.icon} size={20} />
                </span>
                <Heading as="h3" className={styles.featureTitle}>
                  {feature.title}
                </Heading>
                <p className={styles.featureText}>{feature.text}</p>
                <span className={styles.featureMore}>
                  Read the guide
                  <Icon name="arrowRight" size={14} />
                </span>
              </div>
              {feature.visual === 'rollout' && <RolloutVisual />}
              {feature.visual === 'tenants' && <TenantVisual />}
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

function Packages() {
  return (
    <section className={styles.section}>
      <div className="container">
        <SectionHeader eyebrow="Packages" title="Install the core. Add only what you use.">
          All six packages ship together with the same version number. Every companion package depends only on the core
          and the library it integrates with.
        </SectionHeader>
        <div className={styles.packages}>
          {packages.map((pkg) => (
            <Link key={pkg.name} to={pkg.to} className={styles.packageCard}>
              <div className={styles.packageTop}>
                <span className={styles.packageIcon}>
                  <Icon name="box" size={18} />
                </span>
                <span className={styles.packageRole}>{pkg.role}</span>
              </div>
              <code className={styles.packageName}>{pkg.name}</code>
              <p className={styles.packageText}>{pkg.text}</p>
              <div className={styles.packageMeta}>
                <span>{pkg.target}</span>
                <span>{pkg.deps}</span>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

function CallToAction() {
  return (
    <section className={styles.cta}>
      <div className={clsx('container', styles.ctaInner)}>
        <Heading as="h2" className={styles.ctaTitle}>
          Write your first rule in five minutes.
        </Heading>
        <p className={styles.ctaText}>The Quick Start defines two rules, checks one, then checks both together.</p>
        <div className={styles.heroActions}>
          <Link className={clsx('button button--primary button--lg', styles.primaryButton)} to="/docs/quick-start">
            Open the Quick Start
            <Icon name="arrowRight" size={18} />
          </Link>
          <Link className={clsx('button button--lg', styles.secondaryButton)} to="/docs/examples">
            See real-world examples
          </Link>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout description="A composable business rule engine for .NET: sync and async rules, composition, explain trees, feature-flag rollout, per-tenant parameters and test tools.">
      <Hero />
      <main>
        <Stats />
        <RuleStyles />
        <Features />
        <Packages />
        <CallToAction />
      </main>
    </Layout>
  );
}
