using System.Data;
using System.Transactions;
using API_veterinaria.Data;
using API_veterinaria.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace API_veterinaria.Tests;

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRACTICA2_TEST_CONNECTION")))
            Skip = "Configura PRACTICA2_TEST_CONNECTION para probar SQL Server con rollback.";
    }
}

[CollectionDefinition("SQL", DisableParallelization = true)]
public sealed class SqlCollection;

[Collection("SQL")]
public sealed class SqlTests
{
    private static string ConnectionString => Environment.GetEnvironmentVariable("PRACTICA2_TEST_CONNECTION")!;
    private static string Unique() => "prueba_" + Guid.NewGuid().ToString("N");

    [SqlFact]
    public async Task RepositorioRegistraYConsultaDatosConRollback()
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Practica2"] = ConnectionString }).Build();
        var repository = new VeterinariaRepository(config);
        var cedula = Unique();
        var id = await repository.RegistrarClienteAsync(new RegistrarCliente
            { Cedula = " " + cedula + " ", Nombre = " Cliente de prueba ", Correo = " prueba@example.com " }, default);
        var cliente = Assert.Single(await repository.ConsultarClientesAsync(default), c => c.IdCliente == id);
        Assert.Equal(cedula, cliente.Cedula);
        Assert.Equal("Cliente de prueba", cliente.Nombre);
        Assert.True(cliente.Estado);
        var especie = Unique();
        var mascotaId = await repository.RegistrarMascotaAsync(new RegistrarMascota
            { Nombre = " Luna ", Especie = especie, Raza = " Mixta ", Peso = 2.50m, IdCliente = id }, default);
        var mascota = Assert.Single(await repository.ConsultarMascotasAsync(default), m => m.IdMascota == mascotaId);
        Assert.Equal("Luna", mascota.Nombre);
        Assert.Equal(cedula, mascota.CedulaCliente);
        Assert.Equal("Cliente de prueba", mascota.NombreCliente);
        Assert.Equal(2.50m, mascota.Peso);
        // Sin Complete: todos los registros se revierten al salir.
    }

    [SqlFact]
    public async Task CedulaDuplicadaSeRechazaInclusoConEspacios()
    {
        await using var session = await SqlSession.OpenAsync();
        var cedula = Unique();
        await session.ClienteAsync(cedula);
        Assert.Equal(-1, await session.ClienteAsync(" " + cedula + " "));
        var cantidad = await session.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Clientes WHERE Cedula = @cedula", new { cedula }, session.Transaction);
        Assert.Equal(1, cantidad);
        Assert.True(await session.ClienteAsync(Unique()) > 0);
    }

    [SqlFact]
    public async Task TerceraMascotaSeRechazaParaOtroDuenoConMayusculasYEspacios()
    {
        await using var session = await SqlSession.OpenAsync();
        var cliente1 = await session.ClienteAsync(Unique());
        var cliente2 = await session.ClienteAsync(Unique());
        var especie = Unique();
        await session.MascotaAsync(cliente1, especie);
        await session.MascotaAsync(cliente2, " " + especie.ToUpperInvariant() + " ");
        var resultado = await session.MascotaAsync(cliente2, especie);
        Assert.Equal(-2, resultado);
        var cantidad = await session.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Mascotas WHERE Especie = @especie", new { especie }, session.Transaction);
        Assert.Equal(2, cantidad);
        Assert.True(await session.MascotaAsync(cliente2, Unique()) > 0);
    }

    [SqlFact]
    public async Task ClienteInexistenteSeRechaza()
    {
        await using var session = await SqlSession.OpenAsync();
        var error = await Assert.ThrowsAsync<SqlException>(() => session.MascotaAsync(-1, Unique()));
        Assert.Equal(50003, error.Number);
    }

    [SqlFact]
    public async Task PesoConMasDeDosDecimalesSeRechazaEnSql()
    {
        await using var session = await SqlSession.OpenAsync();
        var id = await session.ClienteAsync(Unique());
        var error = await Assert.ThrowsAsync<SqlException>(() => session.MascotaAsync(id, Unique(), 1.234m));
        Assert.Equal(50004, error.Number);
    }

    [SqlFact]
    public async Task AltaClienteBloqueaUnaSolicitudConcurrenteHastaRollback()
    {
        await using var first = await SqlSession.OpenAsync();
        await using var second = await SqlSession.OpenAsync();
        var cedula = Unique();
        await first.ClienteAsync(cedula);
        await second.Connection.ExecuteAsync("SET LOCK_TIMEOUT 250", transaction: second.Transaction);
        var error = await Assert.ThrowsAsync<SqlException>(() => second.ClienteAsync(cedula));
        Assert.Contains(error.Errors.Cast<SqlError>(), e => e.Number == 1222);
    }

    [SqlFact]
    public async Task AltaMascotaMantieneBloqueadoElConteoConcurrente()
    {
        await using var first = await SqlSession.OpenAsync();
        var id = await first.ClienteAsync(Unique());
        await first.MascotaAsync(id, Unique());
        await using var second = new SqlConnection(ConnectionString);
        await second.OpenAsync();
        await second.ExecuteAsync("SET LOCK_TIMEOUT 250");
        var error = await Assert.ThrowsAsync<SqlException>(() => second.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Mascotas WITH (TABLOCKX, HOLDLOCK)"));
        Assert.Equal(1222, error.Number);
    }

    private sealed class SqlSession(SqlConnection connection, SqlTransaction transaction) : IAsyncDisposable
    {
        public SqlConnection Connection { get; } = connection;
        public SqlTransaction Transaction { get; } = transaction;
        public static async Task<SqlSession> OpenAsync()
        {
            var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            return new SqlSession(connection, (SqlTransaction)await connection.BeginTransactionAsync());
        }
        public Task<long> ClienteAsync(string cedula) => Connection.ExecuteScalarAsync<long>("dbo.RegistrarCliente",
            new { Cedula = cedula, Nombre = "Prueba", Correo = "prueba@example.com" }, Transaction, commandType: CommandType.StoredProcedure);
        public Task<long> MascotaAsync(long idCliente, string especie, decimal peso = 1) => Connection.ExecuteScalarAsync<long>(
            "dbo.RegistrarMascota", new { Nombre = "Luna", Especie = especie, Raza = "Mixta", Peso = peso, IdCliente = idCliente },
            Transaction, commandType: CommandType.StoredProcedure);
        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
