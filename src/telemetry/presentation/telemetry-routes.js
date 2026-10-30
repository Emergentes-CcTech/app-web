export default [
    { path: '', redirect: '/telemetry/dashboard' },
    { path: 'dashboard', name: 'dashboard', component: () => import('./views/dashboard.view.vue'), meta: { title: 'Dashboard' } }, // ambos roles
    { path: 'monitoring', name: 'monitoring', component: () => import('./views/monitoring.view.vue'),
      props: { defaultTab: 'monitoring' },
      meta: { title: 'Monitoring', requiresRole: 'administrator' } },
    { path: 'analytics', name: 'analytics', component: () => import('./views/common-areas-report.view.vue'),
      meta: { title: 'Analytics', requiresRole: 'resident' } }, // reporte de áreas comunes del dueño de departamento
    { path: 'alerts', name: 'alerts', component: () => import('./views/monitoring.view.vue'),
      props: { defaultTab: 'alerts' },
      meta: { title: 'Alerts', requiresRole: 'administrator' } },
];
