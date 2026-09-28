using System.Text.Json.Serialization;

namespace ApiSueloInteligente.Models;

public class NuevaLecturaSensor
{
  [JsonPropertyName("lecturaId")]
  public string LecturaId { get; set; } = string.Empty;

  [JsonPropertyName("dispositivoId")]
  public string DispositivoId { get; set; } = string.Empty;

  [JsonPropertyName("campoId")]
  public string CampoId { get; set; } = string.Empty;

  [JsonPropertyName("campoNombre")]
  public string CampoNombre { get; set; } = string.Empty;

  [JsonPropertyName("cultivo")]
  public string Cultivo { get; set; } = string.Empty;

  [JsonPropertyName("fechaCaptura")]
  public DateTime? FechaCaptura { get; set; }

  [JsonPropertyName("ph")]
  public double Ph { get; set; }

  [JsonPropertyName("conductividad")]
  public double Conductividad { get; set; }

  [JsonPropertyName("humedad")]
  public double Humedad { get; set; }

  [JsonPropertyName("orp")]
  public double Orp { get; set; }

  [JsonPropertyName("temperatura")]
  public double Temperatura { get; set; }
}
