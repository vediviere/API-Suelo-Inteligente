using System.Text.Json.Serialization;

namespace ApiSueloInteligente.Models;

public class CatalogoRegional
{
  [JsonPropertyName("cultivos")]
  public Dictionary<string, CultivoRegional> Cultivos { get; set; } = [];
}

public class CultivoRegional
{
  [JsonPropertyName("nombre_comun")]
  public string NombreComun { get; set; } = string.Empty;

  [JsonPropertyName("nombre_cientifico")]
  public string NombreCientifico { get; set; } = string.Empty;

  [JsonPropertyName("tipo")]
  public string Tipo { get; set; } = string.Empty;

  [JsonPropertyName("zonas_representativas")]
  public List<string> ZonasRepresentativas { get; set; } = [];

  [JsonPropertyName("notas")]
  public string Notas { get; set; } = string.Empty;

  [JsonPropertyName("rangos")]
  public Dictionary<string, RangoRegional> Rangos { get; set; } = [];
}

public class RangoRegional
{
  [JsonPropertyName("min")]
  public double Min { get; set; }

  [JsonPropertyName("optimo")]
  public double Optimo { get; set; }

  [JsonPropertyName("max")]
  public double Max { get; set; }
}
