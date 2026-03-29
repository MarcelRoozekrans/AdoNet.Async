// @ts-check
import {themes as prismThemes} from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'AdoNet.Async',
  tagline: 'Async-first interfaces and base classes for ADO.NET',
  favicon: 'img/favicon.ico',
  url: 'https://marcelroozekrans.github.io',
  baseUrl: '/AdoNet.Async/',
  organizationName: 'MarcelRoozekrans',
  projectName: 'AdoNet.Async',
  trailingSlash: false,
  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

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
          editUrl: 'https://github.com/MarcelRoozekrans/AdoNet.Async/tree/main/website/',
          routeBasePath: '/',
        },
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      navbar: {
        title: 'AdoNet.Async',
        logo: {
          alt: 'AdoNet.Async Logo',
          src: 'img/logo.svg',
          href: '/docs/intro',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'docs',
            position: 'left',
            label: 'Docs',
          },
          {
            href: 'https://github.com/MarcelRoozekrans/AdoNet.Async',
            label: 'GitHub',
            position: 'right',
          },
          {
            href: 'https://www.nuget.org/packages/AdoNet.Async',
            label: 'NuGet',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              { label: 'Introduction', to: '/docs/intro' },
              { label: 'Getting Started', to: '/docs/getting-started/installation' },
              { label: 'Typed DataSets', to: '/docs/typed-datasets/overview' },
            ],
          },
          {
            title: 'Community',
            items: [
              { label: 'GitHub', href: 'https://github.com/MarcelRoozekrans/AdoNet.Async' },
              { label: 'Issues', href: 'https://github.com/MarcelRoozekrans/AdoNet.Async/issues' },
            ],
          },
          {
            title: 'More',
            items: [
              { label: 'NuGet', href: 'https://www.nuget.org/packages/AdoNet.Async' },
              { label: 'License', href: 'https://github.com/MarcelRoozekrans/AdoNet.Async/blob/main/LICENSE' },
            ],
          },
        ],
        copyright: `Copyright ${new Date().getFullYear()} Marcel Roozekrans. Built with Docusaurus.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'markup', 'bash', 'json'],
      },
    }),
};

export default config;
