import { defineStore } from "pinia";
import { ref } from "vue";
import { AuditApi } from "../infrastructure/audit-api.js";
import { AuditEvent } from "../domain/model/audit-event.entity.js";

const api = new AuditApi();

export const useAuditStore = defineStore('audit', () => {
    const events = ref([]);
    const isLoading = ref(false);
    const errors = ref([]);

    async function fetchEvents(condominiumId, filters = {}) {
        isLoading.value = true; errors.value = [];
        try {
            const clean = Object.fromEntries(Object.entries(filters).filter(([, v]) => v));
            events.value = (await api.getEvents(condominiumId, clean)).data.map(e => new AuditEvent(e));
        } catch (e) { errors.value.push(e.message || 'Error'); }
        finally { isLoading.value = false; }
    }
    function clearUserSession() { events.value = []; errors.value = []; }
    return { events, isLoading, errors, fetchEvents, clearUserSession };
});
