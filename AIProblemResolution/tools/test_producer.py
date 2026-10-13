"""Test-only CLI. Runtime submissions come from ASP.NET Core.

From AIProblemResolution: python -m tools.test_producer --session 101 --people 1 --message '...'
"""
import argparse
import json
import uuid
import broker
from core import validate, validate_job_id


def publish(payload, message_id=None):
    payload = validate(payload)
    message_id = message_id or uuid.uuid4().hex
    validate_job_id(message_id)
    broker.publish_request(payload, message_id)
    return message_id


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--session", required=True)
    parser.add_argument("--people", type=int, choices=[1, 2], required=True)
    parser.add_argument("--message", required=True)
    parser.add_argument("--message-id", help="Reuse the SAME ID when retrying the SAME submission")
    args = parser.parse_args()
    job = args.message_id or uuid.uuid4().hex
    # Print ID before publishing so it remains available if confirmation is uncertain.
    print(json.dumps({"messageId": job, "sessionId": args.session}), flush=True)
    publish(dict(sessionId=args.session, people=args.people, message=args.message), job)
    print("Broker confirmed acceptance (this does not mean AI completed).")
