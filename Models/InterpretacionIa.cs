using System.Text.Json.Serialization;

namespace ApiSueloInteligente.Models;

public class InterpretacionIa
{
  [JsonPropertyName("analisis_id")]
  public string AnalisisId { get; set; } = string.Empty;

  [JsonPropertyName("resumen")]
  public string Resumen { get; set; } = string.Empty;

  [JsonPropertyName("prioridad")]
  public string Prioridad { get; set; } = string.Empty;

  [JsonPropertyName("variable_prioritaria")]
  public string VariablePrioritaria { get; set; } = string.Empty;

  [JsonPropertyName("acciones")]
  public List<string> Acciones { get; set; } = [];

  [JsonPropertyName("advertencia")]
  public string Advertencia { get; set; } = string.Empty;

  [JsonPropertyName("generado_por")]
  public string GeneradoPor { get; set; } = string.Empty;

  [JsonPropertyName("fecha_generacion")]
  public DateTime FechaGeneracion { get; set; }
}
