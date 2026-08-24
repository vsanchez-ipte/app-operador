using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Una incidencia de la cola con todo lo que hace falta para decidir e intentar su envío.
/// </summary>
/// <remarks>
/// <para>
/// <b>No es un <see cref="RegistroCola"/>.</b> Aquél es el resumen que pinta la pantalla —clase,
/// prioridad, descripción—; éste trae los datos que viajan a Jacob y el historial que decide
/// <b>si toca intentarlo ahora</b>.
/// </para>
/// <para>
/// Existe para que el caso de uso no conozca la fila de SQLite. La cola traduce; la orquestación
/// trabaja sobre este modelo.
/// </para>
/// </remarks>
/// <param name="Uuid">Llave de idempotencia del dispositivo.</param>
/// <param name="ClaveLocal">Identificador visible, <c>LOC-######</c>.</param>
/// <param name="TipoId">Tipo del catálogo, o <see langword="null"/> si trae una clave antigua.</param>
/// <param name="SeveridadId">Nivel de severidad de Jacob.</param>
/// <param name="Kilometro">Kilómetro en forma canónica <c>000+000</c>.</param>
/// <param name="FuenteKilometro">Si lo dio el GPS o lo escribió el operador.</param>
/// <param name="Nota">Nota del operador.</param>
/// <param name="CapturadaUtc">Cuándo capturó el operador, en UTC.</param>
/// <param name="SesionOrigen">Sesión de la que salió, si se conoce.</param>
/// <param name="Estado">Estado actual dentro de la cola.</param>
/// <param name="Intentos">Envíos ya intentados. Alimenta la espera creciente (CA 7).</param>
/// <param name="UltimoIntentoUtc">Cuándo se intentó por última vez.</param>
/// <param name="UltimoErrorCodigo">
/// Código del último rechazo. Es lo que distingue un fallo funcional —que no se reintenta
/// solo— de uno técnico, y <b>tiene que sobrevivir al cierre de la app</b> o el CA 8 se
/// incumpliría en cuanto el operador reabriera.
/// </param>
public sealed record IncidenciaEnviable(
	string Uuid,
	string ClaveLocal,
	int? TipoId,
	Guid? SeveridadId,
	string? Kilometro,
	KilometerSource FuenteKilometro,
	string Nota,
	DateTime CapturadaUtc,
	string SesionOrigen,
	EstadoSincronizacion Estado,
	int Intentos,
	DateTime UltimoIntentoUtc,
	string? UltimoErrorCodigo);

/// <summary>
/// Lo que hay que escribir en la cola después de intentar un envío.
/// </summary>
/// <remarks>
/// <b>Lo decide el caso de uso, no la cola.</b> Aplicar
/// <c>ReglaTransicionSincronizacion</c> es usar una regla de dominio, y eso pertenece a la
/// orquestación; escribir la fila pertenece a la persistencia. Con la decisión dentro del
/// repositorio, la regla quedaba enterrada donde nadie la busca.
/// </remarks>
/// <param name="Uuid">Registro que se actualiza.</param>
/// <param name="Estado">Estado al que pasa.</param>
/// <param name="Intentos">Contador de intentos ya incrementado, si toca.</param>
/// <param name="FolioCentral">Folio asignado por Jacob, si lo aceptó.</param>
/// <param name="UltimoErrorCodigo">Código del rechazo, o <see langword="null"/> si fue bien.</param>
public sealed record ActualizacionEnvio(
	string Uuid,
	EstadoSincronizacion Estado,
	int Intentos,
	string? FolioCentral,
	string? UltimoErrorCodigo);
