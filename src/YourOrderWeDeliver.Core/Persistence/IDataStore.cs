using System.Text.Json;
using System.Text.Json.Serialization;

namespace YourOrderWeDeliver.Core.Persistence;

/// <summary>Loads and saves the system state. Keeps storage technology out of the domain classes.</summary>
public interface IDataStore
{
    /// <summary>Loads the last saved state, or returns <c>null</c> if nothing has been saved.</summary>
    SystemSnapshot? Load();

    /// <summary>Saves the complete state.</summary>
    void Save(SystemSnapshot snapshot);
}

/// <summary>
/// Stores the state as one JSON file. Each save writes a temporary file and then replaces the
/// previous file, so an interrupted save never leaves a half-written file (A1 QR1.3).
/// </summary>
public sealed class JsonFileDataStore : IDataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Creates a store for the given file path.</summary>
    public JsonFileDataStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
    }

    /// <summary>Gets the absolute path of the data file.</summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public SystemSnapshot? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        using FileStream stream = File.OpenRead(FilePath);
        return JsonSerializer.Deserialize<SystemSnapshot>(stream, SerializerOptions)
            ?? throw new InvalidDataException($"The data file '{FilePath}' is empty or invalid.");
    }

    /// <inheritdoc />
    public void Save(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = FilePath + ".tmp";
        using (FileStream stream = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(stream, snapshot, SerializerOptions);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}

/// <summary>Keeps the state in memory only; used by tests.</summary>
public sealed class InMemoryDataStore : IDataStore
{
    /// <summary>Gets the last saved snapshot.</summary>
    public SystemSnapshot? Saved { get; private set; }

    /// <summary>Gets how many times state has been saved.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc />
    public SystemSnapshot? Load() => Saved;

    /// <inheritdoc />
    public void Save(SystemSnapshot snapshot)
    {
        Saved = snapshot;
        SaveCount++;
    }
}
