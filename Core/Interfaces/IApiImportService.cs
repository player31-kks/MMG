using MMG.Models.Import;

namespace MMG.Core.Interfaces
{
    public interface IApiImportService
    {
        Task<ApiImportResult> ImportAsync(ApiImportRequest request);
    }
}