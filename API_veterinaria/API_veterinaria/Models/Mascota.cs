using System.ComponentModel.DataAnnotations;

namespace API_veterinaria.Models;

public sealed class RegistrarMascota : IValidatableObject
{
    [Required, StringLength(100)]
    public string Nombre { get; init; } = "";
    [Required, StringLength(100)]
    public string Especie { get; init; } = "";
    [Required, StringLength(100)]
    public string Raza { get; init; } = "";
    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Peso { get; init; }
    [Range(typeof(long), "1", "9223372036854775807", ParseLimitsInInvariantCulture = true)]
    public long IdCliente { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(Peso, 2) != Peso)
            yield return new ValidationResult("El peso admite hasta dos decimales.", [nameof(Peso)]);
    }
}

public sealed record Mascota(long IdMascota, string Nombre, string Especie, string Raza,
    decimal Peso, long IdCliente, string CedulaCliente, string NombreCliente);
