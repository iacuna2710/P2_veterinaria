using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace Web_veterinaria.Models.Mascota;
public class mascotaModel : IValidatableObject
{
    public long IdMascota { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100)]
    public string Nombre { get; set; } = "";
    [Required(ErrorMessage = "La especie es obligatoria."), StringLength(100)]
    public string Especie { get; set; } = "";
    [Required(ErrorMessage = "La raza es obligatoria."), StringLength(100)]
    public string Raza { get; set; } = "";
    [Required(ErrorMessage = "El peso es obligatorio.")]
    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "El peso debe estar entre 0.01 y 999999.99 kg.")]
    [Display(Name = "Peso (kg)")]
    public decimal? Peso { get; set; }
    [Range(typeof(long), "1", "9223372036854775807", ParseLimitsInInvariantCulture = true, ErrorMessage = "Selecciona un cliente.")]
    [Display(Name = "Cliente")]
    public long IdCliente { get; set; }
    [ValidateNever]
    public string CedulaCliente { get; set; } = "";
    [ValidateNever]
    public string NombreCliente { get; set; } = "";
    [ValidateNever]
    public List<SelectListItem> Clientes { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Peso.HasValue && decimal.Round(Peso.Value, 2) != Peso.Value)
            yield return new ValidationResult("El peso admite hasta dos decimales.", [nameof(Peso)]);
    }
}
