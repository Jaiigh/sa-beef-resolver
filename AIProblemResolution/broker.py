import hashlib
import os
import json
import pika


PREFIX = os.getenv("AI_QUEUE_PREFIX", "ai.resolution")
SHARDS = int(os.getenv("AI_SHARDS", "2"))
if not 1 <= SHARDS <= 32:
    raise ValueError("AI_SHARDS must be 1-32")
EXCHANGE = PREFIX + ".requests"
FAILED_EXCHANGE = PREFIX + ".failed"
FAILED_QUEUE = PREFIX + ".failed"


def shard_for(session_id):
    return int.from_bytes(hashlib.sha256(str(session_id).encode()).digest()[:8], "big") % SHARDS


def queue_for(shard):
    return PREFIX + ".work." + str(shard)


def connect():
    parameters = pika.URLParameters(os.getenv("RABBITMQ_URL", "amqp://ai:ai-local-only@localhost:5672/%2F"))
    parameters.heartbeat = 60
    parameters.blocked_connection_timeout = 30
    parameters.socket_timeout = 10
    return pika.BlockingConnection(parameters)


def declare(channel):
    channel.exchange_declare(exchange=EXCHANGE, exchange_type="direct", durable=True)
    channel.exchange_declare(exchange=FAILED_EXCHANGE, exchange_type="direct", durable=True)
    channel.queue_declare(queue=FAILED_QUEUE, durable=True)
    channel.queue_bind(queue=FAILED_QUEUE, exchange=FAILED_EXCHANGE, routing_key="failed")
    for shard in range(SHARDS):
        queue = queue_for(shard)
        channel.queue_declare(queue=queue, durable=True, arguments={
            "x-single-active-consumer": True,
            "x-dead-letter-exchange": FAILED_EXCHANGE,
            "x-dead-letter-routing-key": "failed",
        })
        channel.queue_bind(queue=queue, exchange=EXCHANGE, routing_key=str(shard))


def publish_request(payload, message_id):
    """Shared transport for failed-job replay and development tooling.

    New user submissions are published by ASP.NET Core, not this worker module.
    """
    from core import validate, validate_job_id
    payload = validate(payload)
    validate_job_id(message_id)
    connection = connect()
    try:
        channel = connection.channel()
        declare(channel)
        channel.confirm_delivery()
        channel.basic_publish(
            exchange=EXCHANGE, routing_key=str(shard_for(payload["sessionId"])),
            body=json.dumps(payload, ensure_ascii=False).encode("utf-8"), mandatory=True,
            properties=pika.BasicProperties(
                content_type="application/json", content_encoding="utf-8", delivery_mode=2,
                message_id=message_id, correlation_id=payload["sessionId"],
            ),
        )
    finally:
        connection.close()
