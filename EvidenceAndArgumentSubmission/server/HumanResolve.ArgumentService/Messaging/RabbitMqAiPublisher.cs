using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace HumanResolve.ArgumentService.Messaging;

public sealed class RabbitMqAiPublisher : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly string _prefix;
    private readonly int _shards;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqAiPublisher(IConfiguration configuration)
    {
        _prefix = configuration["AI_QUEUE_PREFIX"] ?? "ai.resolution";
        _shards = configuration.GetValue<int?>("AI_SHARDS") ?? 2;
        if (_shards is < 1 or > 32) throw new ArgumentException("AI_SHARDS must be 1-32");
        _factory = new ConnectionFactory
        {
            Uri = new Uri(configuration["RABBITMQ_URL"] ?? "amqp://ai:ai-local-only@localhost:5672/%2F"),
            ClientProvidedName = "EvidenceAndArgumentSubmission",
            AutomaticRecoveryEnabled = false, // Lazy reconnect on the next explicit submission.
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task PublishAsync(string session, int people, string message, string messageId, CancellationToken token)
    {
        await _gate.WaitAsync(token); // A RabbitMQ channel must not publish concurrently.
        try
        {
            await EnsureChannelAsync(token);
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(session));
            var shard = BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(0, 8)) % (ulong)_shards;
            var body = JsonSerializer.SerializeToUtf8Bytes(new { sessionId = session, people, message });
            var properties = new BasicProperties
            {
                ContentType = "application/json", ContentEncoding = "utf-8",
                Persistent = true, MessageId = messageId, CorrelationId = session
            };
            await _channel!.BasicPublishAsync(_prefix + ".requests", shard.ToString(),
                mandatory: true, basicProperties: properties, body: body, cancellationToken: token);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task EnsureChannelAsync(CancellationToken token)
    {
        if (_channel?.IsOpen == true && _connection?.IsOpen == true) return;
        await ResetAsync();
        _connection = await _factory.CreateConnectionAsync(token);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        await _channel.ExchangeDeclareAsync(_prefix + ".requests", ExchangeType.Direct, durable: true, cancellationToken: token);
        await _channel.ExchangeDeclareAsync(_prefix + ".failed", ExchangeType.Direct, durable: true, cancellationToken: token);
        await _channel.QueueDeclareAsync(_prefix + ".failed", durable: true, exclusive: false, autoDelete: false, cancellationToken: token);
        await _channel.QueueBindAsync(_prefix + ".failed", _prefix + ".failed", "failed", cancellationToken: token);
        for (var shard = 0; shard < _shards; shard++)
        {
            var queue = _prefix + ".work." + shard;
            await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-single-active-consumer"] = true,
                    ["x-dead-letter-exchange"] = _prefix + ".failed",
                    ["x-dead-letter-routing-key"] = "failed"
                }, cancellationToken: token);
            await _channel.QueueBindAsync(queue, _prefix + ".requests", shard.ToString(), cancellationToken: token);
        }
    }

    private async Task ResetAsync()
    {
        var channel = _channel;
        var connection = _connection;
        _channel = null;
        _connection = null;
        try { if (channel is not null) await channel.DisposeAsync(); }
        finally { if (connection is not null) await connection.DisposeAsync(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { await ResetAsync(); }
        finally { _gate.Release(); }
    }
}
