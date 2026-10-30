export default [
    { path: 'account-statement', name: 'account-statement', component: () => import('./views/account-statement.view.vue'),
      meta: { title: 'Account statement', requiresRole: 'resident' } },
    { path: 'delinquency', name: 'delinquency', component: () => import('../../billing/presentation/views/billing.view.vue'),
      props: { defaultTab: 'delinquency' },
      meta: { title: 'Delinquency', requiresRole: 'administrator' } },
];
