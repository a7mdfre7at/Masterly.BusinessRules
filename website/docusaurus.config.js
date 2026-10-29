// @ts-check
// Docusaurus configuration for the Masterly.BusinessRules documentation site.
// See: https://docusaurus.io/docs/api/docusaurus-config

import {themes as prismThemes} from 'prism-react-renderer';

const repositoryUrl = 'https://github.com/a7mdfre7at/Masterly.BusinessRules';
const logoUrl = 'https://raw.githubusercontent.com/a7mdfre7at/Masterly.BusinessRules/master/repo_image.png';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Masterly.BusinessRules',
  tagline: 'A composable business rule engine for .NET',
  favicon: logoUrl,

  future: {
    v4: true,
  },

  url: 'https://a7mdfre7at.github.io',
  baseUrl: '/Masterly.BusinessRules/',
  organizationName: 'a7mdfre7at',
  projectName: 'Masterly.BusinessRules',
  trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',

  markdown: {
    // .md files are plain CommonMark, so C# generics such as IBusinessRule<T> need no escaping.
    format: 'detect',
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl: `${repositoryUrl}/tree/master/website/`,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: logoUrl,
      colorMode: {
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'Masterly.BusinessRules',
        logo: {
          alt: 'Masterly.BusinessRules logo',
          src: logoUrl,
        },
        items: [
          {type: 'docSidebar', sidebarId: 'docs', position: 'left', label: 'Docs'},
          {to: '/docs/api-reference', label: 'API Reference', position: 'left'},
          {href: `${repositoryUrl}/blob/master/CHANGELOG.md`, label: 'Changelog', position: 'right'},
          {href: 'https://www.nuget.org/packages/Masterly.BusinessRules', label: 'NuGet', position: 'right'},
          {href: repositoryUrl, label: 'GitHub', position: 'right'},
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {label: 'Quick Start', to: '/docs/quick-start'},
              {label: 'API Reference', to: '/docs/api-reference'},
              {label: 'Upgrading from 2.x', to: '/docs/upgrading-from-2x'},
            ],
          },
          {
            title: 'Project',
            items: [
              {label: 'GitHub', href: repositoryUrl},
              {label: 'Issues', href: `${repositoryUrl}/issues`},
              {label: 'Changelog', href: `${repositoryUrl}/blob/master/CHANGELOG.md`},
            ],
          },
          {
            title: 'Packages',
            items: [
              {label: 'NuGet', href: 'https://www.nuget.org/packages?q=Masterly.BusinessRules'},
              {label: 'License (MIT)', href: `${repositoryUrl}/blob/master/LICENSE.txt`},
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} Ahmad Omar Al-Freihat. Built with Docusaurus.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'bash', 'json', 'powershell'],
      },
    }),
};

export default config;
