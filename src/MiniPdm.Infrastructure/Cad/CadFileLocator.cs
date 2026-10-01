using MiniPdm.Application.Interfaces;

namespace MiniPdm.Infrastructure.Cad;

public sealed class CadFileLocator : ICadFileLocator
{
    public string GetFileName(string path) => Path.GetFileName(path);

    public IReadOnlyList<string> GetCadFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        return Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(x => x.EndsWith(".a3d", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".m3d", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
