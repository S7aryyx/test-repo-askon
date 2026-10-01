namespace MiniPdm.Application.Interfaces;

public interface ICadFileLocator
{
    IReadOnlyList<string> GetCadFiles(string folderPath);
    string GetFileName(string path);
}
