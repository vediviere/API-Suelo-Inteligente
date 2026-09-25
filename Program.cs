using ApiSueloInteligente.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
    policy.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
  });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

var rutaJson = Path.Combine(AppContext.BaseDirectory, "Data", "Analisis.json");

if (!File.Exists(rutaJson))
{
  throw new FileNotFoundException("No se encontró el archivo Data/Analisis.json.");
}

var opciones = new JsonSerializerOptions
{
  PropertyNameCaseInsensitive = true
};

var json = File.ReadAllText(rutaJson);
var analisis = JsonSerializer.Deserialize<List<AnalisisSuelo>>(json, opciones) ?? [];

analisis = analisis
    .OrderByDescending(a => a.FechaProcesamiento)
    .ToList();

app.MapGet("/", () => Results.Ok(new
{
  nombre = "API Suelo Inteligente",
  version = "1.0",
  registros = analisis.Count
}));

app.MapGet("/health", () => Results.Ok(new
{
  estado = "Disponible"
}));

app.MapGet("/api/analisis", (int pagina = 1, int cantidad = 20) =>
{
  if (pagina < 1)
  {
    return Results.BadRequest(new { mensaje = "La página debe ser mayor que cero." });
  }

  if (cantidad < 1 || cantidad > 100)
  {
    return Results.BadRequest(new { mensaje = "La cantidad debe estar entre 1 y 100." });
  }

  var total = analisis.Count;
  var totalPaginas = (int)Math.Ceiling(total / (double)cantidad);

  var datos = analisis
      .Skip((pagina - 1) * cantidad)
      .Take(cantidad)
      .ToList();

  return Results.Ok(new
  {
    pagina,
    cantidad,
    total,
    totalPaginas,
    datos
  });
});

app.MapGet("/api/analisis/todos", () =>
{
  return Results.Ok(analisis);
});

app.MapGet("/api/analisis/ultimo", () =>
{
  var ultimo = analisis.FirstOrDefault();

  if (ultimo is null)
  {
    return Results.NotFound(new { mensaje = "No existen análisis registrados." });
  }

  return Results.Ok(ultimo);
});

app.MapGet("/api/analisis/{analisisId}", (string analisisId) =>
{
  var resultado = analisis.FirstOrDefault(a =>
      a.AnalisisId.Equals(analisisId, StringComparison.OrdinalIgnoreCase));

  if (resultado is null)
  {
    return Results.NotFound(new { mensaje = "Análisis no encontrado." });
  }

  return Results.Ok(resultado);
});

app.MapGet("/api/analisis/lectura/{lecturaId}", (string lecturaId) =>
{
  var resultado = analisis.FirstOrDefault(a =>
      a.LecturaId.Equals(lecturaId, StringComparison.OrdinalIgnoreCase));

  if (resultado is null)
  {
    return Results.NotFound(new { mensaje = "Lectura no encontrada." });
  }

  return Results.Ok(resultado);
});

app.Run();
