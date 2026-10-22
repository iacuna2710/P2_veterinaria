using System.ComponentModel.DataAnnotations;
namespace Web_veterinaria.Models.Cliente;
public class clienteModel
{
    public long IdCliente { get; set; }
    [Required(ErrorMessage = "La cédula es obligatoria."), StringLength(50)]
    [Display(Name = "Cédula")]
    public string Cedula { get; set; } = "";
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100)]
    public string Nombre { get; set; } = "";
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingresa un correo válido."), StringLength(100)]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = "";
    public bool Estado { get; set; }
}
