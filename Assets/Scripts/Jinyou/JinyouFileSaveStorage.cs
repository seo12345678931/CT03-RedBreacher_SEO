using System;
using System.IO;
using UnityEngine;

public static class JinyouFileSaveStorage
{
    public static string GetSavePath(string fileName)
    {
        string safeFileName = string.IsNullOrWhiteSpace(fileName)
            ? "jinyou_save.json"
            : fileName.Trim();
        return Path.Combine(Application.persistentDataPath, safeFileName);
    }

    public static string GetBackupPath(string fileName)
    {
        return GetSavePath(fileName) + ".bak";
    }

    public static bool Exists(string fileName)
    {
        return File.Exists(GetSavePath(fileName));
    }

    public static bool TryRead(string fileName, bool backup, out string json, out string error)
    {
        string path = backup ? GetBackupPath(fileName) : GetSavePath(fileName);
        json = string.Empty;
        error = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                error = $"save file not found: {path}";
                return false;
            }

            json = File.ReadAllText(path);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryWrite(string fileName, string json, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "save json is empty";
            return false;
        }

        string path = GetSavePath(fileName);
        string backupPath = GetBackupPath(fileName);
        string tempPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, backupPath, true);
                }
                catch
                {
                    File.Copy(path, backupPath, true);
                    File.Copy(tempPath, path, true);
                    File.Delete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }

            return false;
        }
    }

    public static void Delete(string fileName)
    {
        DeleteIfExists(GetSavePath(fileName));
        DeleteIfExists(GetBackupPath(fileName));
        DeleteIfExists(GetSavePath(fileName) + ".tmp");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
