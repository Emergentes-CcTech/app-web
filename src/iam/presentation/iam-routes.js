// Lazy-loaded components
const login = () => import('./views/login.vue');
const registerAdministrator = () => import('./views/register-administrator.vue');
const registerResident = () => import('./views/register-resident.vue');

const iamRoutes = [
    { path: 'login', name: 'login', component: login,
      meta: { title: 'Login', public: true } },
    { path: 'register-administrator', name: 'register-administrator', component: registerAdministrator,
      meta: { title: 'Register as Administrator', public: true } },
    { path: 'register-resident', name: 'register-resident', component: registerResident,
      meta: { title: 'Register as Resident', public: true } },
];

export default iamRoutes;
