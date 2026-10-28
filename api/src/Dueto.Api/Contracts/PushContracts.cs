namespace Dueto.Api.Contracts;

/// <summary>Mesmo formato do <c>PushSubscription.toJSON()</c> do navegador, para o PWA enviar direto.</summary>
public sealed record SubscribePushRequest(string? Endpoint, PushKeysRequest? Keys);

public sealed record PushKeysRequest(string? P256dh, string? Auth);

public sealed record UnsubscribePushRequest(string? Endpoint);

public sealed record VapidPublicKeyResponse(string PublicKey);
