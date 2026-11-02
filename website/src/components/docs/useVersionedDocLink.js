import {useActiveVersion, useLatestVersion} from '@docusaurus/plugin-content-docs/client';

const docsPrefix = '/docs/';

/**
 * Resolves a `/docs/<page>` path against the docs version the reader is currently viewing, so a component rendered
 * inside a versioned page links within that version (for example `/docs/next/<page>` on the unreleased line) instead
 * of always jumping to the latest release. Outside the docs the latest release is used. Paths that do not start with
 * `/docs/` are returned unchanged.
 * @returns {(to: string) => string}
 */
export default function useVersionedDocLink() {
  const activeVersion = useActiveVersion(undefined);
  const latestVersion = useLatestVersion(undefined);
  const versionPath = (activeVersion ?? latestVersion).path;
  return (to) => (to.startsWith(docsPrefix) ? `${versionPath}/${to.slice(docsPrefix.length)}` : to);
}
