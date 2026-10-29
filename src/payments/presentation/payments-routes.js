export default [
    { path: 'account-statement', name: 'account-statement', component: () => import('./views/account-statement.view.vue'),
      meta: { title: 'Account statement', requiresRole: 'resident' } },
    { path: 'delinquency', name: 'delinquency', component: () => import('./views/delinquency.view.vue'),
      meta: { title: 'Delinquency', requiresRole: 'administrator' } },
];
