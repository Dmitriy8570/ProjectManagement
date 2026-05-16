namespace BusinessLogic.Common;

/// <summary>
/// Raised by command/query handlers when a referenced entity does not exist.
/// </summary>
public sealed class EntityNotFoundException : Exception
{
    public string EntityName { get; }
    public object Key { get; }

    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }
}
