import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

import {
  docsDescription,
  docsTitle,
  locales,
  siteBase,
  siteUrl,
  technicalSidebar,
  userSidebar,
} from './src/site-config.mjs';

export default defineConfig({
  site: siteUrl,
  base: siteBase,
  markdown: {
    syntaxHighlight: {
      type: 'shiki',
      excludeLangs: ['math', 'mermaid'],
    },
  },
  vite: {
    build: {
      chunkSizeWarningLimit: 700,
    },
  },
  integrations: [
    starlight({
      title: docsTitle,
      description: docsDescription,
      defaultLocale: 'root',
      locales,
      social: [
        {
          icon: 'github',
          label: 'GitHub',
          href: 'https://github.com/lokinmodar/Echoglossian',
        },
      ],
      sidebar: [
        {
          label: 'Plugin User Docs',
          items: userSidebar,
        },
        {
          label: 'Technical Docs',
          items: technicalSidebar,
        },
      ],
      customCss: ['./src/styles/custom.css'],
    }),
  ],
});
