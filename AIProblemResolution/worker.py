import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import logging
import os
from pymongo.errors import PyMongoError
import time
import pika
import broker
from core import InvalidMessage, OllamaModel, RetryableError, Store, validate


LOG = logging.getLogger("ai.worker")


def run_job(store, model, payload, message_id):
    attempts = int(os.getenv("AI_ATTEMPTS", "3"))
    if not 1 <= attempts <= 10:
        raise InvalidMessage("AI_ATTEMPTS must be 1-10")
    for attempt in range(attempts):
        try:
            return store.process(payload, message_id, model)
        except (RetryableError, PyMongoError):
            if attempt + 1 == attempts:
                raise
            time.sleep(min(2 ** attempt, 8))


def consume(shard, store, model):
    connection = broker.connect()
    executor = ThreadPoolExecutor(max_workers=1)
    try:
        channel = connection.channel()
        broker.declare(channel)
        channel.basic_qos(prefetch_count=1)

        def received(ch, method, properties, body):
            try:
                if len(body) > 40000:
                    raise InvalidMessage("payload is too large")
                payload = validate(json.loads(body))
                if broker.shard_for(payload["sessionId"]) != shard:
                    raise InvalidMessage("session routed to wrong shard")
                future = executor.submit(run_job, store, model, payload, properties.message_id)
                # The model runs off-thread; Pika's owning thread still sends heartbeats.
                while not future.done():
                    connection.sleep(0.2)
                result = future.result()
            except (InvalidMessage, ValueError, UnicodeDecodeError):
                LOG.warning("Rejected invalid job; moving to failed queue")
                ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)
            except (RetryableError, PyMongoError):
                LOG.warning("Processing exhausted retries; moving to failed queue")
                ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)
            else:
                # Commit session + idempotency record first. Redelivery returns saved result.
                ch.basic_ack(delivery_tag=method.delivery_tag)
                LOG.info("Committed job=%s session=%s status=%s version=%s",
                         result["messageId"], result["sessionId"], result["status"], result["version"])

        channel.basic_consume(queue=broker.queue_for(shard), on_message_callback=received, auto_ack=False)
        LOG.info("Listening on %s", broker.queue_for(shard))
        channel.start_consuming()
    finally:
        if connection.is_open:
            connection.close()
        executor.shutdown(wait=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--shard", type=int, required=True, choices=range(broker.SHARDS))
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    store, model = Store(), OllamaModel()
    try:
        while True:
            try:
                consume(args.shard, store, model)
            except (pika.exceptions.AMQPError, OSError):
                LOG.warning("Broker unavailable; reconnecting in 3 seconds")
                time.sleep(3)
    except KeyboardInterrupt:
        LOG.info("Worker stopped")
