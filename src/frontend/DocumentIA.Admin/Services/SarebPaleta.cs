namespace DocumentIA.Admin.Services;

/// <summary>
/// Paleta corporativa de SAREB para lo que se pinta desde C# (graficos). Los
/// mismos valores viven como tokens CSS en wwwroot/sareb.css; si cambia uno,
/// cambian los dos.
/// </summary>
public static class SarebPaleta
{
    public const string Verde = "#00858C";
    public const string AzulOscuro = "#046E97";
    public const string Azul = "#0086AD";
    public const string Turquesa = "#00ABA8";
    public const string VerdeClaro = "#90CCB7";
    public const string VerdeGris = "#6F927C";
    public const string Gris = "#BBBEB4";
    public const string GrisClaro = "#AFB6AC";
    public const string Naranja = "#F39E48";
    public const string Rojo = "#EA6B43";

    // De menos a mas intensidad, como pide la guia para comparativas.
    public static readonly string[] Serie = [GrisClaro, Gris, VerdeClaro, Turquesa, Azul, AzulOscuro, Verde];

    // Escala de magnitud (matriz del Monitor): de claro a oscuro, sin saltos de
    // luminosidad; solo en el ultimo paso el texto va en blanco.
    public static readonly string[] Escala = [VerdeClaro, Turquesa, Verde, Azul, AzulOscuro];

    public const string Ok = Turquesa;
    public const string Revision = Naranja;
    public const string Error = Rojo;
    public const string Neutro = Gris;
}
