using MMG.Core.Interfaces;
using MMG.Models;
using MMG.Models.Import;

namespace MMG.Services
{
    public class ApiImportService : IApiImportService
    {
        private readonly IReadOnlyDictionary<ApiImportFormat, IApiImportHandler> _handlers;
        private readonly DatabaseService _databaseService;

        public ApiImportService(IEnumerable<IApiImportHandler> handlers, DatabaseService databaseService)
        {
            _handlers = handlers.ToDictionary(handler => handler.Format);
            _databaseService = databaseService;
        }

        public async Task<ApiImportResult> ImportAsync(ApiImportRequest request)
        {
            if (request.FilePaths.Count == 0)
            {
                return new ApiImportResult();
            }

            if (!_handlers.TryGetValue(request.Format, out var handler))
            {
                throw new NotSupportedException($"'{request.Format}' 형식의 import는 아직 지원되지 않습니다.");
            }

            var importedDefinitions = await handler.ImportAsync(request.FilePaths);
            var savedRequests = importedDefinitions
                .Select(definition => MapToSavedRequest(definition, request.TargetFolderId))
                .ToList();

            await _databaseService.SaveRequestsAsync(savedRequests);

            return new ApiImportResult
            {
                SavedRequests = savedRequests
            };
        }

        private SavedRequest MapToSavedRequest(ImportedApiDefinition definition, int? targetFolderId)
        {
            return new SavedRequest
            {
                Name = definition.Name,
                IpAddress = definition.IpAddress,
                Port = definition.Port,
                IsBigEndian = definition.IsBigEndian,
                WaitForResponse = definition.WaitForResponse,
                FolderId = targetFolderId,
                RequestSchemaJson = _databaseService.SerializeDataFields(definition.RequestHeaders) +
                    "|" + _databaseService.SerializeDataFields(definition.RequestPayload),
                ResponseSchemaJson = _databaseService.SerializeDataFields(definition.ResponseHeaders) +
                    "|" + _databaseService.SerializeDataFields(definition.ResponsePayload)
            };
        }
    }
}