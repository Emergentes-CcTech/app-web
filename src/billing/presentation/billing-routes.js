export default [
    { path: '', redirect: '/billing/bills' },
    { path: 'bills', name: 'bills', component: () => import('./views/bills.view.vue'), meta: { title: 'Utility bills' } },
    { path: 'settlements', name: 'settlements', component: () => import('./views/settlements.view.vue'), meta: { title: 'Settlements' } },
    { path: 'settlements/:id', name: 'settlement-detail', component: () => import('./views/settlement-detail.view.vue'), meta: { title: 'Settlement' } },
];
