using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using OrchardCore.Secrets.Models;

namespace OrchardCore.Secrets.Services;

public class DatabaseSecretStore : ISecretStore
{
    private readonly SecretsDocumentManager _manager;
    private readonly DatabaseSecretDataProtector _databaseSecretDataProtector;
    protected readonly IStringLocalizer S;

    public DatabaseSecretStore(
        SecretsDocumentManager manager,
        DatabaseSecretDataProtector databaseSecretDataProtector,
        IStringLocalizer<DatabaseSecretStore> stringLocalizer)
    {
        _manager = manager;
        _databaseSecretDataProtector = databaseSecretDataProtector;
        S = stringLocalizer;
    }

    public string Name => nameof(DatabaseSecretStore);
    public string DisplayName => S["Database Secret Store"];
    public bool IsReadOnly => false;

    public async Task<Secret> GetSecretAsync(string key, Type type)
    {
        if (!typeof(Secret).IsAssignableFrom(type))
        {
            throw new ArgumentException("The type must implement " + nameof(Secret));
        }

        var secretsDocument = await _manager.GetSecretsDocumentAsync();
        if (secretsDocument.Secrets.TryGetValue(key, out var secretDocument))
        {
            var value = _databaseSecretDataProtector.Unprotect(secretDocument.Value);
            var secret = JsonConvert.DeserializeObject(value, type) as Secret;

            return secret;
        }

        return null;
    }

    public Task UpdateSecretAsync(string key, Secret secret)
    {
        var secretDocument = new SecretDocument
        {
            Value = _databaseSecretDataProtector.Protect(JsonConvert.SerializeObject(secret)),
        };

        return _manager.UpdateSecretAsync(key, secretDocument);
    }

    public Task RemoveSecretAsync(string key) => _manager.RemoveSecretAsync(key);
}