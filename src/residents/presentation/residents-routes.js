export const residentsRoutes = [
    { path: '', name: 'residents', component: () => import('./views/resident-list.vue'),
      meta: { title: 'Residents', requiresRole: 'administrator' } },
    { path: ':id', name: 'resident-profile', component: () => import('./views/resident-profile.vue'),
      meta: { title: 'Resident', requiresRole: 'administrator' } },
];
export default residentsRoutes;
