export default [
    { path: '', name: 'billing', component: () => import('./views/billing.view.vue'), meta: { title: 'Facturación y cobranzas' } },
    { path: 'bills', name: 'bills', component: () => import('./views/billing.view.vue'), props: { defaultTab: 'bills' }, meta: { title: 'Utility bills' } },
    { path: 'settlements', name: 'settlements', component: () => import('./views/billing.view.vue'), props: { defaultTab: 'settlements' }, meta: { title: 'Settlements' } },
    { path: 'settlements/:id', name: 'settlement-detail', component: () => import('./views/settlement-detail.view.vue'), meta: { title: 'Settlement' } },
];
