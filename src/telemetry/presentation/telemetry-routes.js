export default [
    { path: '', redirect: '/telemetry/dashboard' },
    { path: 'dashboard', name: 'dashboard', component: () => import('./views/dashboard.view.vue'), meta: { title: 'Dashboard' } }, // ambos roles
    // Monitoreo y alertas se integraron en el panel del administrador; las rutas antiguas siguen funcionando
    { path: 'monitoring', name: 'monitoring', redirect: '/telemetry/dashboard', meta: { requiresRole: 'administrator' } },
    { path: 'alerts', name: 'alerts', redirect: { path: '/telemetry/dashboard', query: { focus: 'alerts' } }, meta: { requiresRole: 'administrator' } },
    // El reporte de áreas comunes del residente se integró en Mi consumo; la ruta antigua sigue funcionando
    { path: 'analytics', name: 'analytics', redirect: '/telemetry/dashboard', meta: { requiresRole: 'resident' } },
];
