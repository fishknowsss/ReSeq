namespace ReSeq.Core.Services;

public sealed class PhysicalFileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> EnumerateFiles(string folderPath) => Directory.EnumerateFiles(folderPath);

    public void MoveFile(string sourcePath, string targetPath) => File.Move(sourcePath, targetPath);

    public void DeleteFile(string path) => File.Delete(path);
}
