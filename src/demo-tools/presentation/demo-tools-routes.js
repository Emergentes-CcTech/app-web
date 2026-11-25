export const demoToolsRoutes = [
    { path: '', name: 'demo-tools', component: () => import('./views/demo-tools.view.vue'),
      meta: { title: 'Demo tools', requiresRole: 'administrator' } },
];
export default demoToolsRoutes;
