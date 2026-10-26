namespace Dueto.Tests.Infrastructure;

/// <summary>Compartilha um único Postgres em container entre todos os testes de integração.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "postgres";
}
