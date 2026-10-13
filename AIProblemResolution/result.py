import argparse
import json
import time
from core import Store


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Read latest persisted session result")
    parser.add_argument("--session", required=True)
    parser.add_argument("--wait", type=float, default=0, help="seconds to wait for target version")
    parser.add_argument("--min-version", type=int, default=1)
    parser.add_argument("--history", action="store_true", help="show every successful AI resolution for this session")
    args = parser.parse_args()
    store = Store()
    if args.history:
        print(json.dumps(store.get_resolutions(args.session), ensure_ascii=False, indent=2))
        store.close()
        raise SystemExit(0)
    deadline = time.monotonic() + args.wait
    while True:
        result = store.get_session(args.session)
        if result and result["version"] >= args.min_version:
            print(json.dumps(result, ensure_ascii=False, indent=2))
            break
        if time.monotonic() >= deadline:
            print("No result at the requested version yet (job may be queued or failed).")
            raise SystemExit(1)
        time.sleep(0.5)
