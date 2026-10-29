export default [
    { path: '', name: 'condominiums', component: () => import('./views/condominium-list.vue'),
      meta: { title: 'Condominiums', requiresRole: 'administrator' } },
    { path: 'new', name: 'condominium-new', component: () => import('./views/condominium-form.vue'),
      meta: { title: 'New condominium', requiresRole: 'administrator' } },
    { path: ':id', name: 'condominium-details', component: () => import('./views/condominium-details.vue'),
      meta: { title: 'Condominium', requiresRole: 'administrator' } },
    { path: ':id/edit', name: 'condominium-edit', component: () => import('./views/condominium-form.vue'),
      meta: { title: 'Edit condominium', requiresRole: 'administrator' } },
];
