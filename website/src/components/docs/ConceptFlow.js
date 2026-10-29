import React from 'react';
import Link from '@docusaurus/Link';
import styles from './ConceptFlow.module.css';

const stages = [
  {
    step: '1',
    title: 'Define',
    text: 'A rule has a code, a message and a condition. It is broken when the condition is true.',
    apis: ['BaseBusinessRule', 'BusinessRuleBuilder', 'BaseBusinessRule<T>'],
    to: '/docs/writing-rules',
  },
  {
    step: '2',
    title: 'Compose',
    text: 'Combine small rules into bigger ones. A combined rule is still just a rule.',
    apis: ['.And()  .Or()  .Not()', 'RuleGroup.All / Any', 'RuleGroup.AtLeast(n)'],
    to: '/docs/combining-rules',
  },
  {
    step: '3',
    title: 'Run',
    text: 'Check one rule, or a whole batch with options for severity, faults and observers.',
    apis: ['rule.Check()', 'BusinessRuleChecker.CheckAll', 'BusinessRuleChecker.EvaluateAll'],
    to: '/docs/running-many-rules',
  },
  {
    step: '4',
    title: 'Understand',
    text: 'Get every broken rule with its details, or a tree of the whole evaluation.',
    apis: ['BusinessRuleResult', 'RuleEvaluationResult', 'rule.Explain()'],
    to: '/docs/understanding-failures',
  },
];

const wrappers = [
  {api: '.When(...)', text: 'run only if', to: '/docs/conditional-and-cached-rules#run-a-rule-only-when-a-condition-holds'},
  {api: '.Cached(...)', text: 'reuse the verdict', to: '/docs/conditional-and-cached-rules#cache-expensive-rules'},
  {api: '.GatedBy(...)', text: 'feature flag', to: '/docs/feature-flags#gating-a-rule'},
  {api: '.WithSeverity(...)', text: 'report differently', to: '/docs/running-many-rules#warnings-that-do-not-block'},
  {api: '.ToAsync()', text: 'mix sync and async', to: '/docs/conditional-and-cached-rules#mixing-sync-and-async-rules'},
];

// Long member names such as BusinessRuleChecker.EvaluateAll may wrap only after a dot, never mid-word.
function withBreaksAfterDots(api) {
  return api.split(/(?<=\.)/).map((part, index) => (
    <React.Fragment key={index}>
      {index > 0 && <wbr />}
      {part}
    </React.Fragment>
  ));
}

/**
 * The four stages of working with rules, plus the wrappers that apply to any rule.
 */
export default function ConceptFlow() {
  return (
    <div className={styles.flow}>
      <ol className={styles.stages}>
        {stages.map((stage) => (
          <li key={stage.title} className={styles.stage}>
            <Link to={stage.to} className={styles.stageLink}>
              <span className={styles.step} aria-hidden="true">
                {stage.step}
              </span>
              <span className={styles.title}>{stage.title}</span>
              <span className={styles.text}>{stage.text}</span>
              <span className={styles.apis}>
                {stage.apis.map((api) => (
                  <code key={api}>{withBreaksAfterDots(api)}</code>
                ))}
              </span>
            </Link>
          </li>
        ))}
      </ol>
      <div className={styles.wrappers}>
        <span className={styles.wrappersLabel}>Wrap any rule</span>
        <ul className={styles.wrapperList}>
          {wrappers.map((wrapper) => (
            <li key={wrapper.api}>
              <Link to={wrapper.to} className={styles.wrapper}>
                <code>{wrapper.api}</code>
                <span>{wrapper.text}</span>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
