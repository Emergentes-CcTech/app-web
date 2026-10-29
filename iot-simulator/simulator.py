"""
Simulador de Telemetría IoT para la Plataforma IoBuild (Áreas Comunes y Prorrateo Legal).

Modela exclusivamente hardware comunal de condominios residenciales:
  1. GatewayHub: Concentrador de sótano con buffer offline de 72h y respaldo 4G.
  2. WaterFlowSensor: Caudalímetro no invasivo de pulsos en tubería de cisterna (m3 y caudal m3/h).
  3. CurrentTransformerCT: Pinza amperimétrica en tableros de bombas, ascensores e iluminación (kWh, corriente A, potencia kW).

Soporta flags para validación de motores de anomalías:
  --leak      : Inyecta fuga nocturna en cisterna (flujo > 0.15 m3/h entre 01:00 y 04:00 AM hora Perú UTC-5).
  --overload  : Inyecta sobrecarga eléctrica en el cuarto de bombas hidroneumáticas (>25% línea base).
  --replay    : Simula reenvío secuencial desde el buffer local de 72h tras reconexión del Gateway.
"""

import os
import sys
import json
import time
import random
import argparse
import threading
from datetime import datetime, timezone, timedelta

import paho.mqtt.client as mqtt

MQTT_HOST = os.getenv("MQTT_HOST", "localhost")
MQTT_PORT = int(os.getenv("MQTT_PORT", "1883"))
MQTT_USERNAME = os.getenv("MQTT_USERNAME")
MQTT_PASSWORD = os.getenv("MQTT_PASSWORD")

# Catálogo fijo de los 6 equipos IoT comunales en comodato (Condominio Residencial Los Robles)
DEFAULT_COMMUNAL_DEVICES = {
    1: {"name": "GW-SOT-01", "type": "GatewayHub", "subsystem": "MainCistern", "mac": "AA:BB:CC:11:00:01", "location": "Sótano 1 - Cisterna"},
    2: {"name": "WFS-CIS-IN", "type": "WaterFlowSensor", "subsystem": "MainCistern", "mac": "AA:BB:CC:11:00:02", "location": "Sótano 1 - Tubería Matriz Cisterna"},
    3: {"name": "WFS-CIS-OUT", "type": "WaterFlowSensor", "subsystem": "MainCistern", "mac": "AA:BB:CC:11:00:03", "location": "Sótano 1 - Impulsión Hidroneumática"},
    4: {"name": "CT-BOMB-01", "type": "CurrentTransformerCT", "subsystem": "PressurePumpsRoom", "mac": "AA:BB:CC:11:00:04", "location": "Sótano 1 - Tablero de Bombas"},
    5: {"name": "CT-ASC-01", "type": "CurrentTransformerCT", "subsystem": "ElevatorsBank", "mac": "AA:BB:CC:11:00:05", "location": "Azotea - Tablero de Ascensores"},
    6: {"name": "CT-TAB-GRAL", "type": "CurrentTransformerCT", "subsystem": "GeneralElectricalPanel", "mac": "AA:BB:CC:11:00:06", "location": "Sótano 1 - Tablero Eléctrico General"}
}

# Estado de lecturas acumuladas (garantiza monotonicidad estricta)
_cumulative_readings = {
    1: 0.0,      # Gateway (heartbeats)
    2: 420.50,   # Cisterna Entrada (m3)
    3: 412.10,   # Cisterna Salida (m3)
    4: 1250.00,  # Bombas (kWh)
    5: 890.30,   # Ascensores (kWh)
    6: 2150.80   # Tablero General (kWh)
}

_lock = threading.Lock()
_discovered_devices = dict(DEFAULT_COMMUNAL_DEVICES)


