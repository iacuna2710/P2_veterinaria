using System.ComponentModel.DataAnnotations;

namespace API_veterinaria.Models;

public sealed class RegistrarCliente
{
    [Required, StringLength(50)]
    public string Cedula { get; init; } = "";
    [Required, StringLength(100)]
    public string Nombre { get; init; } = "";
    [Required, EmailAddress, StringLength(100)]
    public string Correo { get; init; } = "";
}

public sealed record Cliente(long IdCliente, string Cedula, string Nombre, string Correo, bool Estado);
