using ApiSueloInteligente.Models;

namespace ApiSueloInteligente.Services;

public static class AnalizadorSuelo
{
  public static AnalisisLectura Procesar(LecturaSensor lectura)
  {
    var resultados = new List<ResultadoVariable>
    {
      CrearResultado("ph", "pH", lectura.Ph, "pH", 6, 7.5, 5.5, 8),
      CrearResultado("conductividad", "Conductividad", lectura.Conductividad, "dS/m", 0.8, 2, 0.4, 3),
      CrearResultado("humedad", "Humedad", lectura.Humedad, "%", 35, 60, 25, 70),
      CrearResultado("orp", "ORP", lectura.Orp, "mV", 200, 400, 100, 500),
      CrearResultado("temperatura", "Temperatura", lectura.Temperatura, "°C", 18, 28, 10, 35)
    };

    var criticos = resultados.Count(r => r.Estado == "critico");
    var advertencias = resultados.Count(r => r.Estado == "advertencia");

    var estado = criticos > 0
        ? "critico"
        : advertencias > 0
            ? "advertencia"
            : "optimo";

    var alertas = resultados
        .Where(r => r.Estado != "optimo")
        .Select(r => new AlertaLectura
        {
          Nivel = r.Estado,
          Variable = r.Nombre,
          Mensaje = r.Mensaje
        })
        .ToList();

    var recomendaciones = CrearRecomendaciones(alertas, lectura.Cultivo);

    return new AnalisisLectura
    {
      AnalisisId = $"ana-{Guid.NewGuid().ToString("N")[..8]}",
      LecturaId = lectura.LecturaId,
      FechaProcesamiento = DateTime.UtcNow,
      EstadoGeneral = estado,
      PuntajeGeneral = Math.Max(0, 100 - criticos * 25 - advertencias * 10),
      Resultados = resultados,
      Alertas = alertas,
      Recomendaciones = recomendaciones
    };
  }

  private static ResultadoVariable CrearResultado(string variable, string nombre, double valor, string unidad, double minimo, double maximo, double minimoAdvertencia, double maximoAdvertencia)
  {
    var estado = Evaluar(valor, minimo, maximo, minimoAdvertencia, maximoAdvertencia);

    var mensaje = estado switch
    {
      "optimo" => $"{nombre} se encuentra dentro del rango recomendado.",
      "advertencia" => $"{nombre} requiere seguimiento.",
      _ => $"{nombre} se encuentra fuera del rango seguro."
    };

    return new ResultadoVariable
    {
      Variable = variable,
      Nombre = nombre,
      Valor = valor,
      Unidad = unidad,
      Estado = estado,
      RangoRecomendado = new RangoRecomendado
      {
        Min = minimo,
        Max = maximo
      },
      Mensaje = mensaje
    };
  }

  private static string Evaluar(double valor, double minimo, double maximo, double minimoAdvertencia, double maximoAdvertencia)
  {
    if (valor >= minimo && valor <= maximo)
    {
      return "optimo";
    }

    if (valor >= minimoAdvertencia && valor <= maximoAdvertencia)
    {
      return "advertencia";
    }

    return "critico";
  }

  private static List<Recomendacion> CrearRecomendaciones(List<AlertaLectura> alertas, string cultivo)
  {
    var nombreCultivo = string.IsNullOrWhiteSpace(cultivo)
        ? "el cultivo"
        : cultivo.Trim();

    if (alertas.Count == 0)
    {
      return
      [
        new Recomendacion
      {
        Prioridad = "baja",
        Titulo = $"Mantener condiciones para {nombreCultivo}",
        Descripcion = $"Las variables de {nombreCultivo} se encuentran dentro de los rangos configurados. Se recomienda mantener el monitoreo periódico."
      }
      ];
    }

    var alertaPrincipal = alertas.FirstOrDefault(a => a.Nivel == "critico")
        ?? alertas[0];

    var accion = alertaPrincipal.Variable switch
    {
      "pH" => $"Verificar la acidez del suelo de {nombreCultivo} y considerar una corrección gradual después de confirmar la medición.",
      "Conductividad" => $"Revisar la acumulación de sales y la calidad del agua utilizada para el riego de {nombreCultivo}.",
      "Humedad" => $"Revisar la frecuencia y cantidad de riego aplicada a {nombreCultivo}, evitando tanto sequedad como saturación.",
      "ORP" => $"Verificar el drenaje y la aireación del suelo donde se encuentra {nombreCultivo}.",
      "Temperatura" => $"Revisar la exposición del suelo y considerar medidas de protección para las raíces de {nombreCultivo}.",
      _ => $"Revisar las condiciones actuales de {nombreCultivo} y realizar una nueva medición."
    };

    return
    [
      new Recomendacion
    {
      Prioridad = alertaPrincipal.Nivel == "critico" ? "alta" : "media",
      Titulo = $"Revisar {alertaPrincipal.Variable} en {nombreCultivo}",
      Descripcion = $"{alertaPrincipal.Mensaje} {accion}"
    }
    ];
  }
}
