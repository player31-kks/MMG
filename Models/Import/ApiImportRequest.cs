using System.Collections.ObjectModel;

namespace MMG.Models.Import
{
    public class ApiImportRequest
    {
        public ApiImportFormat Format { get; set; }
        public IReadOnlyList<string> FilePaths { get; set; } = Array.Empty<string>();
        public int? TargetFolderId { get; set; }
    }

    public class ImportedApiDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public bool IsBigEndian { get; set; } = true;
        public bool WaitForResponse { get; set; } = true;
        public ObservableCollection<DataField> RequestHeaders { get; set; } = new();
        public ObservableCollection<DataField> RequestPayload { get; set; } = new();
        public ObservableCollection<DataField> ResponseHeaders { get; set; } = new();
        public ObservableCollection<DataField> ResponsePayload { get; set; } = new();
    }

    public class ApiImportResult
    {
        public IReadOnlyList<SavedRequest> SavedRequests { get; init; } = Array.Empty<SavedRequest>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public int ImportedCount => SavedRequests.Count;
    }
}