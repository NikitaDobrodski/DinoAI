using System.Text.Json;

namespace DinoAI.Core.Models;

public sealed class FileChatModelSettingsStore : IChatModelSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OpenAICompatibleChatModelOptions _current;

    public FileChatModelSettingsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Model settings path cannot be empty.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
        _current = LoadInitialOptions();
    }

    public OpenAICompatibleChatModelOptions Current => _current;

    public async Task<OpenAICompatibleChatModelOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OpenAICompatibleChatModelOptions> SaveAsync(
        OpenAICompatibleChatModelOptions options,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(options);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            _current = normalized;
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    private OpenAICompatibleChatModelOptions LoadInitialOptions()
    {
        var environmentOptions = OpenAICompatibleChatModelOptions.FromEnvironment();
        if (!File.Exists(_filePath))
        {
            return environmentOptions;
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            var saved = JsonSerializer.Deserialize<OpenAICompatibleChatModelOptions>(stream, JsonOptions);
            if (saved is null)
            {
                return environmentOptions;
            }

            return Normalize(saved with
            {
                BaseUrl = string.IsNullOrWhiteSpace(saved.BaseUrl) ? environmentOptions.BaseUrl : saved.BaseUrl,
                Model = string.IsNullOrWhiteSpace(saved.Model) ? environmentOptions.Model : saved.Model,
                ApiKey = string.IsNullOrWhiteSpace(saved.ApiKey) ? environmentOptions.ApiKey : saved.ApiKey
            });
        }
        catch (JsonException)
        {
            return environmentOptions;
        }
        catch (IOException)
        {
            return environmentOptions;
        }
    }

    private static OpenAICompatibleChatModelOptions Normalize(OpenAICompatibleChatModelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new ArgumentException("Base URL cannot be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("Model name cannot be empty.", nameof(options));
        }

        return new OpenAICompatibleChatModelOptions(
            options.BaseUrl.Trim().TrimEnd('/'),
            string.IsNullOrWhiteSpace(options.ApiKey) ? null : options.ApiKey.Trim(),
            options.Model.Trim());
    }
}
