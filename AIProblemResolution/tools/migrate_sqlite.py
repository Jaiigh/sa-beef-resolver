"""One-time idempotent import. Original SQLite is opened read-only and preserved."""
import argparse
import json
import sqlite3
import shutil
import tempfile
from pathlib import Path
from datetime import datetime, timezone
from core import Store


def migrate(path, store):
    # Copy SQLite + WAL sidecars to writable temporary storage. A read-only mount
    # may prevent SQLite opening WAL mode even though the source itself is readable.
    temp = tempfile.TemporaryDirectory()
    original = Path(path)
    for suffix in ("", "-wal", "-shm"):
        candidate = Path(str(original) + suffix)
        if candidate.exists():
            shutil.copyfile(candidate, Path(temp.name) / candidate.name)
    source = sqlite3.connect(str(Path(temp.name) / original.name))
    now = datetime.now(timezone.utc)
    counts = {"sessions": 0, "jobs": 0, "resolutions": 0}
    try:
        sessions = list(source.execute("SELECT session_id,version,history,result FROM sessions"))
        histories = {row[0]: json.loads(row[2]) for row in sessions}
        with store.client.start_session() as transaction:
            with transaction.start_transaction():
                for session_id, version, history, result in sessions:
                    existing = store.db.sessions.find_one({"_id": session_id}, session=transaction)
                    if existing and existing["version"] >= version:
                        continue
                    store.db.sessions.replace_one({"_id": session_id}, {"_id": session_id, "version": version,
                        "history": json.loads(history), "result": json.loads(result), "updatedAt": now},
                        upsert=True, session=transaction)
                    counts["sessions"] += 1
                for job_id, payload, result_json in source.execute("SELECT job_id,payload,result FROM jobs"):
                    result = json.loads(result_json)
                    if store.db.jobs.find_one({"_id": job_id}, session=transaction):
                        continue
                    store.db.jobs.insert_one({"_id": job_id, "sessionId": result["sessionId"], "payload": payload,
                        "result": result, "createdAt": now}, session=transaction)
                    counts["jobs"] += 1
                    if result["status"] == "resolved":
                        store.db.resolutions.insert_one({"_id": job_id, "messageId": job_id, "sessionId": result["sessionId"],
                            "version": result["version"], "resolution": result["resolution"],
                            "arguments": histories[result["sessionId"]][:result["messageCount"]],
                            "model": "legacy-unknown", "createdAt": now, "source": "sqlite-import"}, session=transaction)
                        counts["resolutions"] += 1
    finally:
        source.close()
        temp.cleanup()
    return counts


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("path")
    args = parser.parse_args()
    store = Store()
    try:
        print(json.dumps(migrate(args.path, store)))
    finally:
        store.close()
