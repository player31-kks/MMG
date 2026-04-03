using MMG.Models.Import;

namespace MMG.Core.Interfaces
{
    public interface IApiImportHandler
    {
        ApiImportFormat Format { get; }
        Task<IReadOnlyList<ImportedApiDefinition>> ImportAsync(IReadOnlyList<string> filePaths);
    }
}