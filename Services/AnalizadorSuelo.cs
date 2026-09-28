using System.Globalization;
using System.Text;
using ApiSueloInteligente.Models;

namespace ApiSueloInteligente.Services;

public static class AnalizadorSuelo
{
  public static AnalisisLectura Procesar(LecturaSensor lectura, CatalogoRegional catalogo)
  {
    var cultivoRegional = BuscarCultivo(lectura.Cultivo, catalogo);
    var nombreCultivo = cultivoRegional?.NombreComun;

    if (string.IsNullOrWhiteSpace(nombreCultivo))
    {
      nombreCultivo = string.IsNullOrWhiteSpace(lectura.Cultivo)
          ? "el cultivo"
          : lectura.Cultivo.Trim();
    }

    var resultados = new List<ResultadoVariable>
    {
      CrearResultadoRegional("ph", "pH", lectura.Ph, "pH", "ph", cultivoRegional, nombreCultivo, lectura.Zona, 6, 6.75, 7.5, 5.5, 8),

      CrearResultadoRegional("conductividad", "Conductividad", lectura.Conductividad, "dS/m", "conductividad_electrica", cultivoRegional, nombreCultivo, lectura.Zona, 0.8, 1.4, 2, 0.4, 3),

      CrearResultadoRegional("humedad", "Humedad", lectura.Humedad, "%", "humedad", cultivoRegional, nombreCultivo, lectura.Zona, 35, 47.5, 60, 25, 70),

      CrearResultado("orp", "ORP", lectura.Orp, "mV", 200, 300, 400, 100, 500, nombreCultivo, lectura.Zona, false),

      CrearResultadoRegional("temperatura", "Temperatura", lectura.Temperatura, "°C", "temperatura", cultivoRegional, nombreCultivo, lectura.Zona, 18, 23, 28, 10, 35)
    };

    var criticos = resultados.Count(resultado => resultado.Estado == "critico");
    var advertencias = resultados.Count(resultado => resultado.Estado == "advertencia");

    var estado = criticos > 0
        ? "critico"
        : advertencias > 0
            ? "advertencia"
            : "optimo";

    var alertas = resultados
        .Where(resultado => resultado.Estado != "optimo")
        .Select(resultado => new AlertaLectura
        {
          Nivel = resultado.Estado,
          Variable = resultado.Nombre,
          Mensaje = resultado.Mensaje
        })
        .ToList();

    var recomendaciones = CrearRecomendaciones(alertas, nombreCultivo);

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

  private static CultivoRegional? BuscarCultivo(string cultivo, CatalogoRegional catalogo)
  {
    var cultivoNormalizado = NormalizarCultivo(cultivo);

    if (string.IsNullOrWhiteSpace(cultivoNormalizado))
    {
      return null;
    }

    if (catalogo.Cultivos.TryGetValue(cultivoNormalizado, out var cultivoEncontrado))
    {
      return cultivoEncontrado;
    }

    return catalogo.Cultivos
        .FirstOrDefault(elemento =>
            NormalizarCultivo(elemento.Key) == cultivoNormalizado ||
            NormalizarCultivo(elemento.Value.NombreComun) == cultivoNormalizado)
        .Value;
  }

  private static ResultadoVariable CrearResultadoRegional(
      string variable,
      string nombre,
      double valor,
      string unidad,
      string claveRegional,
      CultivoRegional? cultivo,
      string nombreCultivo,
      string zona,
      double minimoGeneral,
      double optimoGeneral,
      double maximoGeneral,
      double minimoAdvertenciaGeneral,
      double maximoAdvertenciaGeneral)
  {
    if (cultivo is not null && cultivo.Rangos.TryGetValue(claveRegional, out var rango))
    {
      var amplitud = rango.Max - rango.Min;
      var margenAdvertencia = Math.Max(amplitud * 0.25, 0.1);
      var minimoAdvertencia = rango.Min - margenAdvertencia;
      var maximoAdvertencia = rango.Max + margenAdvertencia;

      if (claveRegional != "temperatura")
      {
        minimoAdvertencia = Math.Max(0, minimoAdvertencia);
      }

      return CrearResultado(
          variable,
          nombre,
          valor,
          unidad,
          rango.Min,
          rango.Optimo,
          rango.Max,
          minimoAdvertencia,
          maximoAdvertencia,
          nombreCultivo,
          zona,
          true);
    }

    return CrearResultado(
        variable,
        nombre,
        valor,
        unidad,
        minimoGeneral,
        optimoGeneral,
        maximoGeneral,
        minimoAdvertenciaGeneral,
        maximoAdvertenciaGeneral,
        nombreCultivo,
        zona,
        false);
  }

  private static ResultadoVariable CrearResultado(
      string variable,
      string nombre,
      double valor,
      string unidad,
      double minimo,
      double optimo,
      double maximo,
      double minimoAdvertencia,
      double maximoAdvertencia,
      string nombreCultivo,
      string zona,
      bool usaRangoRegional)
  {
    var estado = Evaluar(valor, minimo, maximo, minimoAdvertencia, maximoAdvertencia);
    var condicion = ObtenerCondicion(valor, minimo, maximo);
    var diferencia = CalcularDiferencia(valor, minimo, maximo);

    var referencia = usaRangoRegional
        ? $" para {nombreCultivo} en {zona}"
        : string.Empty;

    var mensaje = condicion switch
    {
      "bajo" =>
          $"{nombre} está {diferencia:0.##} {unidad} por debajo del rango recomendado{referencia}.",

      "alto" =>
          $"{nombre} supera en {diferencia:0.##} {unidad} el rango recomendado{referencia}.",

      _ =>
          $"{nombre} se encuentra dentro del rango recomendado{referencia}."
    };

    return new ResultadoVariable
    {
      Variable = variable,
      Nombre = nombre,
      Valor = valor,
      Unidad = unidad,
      Estado = estado,
      Condicion = condicion,
      DiferenciaParaRango = diferencia,
      RangoRecomendado = new RangoRecomendado
      {
        Min = minimo,
        Optimo = optimo,
        Max = maximo
      },
      Mensaje = mensaje
    };
  }

  private static string ObtenerCondicion(double valor, double minimo, double maximo)
  {
    if (valor < minimo)
    {
      return "bajo";
    }

    if (valor > maximo)
    {
      return "alto";
    }

    return "optimo";
  }

  private static double CalcularDiferencia(double valor, double minimo, double maximo)
  {
    if (valor < minimo)
    {
      return Math.Round(minimo - valor, 2);
    }

    if (valor > maximo)
    {
      return Math.Round(valor - maximo, 2);
    }

    return 0;
  }

  private static string Evaluar(
      double valor,
      double minimo,
      double maximo,
      double minimoAdvertencia,
      double maximoAdvertencia)
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
    if (alertas.Count == 0)
    {
      return
      [
        new Recomendacion
        {
          Prioridad = "baja",
          Titulo = $"Mantener condiciones para {cultivo}",
          Descripcion = $"Las variables de {cultivo} están dentro de los rangos recomendados. Se recomienda mantener el monitoreo periódico."
        }
      ];
    }

    var alertaPrincipal = alertas.FirstOrDefault(alerta => alerta.Nivel == "critico") ?? alertas[0];

    var accion = alertaPrincipal.Variable switch
    {
      "pH" => $"Verificar la acidez del suelo antes de sembrar {cultivo}.",
      "Conductividad" => $"Revisar la acumulación de sales y la calidad del agua para {cultivo}.",
      "Humedad" => $"Ajustar la frecuencia y cantidad de riego para {cultivo}.",
      "ORP" => $"Verificar el drenaje y la aireación del suelo destinado a {cultivo}.",
      "Temperatura" => $"Revisar la exposición y protección del suelo destinado a {cultivo}.",
      _ => $"Revisar las condiciones del suelo y realizar una nueva medición."
    };

    return
    [
      new Recomendacion
      {
        Prioridad = alertaPrincipal.Nivel == "critico" ? "alta" : "media",
        Titulo = $"Revisar {alertaPrincipal.Variable} para {cultivo}",
        Descripcion = $"{alertaPrincipal.Mensaje} {accion}"
      }
    ];
  }

  private static string NormalizarCultivo(string? cultivo)
  {
    if (string.IsNullOrWhiteSpace(cultivo))
    {
      return string.Empty;
    }

    var texto = cultivo.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
    var resultado = new StringBuilder();

    foreach (var caracter in texto)
    {
      var categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);

      if (categoria == UnicodeCategory.NonSpacingMark)
      {
        continue;
      }

      if (char.IsLetterOrDigit(caracter))
      {
        resultado.Append(caracter);
        continue;
      }

      if (resultado.Length > 0 && resultado[^1] != '_')
      {
        resultado.Append('_');
      }
    }

    return resultado.ToString().Trim('_').Normalize(NormalizationForm.FormC);
  }
}
