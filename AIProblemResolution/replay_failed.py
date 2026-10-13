"""Replay one dead-letter after fixing the underlying problem."""
import json
import broker
from core import validate, validate_job_id


if __name__ == "__main__":
    connection = broker.connect()
    try:
        channel = connection.channel()
        broker.declare(channel)
        method, properties, body = channel.basic_get(queue=broker.FAILED_QUEUE, auto_ack=False)
        if method is None:
            print("Failed queue is empty.")
        else:
            payload = validate(json.loads(body))
            validate_job_id(properties.message_id)
            broker.publish_request(payload, properties.message_id)
            channel.basic_ack(delivery_tag=method.delivery_tag)
            print("Confirmed replay of messageId=" + properties.message_id)
    finally:
        # If validation/publication fails, close requeues the original failed message.
        connection.close()
