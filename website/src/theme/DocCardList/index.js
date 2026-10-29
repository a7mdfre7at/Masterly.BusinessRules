import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import {
  filterDocCardListItems,
  useCurrentSidebarSiblings,
  useDocById,
} from '@docusaurus/plugin-content-docs/client';
import Icon from '@site/src/components/home/Icon';
import styles from './styles.module.css';

// Ejected from @docusaurus/theme-classic (marked safe to swizzle). Renders the cards on generated category pages
// with the site's icon set and full page descriptions instead of emoji and truncated text.
const iconByDocId = {
  'overview': 'layers',
  'installation': 'box',
  'quick-start': 'zap',
  'writing-rules': 'braces',
  'building-rules': 'braces',
  'subject-typed-rules': 'box',
  'combining-rules': 'layers',
  'running-many-rules': 'checks',
  'understanding-failures': 'tree',
  'conditional-and-cached-rules': 'clock',
  'feature-flags': 'flag',
  'per-tenant-rules': 'building',
  'specifications': 'database',
  'rule-expressions': 'braces',
  'logging-tracing-metrics': 'activity',
  'dependency-injection': 'plug',
  'testing': 'flask',
  'examples': 'shuffle',
  'api-reference': 'tree',
  'upgrading-from-2x': 'arrowRight',
};

function CardBody({icon, title, description}) {
  return (
    <>
      <span className={styles.icon}>
        <Icon name={icon} size={18} />
      </span>
      <span className={styles.title}>
        {title}
        <Icon name="arrowRight" size={14} className={styles.arrow} />
      </span>
      {description && <span className={styles.description}>{description}</span>}
    </>
  );
}

function DocLinkCard({item}) {
  const doc = useDocById(item.docId ?? undefined);
  const description = item.description ?? doc?.description;
  return (
    <Link to={item.href} className={styles.card}>
      <CardBody icon={iconByDocId[item.docId] ?? 'braces'} title={item.label} description={description} />
    </Link>
  );
}

function CategoryCard({item}) {
  const href = item.href ?? item.items?.find((child) => child.href)?.href;
  if (!href) {
    return null;
  }
  const count = item.items?.length ?? 0;
  return (
    <Link to={href} className={styles.card}>
      <CardBody
        icon="layers"
        title={item.label}
        description={item.description ?? `${count} ${count === 1 ? 'page' : 'pages'}`}
      />
    </Link>
  );
}

function DocCardListForCurrentSidebarCategory({className}) {
  const items = useCurrentSidebarSiblings();
  return <DocCardList items={items} className={className} />;
}

/**
 * A grid of linked cards, one per sidebar item.
 */
export default function DocCardList(props) {
  const {items, className} = props;
  if (!items) {
    return <DocCardListForCurrentSidebarCategory {...props} />;
  }
  const filteredItems = filterDocCardListItems(items);
  return (
    <ul className={clsx(styles.grid, className)}>
      {filteredItems.map((item, index) => (
        <li key={index} className={styles.item}>
          {item.type === 'category' ? <CategoryCard item={item} /> : <DocLinkCard item={item} />}
        </li>
      ))}
    </ul>
  );
}
