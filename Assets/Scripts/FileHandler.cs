using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


public static class FileHandler
{
    public static bool SaveJsonObject<T>(
        T save,
        FileInfo fileInfo,
        bool prettyPrint = true,
        bool log = false)
    {
        return SaveTextFile(JsonUtility.ToJson(save, prettyPrint), fileInfo, log);
    }

    public static bool SaveJsonObject<T>(
        T save,
        string directory,
        string fileName,
        string fileExtension = ".json",
        bool prettyPrint = true,
        bool log = false)
    {
        return SaveTextFile(JsonUtility.ToJson(save, prettyPrint), directory, fileName, fileExtension, log);
    }

    private static bool SaveTextFile(string text, FileInfo file, bool log = false)
    {
        var directoryName = file.DirectoryName;
        var withoutExtension = Path.GetFileNameWithoutExtension(file.Name);
        var extension = file.Extension;
        return SaveTextFile(text, directoryName, withoutExtension, extension, log);
    }

    private static bool SaveTextFile(
        string text,
        string directory,
        string fileName,
        string fileExtension,
        bool log = false)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        fileExtension = PrepareFileExtension(fileExtension, log);
        var path2 = fileName.Trim() + fileExtension;
        var path = Path.Combine(directory, path2);
        try
        {
            File.WriteAllText(path, text);
            if (log)
                Debug.Log("Saved " + path2 + " to " + path + ")");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving file {fileName} to {path}:\n{ex}");
            return false;
        }
    }

    private static string PrepareFileExtension(string fileExtension, bool log)
    {

        //remove whitespace from file extension, from ends and in the middle
        foreach (var c in WhitespaceChars.Concat(InvalidFileNameChars).Distinct())
        {
            fileExtension = fileExtension.Replace($"{c}", "");
        }

        if (fileExtension.Length == 0 || fileExtension.Length == 1 && !char.IsLetter(fileExtension[0]))
        {
            #if DEBUG
            if(log)
                Debug.LogError($"Invalid file extension {fileExtension} - it likely won't be useful.");
            #endif
        }
        
        if (!fileExtension.StartsWith("."))
        {
#if DEBUG
            if (log)
            {
                Debug.LogWarning("Adding '.' to start of file extension " + fileExtension);
            }
#endif

            fileExtension = "." + fileExtension;
        }

        return fileExtension;
    }

    public static List<T> LoadAllJsonObjects<T>(string directory, string fileExtension)
    {
        var directoryInfo = new DirectoryInfo(directory);
        var objList = new List<T>();
        if (!directoryInfo.Exists)
            return objList;
        foreach (var file in directoryInfo.GetFiles())
        {
            if (file.Extension == fileExtension)
                objList.Add(LoadJsonObject<T>(directory, file.Name));
        }

        return objList;
    }

    public static T LoadJsonObject<T>(string directory, string fileName, string fileExtension)
    {
        return ObjectFromJson<T>(LoadTextFile(directory, fileName, fileExtension));
    }

    public static T LoadJsonObject<T>(FileInfo file)
    {
        if (file != null)
            return ObjectFromJson<T>(LoadTextFile(file.DirectoryName, file.Name));
        Debug.LogWarning("Attempting to create a json object from null file info");
        return default(T);
    }

    private static T LoadJsonObject<T>(string directory, string fullFileName)
    {
        return ObjectFromJson<T>(LoadTextFile(directory, fullFileName));
    }

    private static T ObjectFromJson<T>(string json)
    {
        if (!string.IsNullOrWhiteSpace(json))
            return JsonUtility.FromJson<T>(json);
        return default;
    }

    private static string LoadTextFile(string directory, string fileName, string fileExtension)
    {
        var fullFileName = fileName + fileExtension;
        return LoadTextFile(directory, fullFileName);
    }

    public static string LoadTextFile(FileInfo info)
    {
        if (info != null)
            return LoadTextFile(info.DirectoryName, info.Name);
        Debug.LogWarning("Attempting to load text file on null FileInfo");
        return string.Empty;
    }

    private static string LoadTextFile(string directory, string fullFileName)
    {
        if (!Directory.Exists(directory))
        {
            Debug.LogWarning("Directory not found at " + directory);
            return string.Empty;
        }

        var path = Path.Combine(directory, fullFileName);
        if (File.Exists(path))
            return File.ReadAllText(path);
        Debug.LogWarning("File not found at " + path);
        return string.Empty;
    }

    public static FileInfo[] GetFilesInDirectory(string directory, string extension = "", bool log = false)
    {
        if (Directory.Exists(directory))
        {
            var directoryInfo = new DirectoryInfo(directory);
            if (string.IsNullOrWhiteSpace(extension))
                return directoryInfo.GetFiles();
            var str = PrepareFileExtension(extension, log);
            return directoryInfo.GetFiles("*" + str);
        }

        if (log)
            Debug.LogWarning("Directory " + directory + " was not found");
        return null;
    }

    public static bool DeleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"File deletion error: {ex}");
            return false;
        }
    }

    public static bool DeleteFile(
        string directory,
        string fileNameSansExtension,
        string fileExtension,
        bool log = false)
    {
        var str = PrepareFileExtension(fileExtension, log);
        return DeleteFile(Path.Combine(directory, fileNameSansExtension + str));
    }

    public static bool DeleteFile(FileInfo file) => DeleteFile(file.FullName);

    public static bool ContainsInvalidFileNameCharacters(string name)
    {
        foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
        {
            if (name.Contains(invalidFileNameChar.ToString()))
                return true;
        }

        return name.Contains<char>('/') || name.Contains<char>('\\');
    }

    public static bool ContainsInvalidFileNameCharacters(
        string name,
        out List<char> invalidCharacters)
    {
        invalidCharacters = GetInvalidFileNameCharactersIn(name);
        return invalidCharacters.Count > 0;
    }

    public static List<char> GetInvalidFileNameCharactersIn(
        string name,
        char[] additionalInvalidChars = null)
    {
        var fileNameCharacters = new List<char>();
        foreach (var ch in InvalidFileNameChars)
        {
            if (name.Contains<char>(ch))
                fileNameCharacters.Add(ch);
        }

        if (additionalInvalidChars != null)
        {
            foreach (var additionalInvalidChar in additionalInvalidChars)
            {
                if (name.Contains<char>(additionalInvalidChar))
                    fileNameCharacters.Add(additionalInvalidChar);
            }
        }

        return fileNameCharacters;
    }

    private static readonly char[] InvalidFileNameChars =
        Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).Distinct().ToArray();

    private static readonly char[] WhitespaceChars = { ' ', '\t', '\n', '\r' };
}