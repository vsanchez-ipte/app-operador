using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class RepositorioEvidenciasSqliteTests
{
	private const string Incidencia = "11111111-1111-1111-1111-111111111111";
	private const string OtraIncidencia = "22222222-2222-2222-2222-222222222222";

	private static EvidenciaAdjunta Evidencia(
		string uuid,
		string incidenciaUuid = Incidencia,
		string nombre = "IMG_0001.jpg",
		EstadoSincronizacion estado = EstadoSincronizacion.Pendiente) =>
		new(uuid, incidenciaUuid, nombre, "image/jpeg", 482913, $"/privado/evidencias/{uuid}.jpg", estado);

	[Fact]
	public async Task UnaEvidenciaRegistrada_seLeeCompleta()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		await repositorio.AgregarAsync(Evidencia("ev-1"));

		var leida = await repositorio.ObtenerAsync("ev-1");

		Assert.NotNull(leida);
		Assert.Equal(Incidencia, leida.IncidenciaUuid);
		Assert.Equal("IMG_0001.jpg", leida.NombreOriginal);
		Assert.Equal("image/jpeg", leida.TipoMime);
		Assert.Equal(482913, leida.Bytes);
		Assert.Equal(EstadoSincronizacion.Pendiente, leida.Estado);
	}

	[Fact]
	public async Task ElNombreOriginal_sobreviveAlViajePorSqlite()
	{
		// Sin la columna del tamaño no se puede pintar la lista de adjuntos.
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		await repositorio.AgregarAsync(Evidencia("ev-1", nombre: "Choque carril alta.pdf"));

		Assert.Equal(
			"Choque carril alta.pdf",
			(await repositorio.ObtenerAsync("ev-1"))!.NombreOriginal);
	}

	[Fact]
	public async Task LasEvidenciasSeLeen_enOrdenDeCaptura()
	{
		// Como el operador las adjuntó, no por nombre: es como espera reconocerlas en la lista.
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		await repositorio.AgregarAsync(Evidencia("ev-1", nombre: "zeta.jpg"));
		await Task.Delay(2);
		await repositorio.AgregarAsync(Evidencia("ev-2", nombre: "alfa.jpg"));

		var lista = await repositorio.ObtenerDeIncidenciaAsync(Incidencia);

		Assert.Equal(["zeta.jpg", "alfa.jpg"], lista.Select(e => e.NombreOriginal));
	}

	[Fact]
	public async Task ElCupoCuenta_soloLasDeSuIncidencia()
	{
		// Si contara todas, la segunda incidencia del turno nacería con el cupo ya gastado.
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		await repositorio.AgregarAsync(Evidencia("ev-1"));
		await repositorio.AgregarAsync(Evidencia("ev-2"));
		await contexto.SembrarIncidenciaAsync(OtraIncidencia);
		await repositorio.AgregarAsync(Evidencia("ev-3", incidenciaUuid: OtraIncidencia));

		Assert.Equal(2, await repositorio.ContarDeIncidenciaAsync(Incidencia));
		Assert.Equal(1, await repositorio.ContarDeIncidenciaAsync(OtraIncidencia));
	}

	[Fact]
	public async Task Eliminar_seLlevaSoloLaSuya()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		await repositorio.AgregarAsync(Evidencia("ev-1"));
		await repositorio.AgregarAsync(Evidencia("ev-2"));

		await repositorio.EliminarAsync("ev-1");

		var lista = await repositorio.ObtenerDeIncidenciaAsync(Incidencia);
		Assert.Equal("ev-2", Assert.Single(lista).Uuid);
	}

	[Fact]
	public async Task LasEvidencias_sobrevivenAlCierreDeLaAplicacion()
	{
		// Lo documentado sin cobertura no se pierde porque el sistema cierre la app.
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);
		await new RepositorioEvidenciasSqlite(contexto.BaseDatos).AgregarAsync(Evidencia("ev-1"));

		var reabierta = contexto.ReabrirBaseDatos();
		var lista = await new RepositorioEvidenciasSqlite(reabierta)
			.ObtenerDeIncidenciaAsync(Incidencia);

		Assert.Single(lista);
		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task UnaIncidenciaSinEvidencias_devuelveListaVaciaYNoNulo()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		Assert.Empty(await repositorio.ObtenerDeIncidenciaAsync(Incidencia));
		Assert.Equal(0, await repositorio.ContarDeIncidenciaAsync(Incidencia));
		Assert.Null(await repositorio.ObtenerAsync("no-existe"));
	}

	// ---------- Evidencias pendientes del operador ----------

	[Fact]
	public async Task PendientesDelOperador_cruzaConSuIncidenciaYExcluyeLoSincronizadoYLoAjeno()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);

		// Dos incidencias del operador de la sesión y una de otro operador.
		var mia = await IncidenciaAsync(contexto, contexto.Sesion);
		var miaSegunda = await IncidenciaAsync(contexto, contexto.Sesion);
		var ajena = await IncidenciaAsync(contexto, new SesionFija("otro"));

		await repositorio.AgregarAsync(Evidencia("ev-1", mia.Uuid, "foto-1.jpg"));
		await repositorio.AgregarAsync(Evidencia("ev-2", mia.Uuid, "foto-2.jpg", EstadoSincronizacion.Fallido));
		await repositorio.AgregarAsync(Evidencia("ev-3", mia.Uuid, "subida.jpg", EstadoSincronizacion.Sincronizado));
		await repositorio.AgregarAsync(Evidencia("ev-4", miaSegunda.Uuid, "acta.pdf"));
		await repositorio.AgregarAsync(Evidencia("ev-5", ajena.Uuid, "de-otro.jpg"));

		var pendientes = await repositorio.ObtenerPendientesDelOperadorAsync("admin");

		// Lo fallido cuenta; lo sincronizado y lo de otro operador, no.
		Assert.Equal(["ev-1", "ev-2", "ev-4"], pendientes.Select(p => p.Uuid).ToArray());
		Assert.Equal(mia.ClaveLocal, pendientes[0].ClaveLocalIncidencia);
		Assert.Equal(miaSegunda.ClaveLocal, pendientes[2].ClaveLocalIncidencia);
		Assert.Equal(EstadoSincronizacion.Fallido, pendientes[1].Estado);
	}

	[Fact]
	public async Task PendientesDelOperador_traenElCodigoDelRechazo()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		var mia = await IncidenciaAsync(contexto, contexto.Sesion);
		await repositorio.AgregarAsync(Evidencia("ev-1", mia.Uuid));
		await repositorio.ActualizarEnvioAsync(
			"ev-1", EstadoSincronizacion.Fallido, "appevidencias.formato.nopermitido");

		var pendiente = Assert.Single(await repositorio.ObtenerPendientesDelOperadorAsync("admin"));

		Assert.Equal("appevidencias.formato.nopermitido", pendiente.UltimoErrorCodigo);
	}

	[Fact]
	public async Task PendientesDelOperador_sinOperadorNoDevuelveNada()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		await contexto.SembrarIncidenciaAsync(Incidencia);
		var mia = await IncidenciaAsync(contexto, contexto.Sesion);
		await repositorio.AgregarAsync(Evidencia("ev-1", mia.Uuid));

		Assert.Empty(await repositorio.ObtenerPendientesDelOperadorAsync(""));
		Assert.Empty(await repositorio.ObtenerPendientesDelOperadorAsync("nadie"));
	}

	[Fact]
	public async Task Rezagadas_soloLasSinConfirmarDeIncidenciasYaSincronizadasDelOperador()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();

		var sincronizada = await IncidenciaAsync(contexto, contexto.Sesion);
		var enCola = await IncidenciaAsync(contexto, contexto.Sesion);
		var ajena = await IncidenciaAsync(contexto, new SesionFija("otro"));
		await MarcarSincronizadaAsync(contexto, contexto.Sesion, sincronizada.Uuid);
		await MarcarSincronizadaAsync(contexto, new SesionFija("otro"), ajena.Uuid);

		await repositorio.AgregarAsync(Evidencia("ev-1", sincronizada.Uuid));
		await repositorio.AgregarAsync(Evidencia("ev-2", sincronizada.Uuid, estado: EstadoSincronizacion.Sincronizado));
		await repositorio.AgregarAsync(Evidencia("ev-3", enCola.Uuid));
		await repositorio.AgregarAsync(Evidencia("ev-4", ajena.Uuid));
		await repositorio.ActualizarEnvioAsync(
			"ev-1", EstadoSincronizacion.Fallido, "appincidencias.error.tecnico", "Sin red.");

		var rezagadas = await repositorio.ObtenerRezagadasDelOperadorAsync("admin");

		var unica = Assert.Single(rezagadas);
		Assert.Equal("ev-1", unica.Evidencia.Uuid);
		Assert.Equal(sincronizada.Uuid, unica.Evidencia.IncidenciaUuid);
		Assert.Equal("appincidencias.error.tecnico", unica.Evidencia.UltimoErrorCodigo);
		Assert.Equal(1, unica.Intentos);

		Assert.NotNull(unica.UltimoIntentoUtc);
		Assert.Equal(DateTimeKind.Utc, unica.UltimoIntentoUtc!.Value.Kind);
	}

	[Fact]
	public async Task Rezagadas_unaQueNuncaSeIntentoLlegaSinUltimoIntento()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		var sincronizada = await IncidenciaAsync(contexto, contexto.Sesion);
		await MarcarSincronizadaAsync(contexto, contexto.Sesion, sincronizada.Uuid);
		await repositorio.AgregarAsync(Evidencia("ev-1", sincronizada.Uuid));

		var unica = Assert.Single(await repositorio.ObtenerRezagadasDelOperadorAsync("admin"));

		Assert.Equal(0, unica.Intentos);
		Assert.Null(unica.UltimoIntentoUtc);
	}

	[Fact]
	public async Task LaTarjetaDeLaCola_cuentaLaEvidenciaQueNoLlego()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioEvidenciasSqlite(contexto.BaseDatos);
		await contexto.BaseDatos.InicializarAsync();
		var sincronizada = await IncidenciaAsync(contexto, contexto.Sesion);
		await MarcarSincronizadaAsync(contexto, contexto.Sesion, sincronizada.Uuid);
		await repositorio.AgregarAsync(Evidencia("ev-1", sincronizada.Uuid));
		await repositorio.AgregarAsync(Evidencia("ev-2", sincronizada.Uuid));

		var tarjeta = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());

		Assert.Equal(2, tarjeta.EvidenciasSinEnviar);
		Assert.NotNull(tarjeta.ReintentoEvidenciaUtc);
		Assert.True(tarjeta.HayEvidenciaSinEnviar);
	}

	private static Task MarcarSincronizadaAsync(ContextoSqlite contexto, ISessionStore sesion, string uuid) =>
		contexto.CrearColaDe(sesion).ActualizarEnvioAsync(
			new ActualizacionEnvio(uuid, EstadoSincronizacion.Sincronizado, 1, "INC-APK-2026-0015", null));

	private static async Task<(string Uuid, string ClaveLocal)> IncidenciaAsync(
		ContextoSqlite contexto, ISessionStore sesion)
	{
		var clave = await contexto.CrearRepositorioDe(sesion).GuardarAsync(
			new TipoIncidencia(11, "Objeto en camino"),
			Kilometer.Crear("130+200"),
			KilometerSource.Manual,
			new SeveridadIncidencia(Guid.NewGuid(), "Advertencia", 2, "#EDD611"),
			"nota");

		var enviable = await contexto.CrearColaDe(sesion).ObtenerEnviablePorClaveAsync(clave);
		return (enviable!.Uuid, clave);
	}
}
