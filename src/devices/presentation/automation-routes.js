import DeviceManagement from './views/device-management.vue';

export const automationRoutes = [
  { path: '', redirect: '/devices/device-management' },
  {
    path: 'device-management', // Ruta final: /devices/device-management
    name: 'device-management',
    component: DeviceManagement,
    meta: { title: 'Devices', requiresRole: 'administrator' }
  }
];

export default automationRoutes;
