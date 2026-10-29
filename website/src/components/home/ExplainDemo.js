import React, {useCallback, useEffect, useRef, useState} from 'react';
import clsx from 'clsx';
import CodeBlock from '@theme/CodeBlock';
import Icon from './Icon';
import styles from './ExplainDemo.module.css';

// The code and output are the explain-tree example from docs/understanding-failures.md.
const code = `GroupBusinessRule kyc = RuleGroup.All(
        "KYC.INCOMPLETE",
        "All KYC checks must pass",
        identityVerified,
        addressVerified,
        sanctionsCleared)
    .WithShortCircuit();

RuleExplanation explanation = kyc.Explain(context);
Console.WriteLine(explanation.ToText());`;

const finalStep = 4;
const stepDelayMs = 650;

// resolveAt: the step at which the node shows its outcome. runs: whether the node is evaluated at all.
const rows = [
  {id: 'root', glyph: '', operator: 'ALL', code: 'KYC.INCOMPLETE', outcome: 'broken', message: 'All KYC checks must pass', duration: '0.9 ms', resolveAt: 4, runsFrom: 0},
  {id: 'identity', glyph: '├─', code: 'KYC.ID', outcome: 'passed', duration: '0.1 ms', resolveAt: 1, runsFrom: 0},
  {id: 'address', glyph: '├─', code: 'KYC.ADDRESS', outcome: 'broken', message: 'Address must be verified', duration: '0.2 ms', resolveAt: 2, runsFrom: 1},
  {id: 'sanctions', glyph: '└─', code: 'KYC.SANCTIONS', outcome: 'skipped', reason: 'reason: short-circuit', resolveAt: 3, runsFrom: null},
];

const outcomeLabels = {
  pending: 'PENDING',
  running: 'RUNNING',
  passed: 'PASSED',
  broken: 'BROKEN',
  skipped: 'NOT EVALUATED',
};

function stateOf(row, step) {
  if (step >= row.resolveAt) {
    return row.outcome;
  }
  if (row.runsFrom !== null && step >= row.runsFrom) {
    return 'running';
  }
  return 'pending';
}

function prefersReducedMotion() {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/**
 * A code window that "runs" the KYC rule group and reveals its explain tree node by node.
 * Every row is rendered from the start, so the animation never shifts the layout.
 */
export default function ExplainDemo() {
  const [step, setStep] = useState(0);
  const [running, setRunning] = useState(false);
  const container = useRef(null);
  const started = useRef(false);

  const run = useCallback(() => {
    if (prefersReducedMotion()) {
      setStep(finalStep);
      return;
    }
    setStep(0);
    setRunning(true);
  }, []);

  useEffect(() => {
    if (!running) {
      return undefined;
    }
    if (step >= finalStep) {
      setRunning(false);
      return undefined;
    }
    const timer = setTimeout(() => setStep((current) => current + 1), stepDelayMs);
    return () => clearTimeout(timer);
  }, [running, step]);

  useEffect(() => {
    const element = container.current;
    if (!element || typeof IntersectionObserver === 'undefined') {
      run();
      return undefined;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting) && !started.current) {
          started.current = true;
          run();
          observer.disconnect();
        }
      },
      {threshold: 0.4},
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [run]);

  const done = step >= finalStep;

  return (
    <div className={styles.window} ref={container}>
      <div className={styles.titleBar}>
        <span className={styles.dots} aria-hidden="true">
          <span />
          <span />
          <span />
        </span>
        <span className={styles.fileName}>KycCheck.cs</span>
        <button type="button" className={styles.runButton} onClick={run} disabled={running}>
          <Icon name={done ? 'replay' : 'play'} size={14} />
          <span>{done ? 'Run again' : running ? 'Running' : 'Run'}</span>
        </button>
      </div>

      <CodeBlock language="csharp" className={styles.code}>
        {code}
      </CodeBlock>

      <div className={styles.console}>
        <div className={styles.consoleHeader}>
          <span>Output</span>
          <span className={styles.status} role="status" aria-live="polite">
            {done ? (
              <>
                <span className={styles.statusBroken}>1 broken</span> · 1 passed · 1 not evaluated
              </>
            ) : (
              'Evaluating…'
            )}
          </span>
        </div>
        <ol className={styles.tree} aria-label="Explain tree output">
          {rows.map((row) => {
            const state = stateOf(row, step);
            const resolved = step >= row.resolveAt;
            const ran = row.runsFrom !== null;
            return (
              <li key={row.id} className={clsx(styles.row, row.id === 'root' && styles.root)}>
                {row.glyph && (
                  <span className={styles.glyph} aria-hidden="true">
                    {row.glyph}
                  </span>
                )}
                <span className={clsx(styles.badge, styles[state])}>{outcomeLabels[state]}</span>
                {row.operator && <span className={styles.operator}>{row.operator}</span>}
                <span className={styles.ruleCode}>{row.code}</span>
                {resolved && row.message && state === 'broken' && (
                  <span className={styles.message}>"{row.message}"</span>
                )}
                {resolved && ran && <span className={styles.duration}>({row.duration})</span>}
                {resolved && row.reason && <span className={styles.reason}>{row.reason}</span>}
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}
