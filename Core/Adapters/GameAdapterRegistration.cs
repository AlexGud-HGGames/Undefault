namespace Core.Adapters;

/// <summary>
/// Metadata an <see cref="IGameAdapter{TInput}"/> must declare to participate in
/// multi-title routing. The host registers one of these per supported title; the
/// adapter implementation itself stays typed on its own payload DTO.
/// </summary>
public sealed record GameAdapterRegistration(
    string TitleId,
    int? AppId,
    string EndpointPath,
    string Description);