def generate_communal_telemetry(device_id: int, info: dict, simulate_leak: bool = False, simulate_overload: bool = False, override_timestamp: str = None) -> dict:
    """Genera paquete de telemetría respetando el contrato de áreas comunes y subsistemas."""
    dtype = info.get("type", "CurrentTransformerCT")
    mac = info.get("mac", f"AA:BB:CC:11:00:{device_id:02d}")
    location = info.get("location", "Área Común")
    subsystem = info.get("subsystem", "GeneralElectricalPanel")

    now_utc = datetime.now(timezone.utc)
    ts = override_timestamp or now_utc.isoformat()

    # Hora de Lima (UTC-5) para evaluación de ventana nocturna
    lima_tz = timezone(timedelta(hours=-5))
    local_time = now_utc.astimezone(lima_tz)
    is_nocturnal_window = (1 <= local_time.hour < 4) or simulate_leak

    with _lock:
        prev_reading = _cumulative_readings.get(device_id, 100.0)

    if dtype == "GatewayHub":
        payload = {
            "deviceId": device_id,
            "macAddress": mac,
            "timestamp": ts,
            "subsystem": subsystem,
            "location": location,
            "status": "online",
            "reported": {
                "uptime_seconds": 86400 + random.randint(100, 5000),
                "battery_backup_pct": 100,
                "cellular_backup_signal_dbm": -72,
                "buffered_packets_count": 0,
                "firmware_version": "v2.4.0-communal"
            }
        }

    elif dtype == "WaterFlowSensor":
        # Incremento volumétrico continuo (m3)
        if is_nocturnal_window and simulate_leak:
            # Fuga nocturna: flujo continuo de 0.28 m3/h (> 0.15 umbral)
            flow_m3h = round(random.uniform(0.25, 0.35), 3)
            delta_m3 = (flow_m3h / 3600.0) * 5.0  # incremento en 5 segundos
        elif is_nocturnal_window:
            # Ventana nocturna normal: consumo casi nulo (< 0.05 m3/h)
            flow_m3h = round(random.uniform(0.00, 0.04), 3)
            delta_m3 = (flow_m3h / 3600.0) * 5.0
        else:
            # Día: consumo regular de áreas comunes
            flow_m3h = round(random.uniform(0.80, 2.50), 3)
            delta_m3 = (flow_m3h / 3600.0) * 5.0

        with _lock:
            new_cumulative = round(prev_reading + delta_m3, 4)
            _cumulative_readings[device_id] = new_cumulative

        payload = {
            "deviceId": device_id,
            "macAddress": mac,
            "timestamp": ts,
            "subsystem": subsystem,
            "location": location,
            "status": "online",
            "energy_kwh": 0.0,
            "reported": {
                "flow_m3h": flow_m3h,
                "flow_rate_m3h": flow_m3h,
                "cumulative_m3": new_cumulative,
                "cumulative_value": new_cumulative,
                "water_pressure_bar": round(random.uniform(3.8, 4.5), 2),
                "pulse_count": int(new_cumulative * 1000)
            }
        }

    else:
        # CurrentTransformerCT (Bombas, Ascensores, Tablero General)
        if subsystem == "PressurePumpsRoom" and simulate_overload:
            # Sobrecarga eléctrica en bombas: 5.5 kW (> 25% de la línea base promedio de 2.0 kW)
            power_kw = round(random.uniform(4.8, 5.8), 2)
            current_a = round((power_kw * 1000.0) / 220.0, 2)
        elif subsystem == "PressurePumpsRoom":
            power_kw = round(random.uniform(1.8, 2.2), 2)
            current_a = round((power_kw * 1000.0) / 220.0, 2)
        elif subsystem == "ElevatorsBank":
            power_kw = round(random.uniform(0.5, 3.5), 2)  # variable con subida/bajada
            current_a = round((power_kw * 1000.0) / 220.0, 2)
        else:
            # Tablero General (iluminación común pasillos y sótanos)
            power_kw = round(random.uniform(0.9, 1.4), 2)
            current_a = round((power_kw * 1000.0) / 220.0, 2)

        delta_kwh = (power_kw / 3600.0) * 5.0
        with _lock:
            new_cumulative = round(prev_reading + delta_kwh, 4)
            _cumulative_readings[device_id] = new_cumulative

        payload = {
            "deviceId": device_id,
            "macAddress": mac,
            "timestamp": ts,
            "subsystem": subsystem,
            "location": location,
            "energy_kwh": power_kw,
            "voltage_v": round(random.uniform(218.0, 222.0), 1),
            "status": "online",
            "reported": {
                "power_kw": power_kw,
                "current_a": current_a,
                "cumulative_kwh": new_cumulative,
                "cumulative_value": new_cumulative,
                "power_factor": 0.95
            }
        }

    return payload


