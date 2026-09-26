using System.Text.Json.Serialization;

namespace ApiSueloInteligente.Models;

public class NuevaLecturaSensor
{
  [JsonPropertyName("dispositivoId")]
  public string DispositivoId { get; set; } = string.Empty;

  [JsonPropertyName("cultivo")]
  public string Cultivo { get; set; } = string.Empty;

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
