import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { DeviceApi } from "../infrastructure/device-api.js";
import { DeviceAssembler } from "../infrastructure/device.assembler.js";

const api = new DeviceApi();

export const useDeviceStore = defineStore('device', () => {
    const devices = ref([]);
    const isLoading = ref(false);
    const errors = ref([]);

    const total = computed(() => devices.value.length);
    const online = computed(() => devices.value.filter(d => d.isOnline).length);
    const offline = computed(() => total.value - online.value);

    async function fetchByCondominium(condominiumId) {
        isLoading.value = true;
        errors.value = [];
        try { devices.value = await api.getByCondominium(condominiumId); }
        catch (e) { errors.value.push(e.message || 'Error'); }
        finally { isLoading.value = false; }
    }

    // RF-007: el identificador es único dentro del condominio
    function isDeviceIdTaken(condominiumId, deviceId, ignoreId = null) {
        return devices.value.some(d => d.condominiumId === condominiumId
            && String(d.deviceId).trim().toLowerCase() === String(deviceId).trim().toLowerCase()
            && d.id !== ignoreId);
    }

    async function save(entity) {
        const resource = DeviceAssembler.toResource(entity);
        try {
            if (entity.id) {
                const { data } = await api.update(entity.id, resource);
                const i = devices.value.findIndex(d => d.id === entity.id);
                if (i >= 0) devices.value[i] = DeviceAssembler.toEntity(data);
            } else {
                delete resource.id;
                const { data } = await api.create({ ...resource, status: resource.status || 'offline' });
                devices.value.push(DeviceAssembler.toEntity(data));
            }
        } catch (e) {
            if (e.response?.status === 409) throw new Error('idTaken');
            throw e;
        }
    }

    async function unlink(id) {
        await api.unlink(id);
        devices.value = devices.value.filter(d => d.id !== id);
    }

    function clearUserSession() { devices.value = []; errors.value = []; }

    return { devices, isLoading, errors, total, online, offline, fetchByCondominium, isDeviceIdTaken, save, unlink, clearUserSession };
});
