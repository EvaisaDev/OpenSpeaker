using System.IO;
using LiteDB;
using OpenSpeaker.Data;
namespace OpenSpeaker.Sync;

public static class DatabaseSnapshot
{
    public const int FormatVersion = 1;

    private static readonly string[] PreservedSettingsFields =
    {
        "InstanceId", "InstanceName", "WindowLeft", "WindowTop", "WindowWidth", "WindowHeight", "LastSeenVersion"
    };

    public static string Export(DatabaseContext db, string appVersion)
    {
        var collections = new BsonDocument();
        foreach (var name in db.GetCollectionNames())
            collections[name] = new BsonArray(db.RawCollection(name).FindAll());

        var root = new BsonDocument
        {
            ["format"] = FormatVersion,
            ["appVersion"] = appVersion,
            ["exportedAt"] = DateTime.UtcNow,
            ["collections"] = collections
        };
        return JsonSerializer.Serialize(root);
    }

    public static void Apply(string dbPath, string json, Func<string, bool> includeCollection)
    {
        var root = JsonSerializer.Deserialize(json).AsDocument;
        if (root["format"].AsInt32 != FormatVersion)
            throw new InvalidOperationException($"Unsupported sync format {root["format"]}.");
        var incoming = root["collections"].AsDocument;

        var tempPath = dbPath + ".sync-tmp";
        DeleteIfExists(tempPath);
        DeleteIfExists(LogPathFor(tempPath));

        try
        {
            if (File.Exists(dbPath))
                File.Copy(dbPath, tempPath);

            using (var db = new LiteDatabase(tempPath))
            {
                var preserved = ReadPreservedSettings(db);

                foreach (var name in db.GetCollectionNames().ToList())
                    if (includeCollection(name)) db.DropCollection(name);

                foreach (var kv in incoming)
                {
                    if (!includeCollection(kv.Key)) continue;
                    var docs = kv.Value.AsArray.Select(v => v.AsDocument).ToList();
                    if (docs.Count > 0)
                        db.GetCollection(kv.Key).Insert(docs);
                }

                RestorePreservedSettings(db, preserved);
                db.Checkpoint();
            }
            DeleteIfExists(LogPathFor(tempPath));

            var backupPath = dbPath + ".pre-sync.bak";
            DeleteIfExists(backupPath);
            if (File.Exists(dbPath))
                File.Move(dbPath, backupPath);
            File.Move(tempPath, dbPath);
        }
        finally
        {
            DeleteIfExists(tempPath);
            DeleteIfExists(LogPathFor(tempPath));
        }
    }

    private static string LogPathFor(string dbPath) =>
        Path.Combine(Path.GetDirectoryName(dbPath) ?? string.Empty, Path.GetFileNameWithoutExtension(dbPath) + "-log" + Path.GetExtension(dbPath));

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static BsonDocument? ReadPreservedSettings(LiteDatabase db)
    {
        var doc = db.GetCollection("settings").FindById(1);
        if (doc == null) return null;
        var preserved = new BsonDocument();
        foreach (var field in PreservedSettingsFields)
            if (doc.ContainsKey(field)) preserved[field] = doc[field];
        return preserved;
    }

    private static void RestorePreservedSettings(LiteDatabase db, BsonDocument? preserved)
    {
        if (preserved == null) return;
        var col = db.GetCollection("settings");
        var doc = col.FindById(1);
        if (doc == null) return;
        foreach (var kv in preserved)
            doc[kv.Key] = kv.Value;
        col.Update(doc);
    }
}