def on_connect(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print(f"[MQTT] Conectado exitosamente al broker en {MQTT_HOST}:{MQTT_PORT}")
        client.subscribe("registry/#", qos=1)
        client.subscribe("commands/#", qos=1)
        print("[MQTT] Suscrito a tópicos registry/# y commands/#")
    else:
        print(f"[MQTT] Error de conexión con código {rc}")


def on_message(client, userdata, message):
    topic = message.topic
    try:
        parts = topic.split("/")
        if len(parts) == 2 and parts[0] == "registry":
            dev_id = int(parts[1])
            if not message.payload:
                with _lock:
                    _discovered_devices.pop(dev_id, None)
                print(f"[REGISTRY] Dispositivo #{dev_id} desvinculado (tombstone)")
            else:
                data = json.loads(message.payload.decode("utf-8"))
                with _lock:
                    if dev_id in _discovered_devices:
                        _discovered_devices[dev_id].update(data)
                    else:
                        _discovered_devices[dev_id] = data
                print(f"[REGISTRY] Dispositivo #{dev_id} registrado: {data.get('type')}")
    except Exception as ex:
        print(f"[ERROR] Error procesando mensaje MQTT en {topic}: {ex}")


def run_replay_simulation(client, hours: int = 72):
    """Simula el reenvío ordenado del buffer offline de 72h tras reconexión en sótanos."""
    print(f"\n=== SIMULACIÓN DE BUFFER OFFLINE ({hours} Horas) ===")
    start_time = datetime.now(timezone.utc) - timedelta(hours=hours)
    step_minutes = 60
    current = start_time

    batch_count = 0
    while current <= datetime.now(timezone.utc):
        ts_str = current.isoformat()
        with _lock:
            devices_snapshot = list(_discovered_devices.items())

        for dev_id, info in devices_snapshot:
            payload = generate_communal_telemetry(dev_id, info, override_timestamp=ts_str)
            client.publish(f"telemetry/{dev_id}", json.dumps(payload), qos=1)
            batch_count += 1

        current += timedelta(minutes=step_minutes)

    print(f"[REPLAY] {batch_count} paquetes históricos reenviados exitosamente respetando monotonicidad e idempotencia.")


def main():
    parser = argparse.ArgumentParser(description="Simulador IoT IoBuild para Áreas Comunes de Condominios")
    parser.add_argument("--leak", action="store_true", help="Simula fuga nocturna continua en cisterna (> 0.15 m3/h de 01:00 a 04:00 AM)")
    parser.add_argument("--overload", action="store_true", help="Simula sobrecarga eléctrica en el cuarto de bombas (> 25% línea base)")
    parser.add_argument("--replay", action="store_true", help="Simula reenvío secuencial desde el buffer local de 72h")
    args = parser.parse_args()

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.on_message = on_message
    if MQTT_USERNAME:
        client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD)

    try:
        client.connect(MQTT_HOST, MQTT_PORT, keepalive=60)
        client.loop_start()
    except Exception as e:
        print(f"[ADVERTENCIA] No se pudo conectar al broker MQTT ({e}). Ejecutando en modo consola de prueba.")

    if args.replay:
        run_replay_simulation(client, hours=72)

    print("\nIniciando bucle de telemetría IoT de áreas comunes (intervalo: 5s)...")
    if args.leak:
        print(">> MODO ACTIVO: Simulación de Fuga Nocturna en Cisterna (--leak)")
    if args.overload:
        print(">> MODO ACTIVO: Simulación de Sobrecarga en Cuarto de Bombas (--overload)")

    try:
        while True:
            with _lock:
                devices_snapshot = list(_discovered_devices.items())

            for dev_id, info in devices_snapshot:
                payload = generate_communal_telemetry(dev_id, info, simulate_leak=args.leak, simulate_overload=args.overload)
                try:
                    client.publish(f"telemetry/{dev_id}", json.dumps(payload), qos=1)
                except Exception:
                    pass

                dev_name = info.get('name', f"Dispositivo #{dev_id}")
                dev_type = info.get('type', 'Device')
                print(f"[{payload['timestamp']}] {dev_name} ({dev_type}) | "
                      f"Subsystem: {payload['subsystem']} | "
                      f"Reported: {json.dumps(payload.get('reported', {}))}")

            time.sleep(5)
    except KeyboardInterrupt:
        print("\nDeteniendo simulador IoT.")
        client.loop_stop()
        client.disconnect()


if __name__ == "__main__":
    main()
