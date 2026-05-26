namespace ReSeq.Core.Services;

public interface IFileSystemService
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    IEnumerable<string> EnumerateFiles(string folderPath);

    void MoveFile(string sourcePath, string targetPath);

    void DeleteFile(string path);
}
