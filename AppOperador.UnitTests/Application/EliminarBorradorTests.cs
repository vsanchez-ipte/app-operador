using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Eliminar un borrador se lleva sus adjuntos, archivo y fila, antes que la fila del borrador.
/// </summary>
/// <remarks>
/// Antes del esquema 11 borrar solo el borrador dejaba evidencias huérfanas; ahora la base lo
/// rechaza, y este caso de uso es quien pone las cosas en el orden que la llave exige. Y deja
/// una línea en la bitácora que dice cuántas se fueron con él.
/// </remarks>
public sealed class EliminarBorradorTests
{
	private const string Clave = "LOC-673530";
	private const string Uuid = "i-4";

	private readonly IIncidentRepository _incidencias = Substitute.For<IIncidentRepository>();
	private readonly IRepositorioEvidencias _evidencias = Substitute.For<IRepositorioEvidencias>();
	private readonly IAlmacenEvidencias _almacen = Substitute.For<IAlmacenEvidencias>();
	private readonly IAuditLog _bitacora = Substitute.For<IAuditLog>();
	private readonly List<string> _orden = [];

	private EliminarBorrador Crear()
	{
		_incidencias.EliminarBorradorAsync(Clave, Arg.Any<CancellationToken>())
			.Returns(_ => { _orden.Add("borrador"); return true; });
		_evidencias.EliminarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ci => { _orden.Add("fila " + ci.Arg<string>()); return Task.CompletedTask; });
		_almacen.EliminarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ci => { _orden.Add("archivo " + ci.Arg<string>()); return Task.CompletedTask; });
		_bitacora.RegistrarAsync(
				Arg.Any<OperacionAuditada>(), Arg.Any<ResultadoAuditoria>(), Arg.Any<string>(),
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(ci => { _orden.Add("bitácora: " + ci.ArgAt<string>(2)); return Task.CompletedTask; });

		return new EliminarBorrador(
			_incidencias,
			_evidencias,
			new QuitarEvidencia(_evidencias, _almacen, Substitute.For<IAuditLog>()),
			_bitacora);
	}

	private void ConBorrador(params EvidenciaAdjunta[] adjuntas)
	{
		_incidencias.ObtenerBorradorAsync(Clave, Arg.Any<CancellationToken>())
			.Returns(new BorradorIncidencia(Uuid, Clave, null, null, null, string.Empty));
		_evidencias.ObtenerDeIncidenciaAsync(Uuid, Arg.Any<CancellationToken>())
			.Returns(adjuntas);
		foreach (var adjunta in adjuntas)
		{
			_evidencias.ObtenerAsync(adjunta.Uuid, Arg.Any<CancellationToken>()).Returns(adjunta);
		}
	}

	private static EvidenciaAdjunta Evidencia(string uuid) =>
		new(uuid, Uuid, $"{uuid}.jpg", "image/jpeg", 10, $"/privado/{uuid}.jpg", EstadoSincronizacion.Pendiente);

	[Fact]
	public async Task Quita_cada_adjunto_archivo_y_fila_antes_que_el_borrador()
	{
		ConBorrador(Evidencia("e-1"), Evidencia("e-2"));

		Assert.True(await Crear().EjecutarAsync(Clave));

		Assert.Equal(
			[
				"archivo /privado/e-1.jpg", "fila e-1", "archivo /privado/e-2.jpg", "fila e-2", "borrador",
				"bitácora: Borrador LOC-673530 eliminado con sus 2 evidencias adjuntas.",
			],
			_orden);
	}

	[Fact]
	public async Task Sin_adjuntos_solo_elimina_el_borrador()
	{
		ConBorrador();

		Assert.True(await Crear().EjecutarAsync(Clave));

		Assert.Equal(["borrador", "bitácora: Borrador LOC-673530 eliminado."], _orden);
	}

	[Fact]
	public async Task Con_un_solo_adjunto_la_bitacora_lo_dice_en_singular()
	{
		ConBorrador(Evidencia("e-1"));

		Assert.True(await Crear().EjecutarAsync(Clave));

		Assert.Equal(
			"bitácora: Borrador LOC-673530 eliminado con su evidencia adjunta.",
			_orden[^1]);
	}

	[Fact]
	public async Task Si_la_fila_del_borrador_no_se_elimino_no_se_registra_nada()
	{
		// Otro hilo lo borró entre leerlo y eliminarlo: sin fila que se fuera, no hay línea.
		ConBorrador();
		var eliminar = Crear();
		_incidencias.EliminarBorradorAsync(Clave, Arg.Any<CancellationToken>()).Returns(false);

		Assert.False(await eliminar.EjecutarAsync(Clave));

		await _bitacora.DidNotReceiveWithAnyArgs()
			.RegistrarAsync(default, default, default!, default, default, default);
	}

	[Fact]
	public async Task Si_el_borrador_no_es_del_operador_no_toca_nada()
	{
		// El repositorio no devuelve borradores ajenos (JTT-1388 CA 9), y sin borrador no hay
		// nada que quitar: ni siquiera se pregunta por evidencias.
		_incidencias.ObtenerBorradorAsync(Clave, Arg.Any<CancellationToken>())
			.Returns((BorradorIncidencia?)null);

		Assert.False(await Crear().EjecutarAsync(Clave));

		Assert.Empty(_orden);
		await _evidencias.DidNotReceiveWithAnyArgs().ObtenerDeIncidenciaAsync(default!, default);
		await _bitacora.DidNotReceiveWithAnyArgs()
			.RegistrarAsync(default, default, default!, default, default, default);
	}
}
