export const rootRoutes = ['index'];

export const userRoutes = [
  'users/index',
  'users/overview',
  'users/installation',
  'users/configuration',
  'users/translation-modes',
  'users/support-matrix',
  'users/troubleshooting',
];

export const technicalRoutes = [
  'technical/index',
  'technical/architecture',
  'technical/pipeline',
  'technical/translator-architecture',
  'technical/native-ui-and-overlays',
  'technical/cache-and-persistence',
  'technical/diagnostics',
  'technical/development',
];

export const technicalDiagramRoutes = [
  'technical/architecture',
  'technical/pipeline',
  'technical/translator-architecture',
  'technical/native-ui-and-overlays',
  'technical/cache-and-persistence',
  'technical/diagnostics',
  'technical/development',
];

export function publishedSections() {
  return {
    root: rootRoutes,
    users: userRoutes,
    technical: technicalRoutes,
  };
}
