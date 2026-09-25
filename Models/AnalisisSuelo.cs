using System.Text.Json.Serialization;

namespace ApiSueloInteligente.Models;

public class AnalisisSuelo
{
  [JsonPropertyName("analisis_id")]
  public string AnalisisId { get; set; } = string.Empty;

  [JsonPropertyName("lectura_id")]
  public string LecturaId { get; set; } = string.Empty;

  [JsonPropertyName("fecha_procesamiento")]
  public DateTimeOffset FechaProcesamiento { get; set; }

  [JsonPropertyName("estado_general")]
  public string EstadoGeneral { get; set; } = string.Empty;

  [JsonPropertyName("puntaje_general")]
  public int PuntajeGeneral { get; set; }

  [JsonPropertyName("resultados")]
  public List<ResultadoVariable> Resultados { get; set; } = [];

  [JsonPropertyName("alertas")]
  public List<string> Alertas { get; set; } = [];

  [JsonPropertyName("recomendaciones")]
  public List<Recomendacion> Recomendaciones { get; set; } = [];
}

public class ResultadoVariable
{
  [JsonPropertyName("variable")]
  public string Variable { get; set; } = string.Empty;

  [JsonPropertyName("nombre")]
  public string Nombre { get; set; } = string.Empty;

  [JsonPropertyName("valor")]
  public double Valor { get; set; }

  [JsonPropertyName("unidad")]
  public string Unidad { get; set; } = string.Empty;

  [JsonPropertyName("estado")]
  public string Estado { get; set; } = string.Empty;

  [JsonPropertyName("rango_recomendado")]
  public RangoRecomendado RangoRecomendado { get; set; } = new();

  [JsonPropertyName("mensaje")]
  public string Mensaje { get; set; } = string.Empty;
}

public class RangoRecomendado
{
  [JsonPropertyName("min")]
  public double Min { get; set; }

  [JsonPropertyName("max")]
  public double Max { get; set; }
}

public class Recomendacion
{
  [JsonPropertyName("prioridad")]
  public string Prioridad { get; set; } = string.Empty;

  [JsonPropertyName("titulo")]
  public string Titulo { get; set; } = string.Empty;

  [JsonPropertyName("descripcion")]
  public string Descripcion { get; set; } = string.Empty;
}
