using System.Data;
using API_veterinaria.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace API_veterinaria.Data;

public sealed class VeterinariaRepository(IConfiguration configuration) : IVeterinariaRepository
{
    private SqlConnection CrearConexion() => new(configuration.GetConnectionString("Practica2")
        ?? throw new InvalidOperationException("Falta la conexión Practica2."));

    public async Task<IEnumerable<Cliente>> ConsultarClientesAsync(CancellationToken cancellationToken)
    {
        await using var connection = CrearConexion();
        return await connection.QueryAsync<Cliente>(new CommandDefinition("dbo.ConsultarClientes",
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<long> RegistrarClienteAsync(RegistrarCliente cliente, CancellationToken cancellationToken)
    {
        await using var connection = CrearConexion();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("dbo.RegistrarCliente",
            new { Cedula = cliente.Cedula.Trim(), Nombre = cliente.Nombre.Trim(), Correo = cliente.Correo.Trim() },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Mascota>> ConsultarMascotasAsync(CancellationToken cancellationToken)
    {
        await using var connection = CrearConexion();
        return await connection.QueryAsync<Mascota>(new CommandDefinition("dbo.ConsultarMascotas",
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<long> RegistrarMascotaAsync(RegistrarMascota mascota, CancellationToken cancellationToken)
    {
        await using var connection = CrearConexion();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("dbo.RegistrarMascota",
            new { Nombre = mascota.Nombre.Trim(), Especie = mascota.Especie.Trim(), Raza = mascota.Raza.Trim(), mascota.Peso, mascota.IdCliente },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }
}
