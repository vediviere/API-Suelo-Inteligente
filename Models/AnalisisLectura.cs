using System.Text.Json.Serialization;

namespace ApiSueloInteligente.Models;

public class AnalisisLectura
{
  [JsonPropertyName("analisis_id")]
  public string AnalisisId { get; set; } = string.Empty;

  [JsonPropertyName("lectura_id")]
  public string LecturaId { get; set; } = string.Empty;

  [JsonPropertyName("fecha_procesamiento")]
  public DateTime FechaProcesamiento { get; set; }

  [JsonPropertyName("estado_general")]
  public string EstadoGeneral { get; set; } = string.Empty;

  [JsonPropertyName("puntaje_general")]
  public int PuntajeGeneral { get; set; }

  [JsonPropertyName("resultados")]
  public List<ResultadoVariable> Resultados { get; set; } = [];

  [JsonPropertyName("alertas")]
  public List<AlertaLectura> Alertas { get; set; } = [];

  [JsonPropertyName("recomendaciones")]
  public List<Recomendacion> Recomendaciones { get; set; } = [];
}

public class AlertaLectura
{
  [JsonPropertyName("nivel")]
  public string Nivel { get; set; } = string.Empty;

  [JsonPropertyName("variable")]
  public string Variable { get; set; } = string.Empty;

  [JsonPropertyName("mensaje")]
  public string Mensaje { get; set; } = string.Empty;
}
