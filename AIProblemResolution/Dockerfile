FROM python:3.12-slim
ENV PYTHONUNBUFFERED=1 PYTHONDONTWRITEBYTECODE=1
WORKDIR /app
COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt \
    && groupadd --gid 10001 ai \
    && useradd --uid 10001 --gid ai --no-create-home ai
COPY broker.py core.py worker.py result.py replay_failed.py ./
COPY tools/migrate_sqlite.py ./migrate_sqlite.py
USER ai
CMD ["python", "worker.py", "--shard", "0"]
