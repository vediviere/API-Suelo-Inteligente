using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApiSueloInteligente.Models;

namespace ApiSueloInteligente.Services;

public class ServicioIa
{
  private readonly HttpClient _httpClient;
  private readonly IConfiguration _configuration;

  private static readonly JsonSerializerOptions OpcionesJson = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public ServicioIa(HttpClient httpClient, IConfiguration configuration)
  {
    _httpClient = httpClient;
    _configuration = configuration;
  }

  public async Task<InterpretacionIa> GenerarInterpretacionAsync(
      LecturaSensor lectura,
      AnalisisLectura analisis,
      CancellationToken cancellationToken = default)
  {
    var apiKey = _configuration["Groq:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
      throw new InvalidOperationException(
          "No se configuró la clave de Groq.");
    }

    var modelo = _configuration["Groq:Modelo"] ?? "openai/gpt-oss-20b";

    var datosAnalisis = JsonSerializer.Serialize(new
    {
      zona = lectura.Zona,
      cultivo = lectura.Cultivo,
      campo = lectura.CampoNombre,
      dispositivo = lectura.DispositivoId,
      estado_general = analisis.EstadoGeneral,
      puntaje_general = analisis.PuntajeGeneral,
      variables = analisis.Resultados.Select(resultado => new
      {
        resultado.Nombre,
        resultado.Valor,
        resultado.Unidad,
        resultado.Estado,
        resultado.Condicion,
        resultado.DiferenciaParaRango,
        resultado.RangoRecomendado,
        resultado.Mensaje
      }),
      alertas = analisis.Alertas,
      recomendaciones_regionales = analisis.Recomendaciones
    });

    var instrucciones = """
            Eres un asistente especializado en interpretación de mediciones
            básicas de suelo para TLALCANI.

            Utiliza únicamente los datos proporcionados por el sistema.
            Los rangos regionales ya fueron calculados por el backend.

            Reglas:
            - Responde en español claro y breve.
            - Considera la zona y el cultivo.
            - Identifica la variable que requiere mayor atención.
            - Propón entre una y tres acciones prudentes.
            - No recomiendes cantidades de fertilizantes o químicos.
            - No diagnostiques nitrógeno, fósforo o potasio.
            - No contradigas los estados calculados por el sistema.
            - No inventes mediciones ni rangos.
            """;

    var esquema = new
    {
      type = "object",
      properties = new Dictionary<string, object>
      {
        ["resumen"] = new
        {
          type = "string"
        },
        ["prioridad"] = new
        {
          type = "string",
          @enum = new[] { "baja", "media", "alta" }
        },
        ["variable_prioritaria"] = new
        {
          type = "string"
        },
        ["acciones"] = new
        {
          type = "array",
          minItems = 1,
          maxItems = 3,
          items = new
          {
            type = "string"
          }
        }
      },
      required = new[]
        {
                "resumen",
                "prioridad",
                "variable_prioritaria",
                "acciones"
            },
      additionalProperties = false
    };

    var cuerpo = new
    {
      model = modelo,
      messages = new[]
        {
                new
                {
                    role = "system",
                    content = instrucciones
                },
                new
                {
                    role = "user",
                    content = $"Interpreta estos datos del suelo: {datosAnalisis}"
                }
            },
      reasoning_effort = "low",
      response_format = new
      {
        type = "json_schema",
        json_schema = new
        {
          name = "interpretacion_suelo",
          strict = true,
          schema = esquema
        }
      }
    };

    using var solicitud = new HttpRequestMessage(
        HttpMethod.Post,
        "chat/completions");

    solicitud.Headers.Authorization =
        new AuthenticationHeaderValue("Bearer", apiKey);

    solicitud.Content = new StringContent(
        JsonSerializer.Serialize(cuerpo),
        Encoding.UTF8,
        "application/json");

    using var respuesta = await _httpClient.SendAsync(
        solicitud,
        cancellationToken);

    var contenido = await respuesta.Content.ReadAsStringAsync(
        cancellationToken);

    if (!respuesta.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
          $"Groq respondió con el código {(int)respuesta.StatusCode}: {contenido}");
    }

    using var documento = JsonDocument.Parse(contenido);

    var textoRespuesta = documento.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString();

    if (string.IsNullOrWhiteSpace(textoRespuesta))
    {
      throw new InvalidOperationException(
          "Groq no devolvió una interpretación.");
    }

    var interpretacion =
        JsonSerializer.Deserialize<InterpretacionIa>(
            textoRespuesta,
            OpcionesJson);

    if (interpretacion is null ||
        string.IsNullOrWhiteSpace(interpretacion.Resumen) ||
        interpretacion.Acciones.Count == 0)
    {
      throw new InvalidOperationException(
          "La respuesta de Groq no tiene el formato esperado.");
    }

    interpretacion.AnalisisId = analisis.AnalisisId;
    interpretacion.GeneradoPor = modelo;
    interpretacion.FechaGeneracion = DateTime.UtcNow;
    interpretacion.Advertencia =
        "Interpretación orientativa. No sustituye un análisis profesional de laboratorio.";

    return interpretacion;
  }
}
