import ProfileRouterView from './views/profile-router.vue';

const profileRoutes = [
    {
        path: 'profile',
        name: 'profile',
        component: ProfileRouterView,
        meta: {
            title: 'My Profile',
            requiresAuth: true
        }
    }
];

export default profileRoutes;