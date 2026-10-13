"""Opt-in real RabbitMQ test with a fake Ollama HTTP endpoint, not a real LLM.
Run from AIProblemResolution: python tests/integration.py
Uses unique queue names, a temporary DB, and cleans up its workers/topology.
"""
import json
import os
from pathlib import Path
import subprocess
import socket
import sys
import tempfile
import threading
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
os.environ["AI_QUEUE_PREFIX"] = "ai.integration." + uuid.uuid4().hex
os.environ["AI_SHARDS"] = "2"
os.environ["AI_ATTEMPTS"] = "1"
import broker
from tools.test_producer import publish
from core import Store, RESULT_FIELDS
import pika


def wait_for(predicate, seconds=20):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = predicate()
        if value:
            return value
        time.sleep(0.1)
    raise AssertionError("Timed out waiting for integration result")


def main():
    contexts = []
    active = 0
    peak = 0
    lock = threading.Lock()
    class Handler(BaseHTTPRequestHandler):
        def do_POST(self):
            nonlocal active, peak
            request = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
            context = json.loads(request["messages"][1]["content"])
            with lock:
                contexts.append(context)
                active += 1
                peak = max(peak, active)
            time.sleep(0.5)
            bad = any(x["message"] == "FAIL_MODEL" for x in context["arguments"])
            self.send_response(200)
            self.end_headers()
            result = "not json" if bad else json.dumps({k: "resolution" for k in RESULT_FIELDS})
            self.wfile.write(json.dumps({"message": {"content": result}}).encode())
            with lock:
                active -= 1

        def log_message(self, *args):
            pass

    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    connection = None
    workers = []
    try:
        with tempfile.TemporaryDirectory() as temp:
            os.environ["MONGO_DATABASE"] = "ai_integration_" + uuid.uuid4().hex
            os.environ["OLLAMA_URL"] = "http://127.0.0.1:" + str(server.server_port)
            store = Store()
            connection = broker.connect()
            channel = connection.channel()
            broker.declare(channel)
            for shard in range(2):
                workers.append(subprocess.Popen(
                    [sys.executable, "worker.py", "--shard", str(shard)], cwd=str(ROOT),
                    env=os.environ.copy(), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
            sessions = [next(str(i) for i in range(1000) if broker.shard_for(str(i)) == shard)
                        for shard in range(2)]
            for session in sessions:
                publish(dict(sessionId=session, people=1, message="PRIVATE_" + session), "first_" + session)
            for session in sessions:
                publish(dict(sessionId=session, people=2, message="REPLY_" + session), "second_" + session)
            for session in sessions:
                wait_for(lambda: (store.get_session(session) or {}).get("status") == "resolved")
            assert peak == 2, "different shards should analyze concurrently"
            for context in contexts:
                assert all(context["sessionId"] in x["message"] for x in context["arguments"])
            before = len(contexts)
            session = sessions[0]
            publish(dict(sessionId=session, people=2, message="REPLY_" + session), "second_" + session)
            wait_for(lambda: all(channel.queue_declare(queue=broker.queue_for(s), passive=True).method.message_count == 0
                                for s in range(2)))
            time.sleep(0.7)
            assert len(contexts) == before
            assert store.get_session(session)["messageCount"] == 2
            publish(dict(sessionId=session, people=2, message="FAIL_MODEL"), "fail_job")
            wait_for(lambda: channel.queue_declare(queue=broker.FAILED_QUEUE, passive=True).method.message_count == 1)
            assert store.get_session(session)["version"] == 2
            channel.basic_publish(exchange=broker.EXCHANGE, routing_key="0", body=b'{"invalid":true}',
                                  properties=pika.BasicProperties(delivery_mode=2, message_id="invalid_job"))
            wait_for(lambda: channel.queue_declare(queue=broker.FAILED_QUEUE, passive=True).method.message_count == 2)
            # Commit-before-ACK idempotency remains valid after worker restart.
            workers[0].terminate()
            workers[0].wait(timeout=10)
            workers[0] = subprocess.Popen([sys.executable, "worker.py", "--shard", "0"],
                                         cwd=str(ROOT), env=os.environ.copy(),
                                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            session = sessions[0]
            publish(dict(sessionId=session, people=2, message="REPLY_" + session), "second_" + session)
            time.sleep(1)
            assert store.get_session(session)["messageCount"] == 2
            assert len(contexts) == before + 1  # the single failed model call, no replay call
            print("PASS: real broker delivery, parallel shards, isolated contexts, duplicate/restart, model failure and invalid-message DLQ")
            # Optional bridge verification: build .NET first and provide its DLL path.
            bridge_dll = os.getenv("AI_BRIDGE_DLL")
            if bridge_dll:
                with socket.socket() as listener:
                    listener.bind(("127.0.0.1", 0))
                    port = listener.getsockname()[1]
                env = os.environ.copy()
                env["GRPC_PORT"] = str(port)
                env["GRPC_HOST"] = "127.0.0.1"
                env["DEMO_DISPUTE_ID"] = "bridge-" + uuid.uuid4().hex
                backend = subprocess.Popen(["dotnet", bridge_dll], env=env,
                                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                workers.append(backend)
                def ready():
                    if backend.poll() is not None:
                        raise AssertionError("ASP.NET Core exited during startup")
                    try:
                        with socket.create_connection(("127.0.0.1", port), timeout=0.2):
                            return True
                    except OSError:
                        return False
                wait_for(ready)
                model_calls_before = len(contexts)
                demo = subprocess.run(["node", "client/src/ai-fixture.js"],
                                      cwd=str(ROOT.parent / "EvidenceAndArgumentSubmission"),
                                      env=env, capture_output=True, text=True, timeout=30)
                assert demo.returncode == 0, demo.stderr
                session = env["DEMO_DISPUTE_ID"]
                wait_for(lambda: (store.get_session(session) or {}).get("status") == "resolved")
                result = store.get_session(session)
                assert result["version"] == 2 and result["messageCount"] == 2
                assert len(contexts) == model_calls_before + 1
                context = contexts[-1]
                assert context["sessionId"] == session
                messages = [json.loads(x["message"]) for x in context["arguments"]]
                assert messages[0]["evidence"][0]["url"] == "https://example.com/receipt"
                assert [x["people"] for x in context["arguments"]] == [1, 2]
                print("PASS: Node gRPC -> ASP.NET Core -> RabbitMQ -> Python -> fake Ollama -> MongoDB; duplicate and RPC validation")
    finally:
        for worker in workers:
            if worker.poll() is None:
                worker.terminate()
            worker.wait(timeout=10)
        if connection and connection.is_open:
            channel = connection.channel()
            for shard in range(2):
                channel.queue_delete(queue=broker.queue_for(shard))
            channel.queue_delete(queue=broker.FAILED_QUEUE)
            channel.exchange_delete(exchange=broker.EXCHANGE)
            channel.exchange_delete(exchange=broker.FAILED_EXCHANGE)
            connection.close()
        if "store" in locals():
            store.client.drop_database(os.environ["MONGO_DATABASE"])
            store.close()
        server.shutdown()
        server.server_close()
        thread.join()


if __name__ == "__main__":
    main()
