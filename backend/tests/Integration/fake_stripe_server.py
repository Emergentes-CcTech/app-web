#!/usr/bin/env python3
import json
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

proof = {"checkout": 0, "session": 0, "invoices": 0}

def send(handler, status, payload):
    raw = json.dumps(payload).encode()
    handler.send_response(status)
    handler.send_header("Content-Type", "application/json")
    handler.send_header("Content-Length", str(len(raw)))
    handler.end_headers()
    handler.wfile.write(raw)

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass
    def auth(self):
        return self.headers.get("Authorization") == "Bearer rk_test_minimum" and self.headers.get("Stripe-Version") == "2026-05-27.dahlia"
    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/health": return send(self, 200, {"status": "ok"})
        if parsed.path == "/proof": return send(self, 200, proof)
        if not self.auth(): return send(self, 401, {"error": "invalid_api_key"})
        if parsed.path == "/v1/checkout/sessions/cs_runtime":
            proof["session"] += 1
            return send(self, 200, {"id": "cs_runtime", "status": "complete", "payment_status": "paid", "metadata": {"builder_id": "1", "plan_id": "3"}})
        if parsed.path == "/v1/invoices" and parse_qs(parsed.query) == {"customer": ["cus_runtime"], "limit": ["100"]}:
            proof["invoices"] += 1
            return send(self, 200, {"object": "list", "data": [{"id": "in_runtime", "status": "paid", "amount_paid": 1200}]})
        return send(self, 404, {"error": "not_found"})
    def do_POST(self):
        if self.path != "/v1/checkout/sessions" or not self.auth(): return send(self, 401, {"error": "invalid_api_key"})
        if self.headers.get("Content-Type") != "application/x-www-form-urlencoded": return send(self, 415, {"error": "invalid_content_type"})
        length = int(self.headers.get("Content-Length", "0"))
        form = parse_qs(self.rfile.read(length).decode())
        required = {"mode": ["subscription"], "success_url": ["https://success.example"], "cancel_url": ["https://cancel.example"], "line_items[0][price]": ["price_runtime"], "line_items[0][quantity]": ["1"], "metadata[builder_id]": ["1"], "metadata[plan_id]": ["3"]}
        if form != required: return send(self, 400, {"error": "invalid_checkout_contract", "received": form})
        proof["checkout"] += 1
        return send(self, 200, {"id": "cs_runtime", "url": "https://checkout.stripe.test/cs_runtime", "amount_total": 1200, "payment_status": "unpaid", "status": "open", "metadata": {"builder_id": "1", "plan_id": "3"}})

ThreadingHTTPServer(("127.0.0.1", int(sys.argv[1])), Handler).serve_forever()
