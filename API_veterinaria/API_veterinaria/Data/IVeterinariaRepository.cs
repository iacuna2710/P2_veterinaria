using API_veterinaria.Models;

namespace API_veterinaria.Data;

public interface IVeterinariaRepository
{
    Task<IEnumerable<Cliente>> ConsultarClientesAsync(CancellationToken cancellationToken);
    Task<long> RegistrarClienteAsync(RegistrarCliente cliente, CancellationToken cancellationToken);
    Task<IEnumerable<Mascota>> ConsultarMascotasAsync(CancellationToken cancellationToken);
    Task<long> RegistrarMascotaAsync(RegistrarMascota mascota, CancellationToken cancellationToken);
}
