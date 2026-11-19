using System.IO.Compression;
using Microsoft.Data.Sqlite;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Core;

public static class BackupService
{
    private const long MaxBackupBytes = 500L * 1024 * 1024;
    private const long MaxImageBytes = 25L * 1024 * 1024;
    private static readonly string[] ImageTypes = [".jpg", ".jpeg", ".png", ".bmp"];

    private static void AdminOnly()
    {
        if (!Session.RequireUser().IsAdmin) throw new UnauthorizedAccessException("Administrator permission required.");
    }

    public static void Create(string destination)
    {
        AdminOnly();
        var folder = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Directory.CreateDirectory(folder);
        var stage = Path.Combine(Path.GetTempPath(), "RestaurantBackup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var tmpZip = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var snapshot = Path.Combine(stage, "restaurant.sqlite3");
            using (var source = Database.Open())
            using (var snapshotDb = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = snapshot, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
            {
                snapshotDb.Open();
                source.BackupDatabase(snapshotDb);
            }
            using (var stream = File.Create(tmpZip))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(snapshot, "restaurant.sqlite3", CompressionLevel.Optimal);
                foreach (var path in Directory.EnumerateFiles(Database.ImagesFolder))
                {
                    var file = Path.GetFileName(path);
                    if (!ImageTypes.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                    zip.CreateEntryFromFile(path, "Images/" + file, CompressionLevel.Optimal);
                }
            }
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(tmpZip, destination);
        }
        finally
        {
            if (File.Exists(tmpZip)) File.Delete(tmpZip);
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }
    }

    public static string Restore(string archivePath)
    {
        AdminOnly();
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Backup not found.", archivePath);
        var stage = Path.Combine(Path.GetTempPath(), "RestaurantRestore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var sourcePath = Path.Combine(stage, "restaurant.sqlite3");
            var imageFolder = Path.Combine(stage, "Images");
            Directory.CreateDirectory(imageFolder);
            long total = 0;
            bool hasDatabase = false;
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    total += entry.Length;
                    if (total > MaxBackupBytes) throw new InvalidOperationException("Backup is too large.");
                    string target;
                    if (entry.FullName == "restaurant.sqlite3")
                    {
                        if (hasDatabase) throw new InvalidOperationException("Duplicate database entry.");
                        hasDatabase = true;
                        target = sourcePath;
                    }
                    else if (entry.FullName.StartsWith("Images/", StringComparison.Ordinal) &&
                        entry.FullName == "Images/" + entry.Name &&
                        ImageTypes.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()) &&
                        entry.Length <= MaxImageBytes)
                        target = Path.Combine(imageFolder, entry.Name);
                    else throw new InvalidOperationException("Backup contains unexpected files or paths.");
                    if (File.Exists(target)) throw new InvalidOperationException("Duplicate file inside backup.");
                    using var input = entry.Open();
                    using var output = File.Create(target);
                    input.CopyTo(output);
                }
            }
            if (!hasDatabase) throw new InvalidOperationException("No restaurant database inside the backup.");
            using (var check = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            {
                check.Open();
                using var integrity = check.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check";
                if (Convert.ToString(integrity.ExecuteScalar()) != "ok")
                    throw new InvalidOperationException("Backup database failed integrity checks.");
                using var schema = check.CreateCommand();
                schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' " +
                    "AND name IN ('Users','Orders','Meals','OrderItems','Invoices','DiningTables','Settings')";
                if (Convert.ToInt32(schema.ExecuteScalar()) != 7)
                    throw new InvalidOperationException("Not a Restaurant Management System database.");
            }
            // Keep a recoverable pre-restore copy beside the application's data.
            var safetyFile = Path.Combine(Database.DataFolder,
                "BeforeRestore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" +
                Guid.NewGuid().ToString("N")[..6] + ".zip");
            Create(safetyFile);
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            using (var destination = Database.Open())
            {
                source.Open();
                source.BackupDatabase(destination);
            }
            // Merge image files. Old images are deliberately retained, so a failure cannot erase them.
            Directory.CreateDirectory(Database.ImagesFolder);
            foreach (var file in Directory.EnumerateFiles(imageFolder))
                File.Copy(file, Path.Combine(Database.ImagesFolder, Path.GetFileName(file)), true);
            Database.Initialize(); // Upgrade a valid backup made by an earlier version.
            return safetyFile;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }
    }
}
