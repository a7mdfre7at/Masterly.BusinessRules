import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';
import styles from './index.module.css';

const sampleRule = `public sealed class OrderMustHaveItemsRule : BaseBusinessRule
{
    private readonly Order _order;

    public OrderMustHaveItemsRule(Order order)
    {
        _order = order;
    }

    public override string Code => "ORDER.NO_ITEMS";

    public override string Message => "An order must contain at least one item.";

    public override bool IsBroken() => _order.Items.Count == 0;
}

BusinessRuleChecker.CheckAll(
    new OrderMustHaveItemsRule(order),
    new CustomerMustBeActiveRule(customer));`;

const features = [
  {
    title: 'Small, named rules',
    to: '/docs/writing-rules',
    description: 'Write each rule as a class or build it inline from a lambda. Every rule has a code, a message and a severity.',
  },
  {
    title: 'Compose and explain',
    to: '/docs/combining-rules',
    description: 'Combine rules with And, Or, Not and groups, then print a tree that shows exactly which part failed.',
  },
  {
    title: 'Run many at once',
    to: '/docs/running-many-rules',
    description: 'Check a batch, collect every failure or stop at the first, filter by severity, category or tag.',
  },
  {
    title: 'Roll out safely',
    to: '/docs/feature-flags',
    description: 'Ship a rule in shadow mode behind a feature flag, then enforce it for a percentage of callers.',
  },
  {
    title: 'Per-tenant limits',
    to: '/docs/per-tenant-rules',
    description: 'Give each tenant its own parameters, such as a maximum order amount, without changing rule code.',
  },
  {
    title: 'Observe and test',
    to: '/docs/testing',
    description: 'Log, trace and measure every evaluation, and test rules with coverage, boundary and property checks.',
  },
];

function Hero() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.hero)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          {siteConfig.title}
        </Heading>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
        <div className={styles.buttons}>
          <Link className="button button--secondary button--lg" to="/docs/quick-start">
            Get started
          </Link>
          <Link className="button button--outline button--secondary button--lg" to="/docs/api-reference">
            API reference
          </Link>
        </div>
        <code className={styles.install}>dotnet add package Masterly.BusinessRules</code>
      </div>
    </header>
  );
}

function Feature({title, to, description}) {
  return (
    <div className={clsx('col col--4', styles.feature)}>
      <Heading as="h3">
        <Link to={to}>{title}</Link>
      </Heading>
      <p>{description}</p>
    </div>
  );
}

export default function Home() {
  return (
    <Layout description="A composable business rule engine for .NET: sync and async rules, composition, explain trees, feature-flag rollout, per-tenant parameters and test tools.">
      <Hero />
      <main>
        <section className={styles.section}>
          <div className="container">
            <div className="row">
              {features.map((feature) => (
                <Feature key={feature.title} {...feature} />
              ))}
            </div>
          </div>
        </section>
        <section className={styles.section}>
          <div className={clsx('container', styles.sample)}>
            <Heading as="h2">A rule in a few lines</Heading>
            <CodeBlock language="csharp">{sampleRule}</CodeBlock>
          </div>
        </section>
      </main>
    </Layout>
  );
}
