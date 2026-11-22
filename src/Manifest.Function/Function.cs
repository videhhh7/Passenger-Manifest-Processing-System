using Manifest.Function.Models;
using Manifest.Function.Services;

namespace Manifest.Function;

/// <summary>
/// Framework-neutral entry point. An AWS Lambda adapter can deserialize an
/// EventBridge event and call HandleAsync. Cloud SDK concerns stay outside
/// the workflow/domain layer.
/// </summary>
public sealed class Function(ManifestWorkflow workflow)
{
    public Task<ManifestResult> HandleAsync(
        ManifestRequest request,
        CancellationToken cancellationToken = default) =>
        workflow.ExecuteAsync(request, cancellationToken);
}
