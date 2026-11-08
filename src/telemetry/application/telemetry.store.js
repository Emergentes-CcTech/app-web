import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { TelemetryApi } from "../infrastructure/telemetry-api.js";
import { Reading } from "../domain/model/reading.entity.js";
import { Alert } from "../domain/model/alert.entity.js";
import { LIVE_POLLING_MS, ALERT_POLLING_MS } from "../../shared/infrastructure/constants.js";
import { litersPerMinToM3, ampsToKwh } from "./projection.js";

const api = new TelemetryApi();
let _readingsTimer = null;
let _alertsTimer = null;

const toReadings = (list) => (Array.isArray(list) ? list : []).map(r => new Reading(r));
const toAlerts = (list) => (Array.isArray(list) ? list : []).map(a => new Alert(a));

export const useTelemetryStore = defineStore('telemetry', () => {
    const liveReadings = ref([]);
    const readingsByDevice = ref({});
    const history = ref([]);
    const monthReadings = ref([]);
    const dailyConsumption = ref([]);
    const alerts = ref([]);
    const lastCriticalNotice = ref(null);
    const isLoading = ref(false);
    const errors = ref([]);
    let _alertsSeeded = false;

    const openAlerts = computed(() => alerts.value.filter(a => a.isOpen));
    const openAlertsCount = computed(() => openAlerts.value.length);

    /** Última lectura por dispositivo */
    const latestByDevice = computed(() => {
        const map = {};
        [...liveReadings.value].sort((a, b) => a.timestamp.localeCompare(b.timestamp)).forEach(r => { map[r.deviceId] = r; });
        return map;
    });

    async function fetchLive(condominiumId) {
        try {
            let list = toReadings((await api.getLiveReadings(condominiumId)).data);
            if (!list.length) list = toReadings((await api.getLatestReadings(condominiumId)).data); // sin tráfico reciente: última lectura conocida
            liveReadings.value = list;
            const grouped = {};
            list.forEach(r => { (grouped[r.deviceId] ||= []).push(r); });
            readingsByDevice.value = grouped;
        } catch (e) { errors.value.push(e.message || 'Error'); }
    }

    /** RF-009: histórico diario (promedio por día, 30 días) o mensual (promedio por mes, 12 meses) */
    async function fetchHistory(condominiumId, deviceId, granularity = 'day') {
        const to = new Date();
        const from = new Date(to);
        if (granularity === 'month') from.setMonth(from.getMonth() - 12); else from.setDate(from.getDate() - 30);
        const list = toReadings((await api.getReadings(condominiumId, deviceId, from.toISOString(), to.toISOString())).data);
        const buckets = {};
        list.forEach(r => {
            const key = r.timestamp.slice(0, granularity === 'month' ? 7 : 10);
            (buckets[key] ||= []).push(r.value);
        });
        history.value = Object.keys(buckets).sort().map(k => ({
            label: k, value: Math.round((buckets[k].reduce((a, b) => a + b, 0) / buckets[k].length) * 100) / 100,
        }));
        return history.value;
    }

    /** Lecturas del mes en curso para el panel (RF-010) */
    async function fetchMonth(condominiumId) {
        const now = new Date();
        const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
        monthReadings.value = toReadings((await api.getReadings(condominiumId, null, from, now.toISOString())).data);
        return monthReadings.value;
    }

    /** Consumo por día (m³ de agua de ingreso y kWh de áreas comunes) desde el inicio del mes anterior */
    async function fetchDailyConsumption(condominiumId) {
        const now = new Date();
        const from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
        dailyConsumption.value = (await api.getDailyConsumption(condominiumId, from.toISOString(), now.toISOString())).data || [];
        return dailyConsumption.value;
    }

    /** Consumo por medidor en un rango de fechas, sin guardarlo en el estado (mes actual o tramo del mes anterior) */
    async function fetchDeviceConsumption(condominiumId, from, to) {
        return (await api.getDeviceConsumption(condominiumId, from.toISOString(), to.toISOString())).data || [];
    }

    /** Lecturas de un rango de fechas, sin guardarlas en el estado (p. ej. el mes anterior para comparar) */
    async function fetchPeriod(condominiumId, from, to) {
        return toReadings((await api.getReadings(condominiumId, null, from.toISOString(), to.toISOString())).data);
    }

    function consumptionOf(readings, unit) {
        const sum = readings.filter(r => r.unit === unit).reduce((a, r) => a + (unit === 'L/min' ? litersPerMinToM3(r.value) : ampsToKwh(r.value)), 0);
        return Math.round(sum * 100) / 100;
    }

    function ingestAlerts(list) {
        const incoming = toAlerts(list);
        if (_alertsSeeded) {
            const known = new Set(alerts.value.map(a => a.id));
            const fresh = incoming.find(a => a.isOpen && a.isCritical && !known.has(a.id));
            if (fresh) lastCriticalNotice.value = fresh;
        }
        _alertsSeeded = true;
        alerts.value = incoming;
    }

    async function fetchAlerts(condominiumId, status = null) {
        try { ingestAlerts((await api.getAlerts(condominiumId, status)).data); }
        catch (e) { errors.value.push(e.message || 'Error'); }
    }
    /** Alertas abiertas de todos los condominios del administrador (el backend filtra por JWT) */
    async function fetchOpenAlerts() {
        try { ingestAlerts((await api.getOpenAlerts()).data); }
        catch (e) { errors.value.push(e.message || 'Error'); }
    }
    async function acknowledge(alertId) {
        await api.acknowledgeAlert(alertId);
        const a = alerts.value.find(x => x.id === alertId);
        if (a) a.status = 'acknowledged';
    }

    // RNF-012: lectura < 5 s, alerta crítica < 60 s
    function startPolling(condominiumId) {
        stopPolling();
        _readingsTimer = setInterval(() => fetchLive(condominiumId), LIVE_POLLING_MS);
        startAlertPolling(() => fetchAlerts(condominiumId));
    }
    function startAlertPolling(fn = fetchOpenAlerts) {
        if (_alertsTimer) clearInterval(_alertsTimer);
        _alertsTimer = setInterval(fn, ALERT_POLLING_MS);
    }
    function stopPolling() {
        if (_readingsTimer) clearInterval(_readingsTimer);
        if (_alertsTimer) clearInterval(_alertsTimer);
        _readingsTimer = _alertsTimer = null;
    }

    function clearUserSession() {
        stopPolling();
        liveReadings.value = []; readingsByDevice.value = {}; history.value = []; monthReadings.value = []; dailyConsumption.value = [];
        alerts.value = []; errors.value = []; _alertsSeeded = false;
    }

    return { liveReadings, readingsByDevice, history, monthReadings, dailyConsumption, fetchDailyConsumption, fetchDeviceConsumption, alerts, openAlerts, openAlertsCount, latestByDevice,
        lastCriticalNotice, isLoading, errors, fetchLive, fetchHistory, fetchMonth, fetchPeriod, consumptionOf, fetchAlerts, fetchOpenAlerts,
        acknowledge, startPolling, startAlertPolling, stopPolling, clearUserSession };
});
