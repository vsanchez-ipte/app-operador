using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// JTT-1385 CA 1, 3 y 4: qué autoriza la sesión del operador.
/// </summary>
public class ReglaCapacidadesTests
{
	private static readonly CapacidadOperador[] Todas =
	[
		CapacidadOperador.RegistrarIncidencia,
		CapacidadOperador.AdjuntarEvidencia,
		CapacidadOperador.ConsultarCola,
		CapacidadOperador.Sincronizar,
	];

	public static TheoryData<CapacidadOperador> Capacidades()
	{
		var datos = new TheoryData<CapacidadOperador>();
		foreach (var capacidad in Todas)
		{
			datos.Add(capacidad);
		}

		return datos;
	}

	/// <summary>
	/// Las dos que cubre el permiso de captura: registrar y adjuntar evidencia.
	/// </summary>
	private static readonly CapacidadOperador[] ConPermisoPropio =
	[
		CapacidadOperador.RegistrarIncidencia,
		CapacidadOperador.AdjuntarEvidencia,
	];

	/// <summary>
	/// Las que siguen bajo el permiso general, porque Jacob no las emite por separado.
	/// </summary>
	public static TheoryData<CapacidadOperador> CapacidadesSinPermisoPropio()
	{
		var datos = new TheoryData<CapacidadOperador>();
		foreach (var capacidad in Todas.Where(c => !ConPermisoPropio.Contains(c)))
		{
			datos.Add(capacidad);
		}

		return datos;
	}

	/// <summary>Las que exigen el permiso de captura.</summary>
	public static TheoryData<CapacidadOperador> CapacidadesConPermisoPropio()
	{
		var datos = new TheoryData<CapacidadOperador>();
		foreach (var capacidad in ConPermisoPropio)
		{
			datos.Add(capacidad);
		}

		return datos;
	}

	/// <summary>
	/// CA 4: sin sesión válida no se registra, no se adjunta, no se consulta y no se sincroniza.
	/// </summary>
	[Theory]
	[MemberData(nameof(Capacidades))]
	public void Sin_sesion_no_se_concede_nada(CapacidadOperador capacidad)
	{
		Assert.False(ReglaCapacidades.Concede(null, capacidad));
	}

	[Theory]
	[MemberData(nameof(Capacidades))]
	public void Sin_ningun_permiso_no_se_concede_nada(CapacidadOperador capacidad)
	{
		Assert.False(ReglaCapacidades.Concede(PermisosOperador.Ninguno, capacidad));
	}

	/// <summary>
	/// El permiso general sigue autorizando lo que Jacob no emite por separado: consultar la
	/// cola y sincronizar.
	/// </summary>
	[Theory]
	[MemberData(nameof(CapacidadesSinPermisoPropio))]
	public void El_permiso_de_la_app_concede_las_que_no_tienen_permiso_propio(
		CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoAppOperadorMovil]);

		Assert.True(ReglaCapacidades.Concede(permisos, capacidad));
	}

	/// <summary>
	/// La prueba que da sentido al permiso nuevo (decisión del 20-ago).
	/// </summary>
	/// <remarks>
	/// Antes el permiso general concedía las cuatro capacidades. Si siguiera haciéndolo, un
	/// operador con solo <c>APP_OPERADOR_MOVIL</c> capturaría igual y el permiso de captura no
	/// serviría de nada.
	/// </remarks>
	[Theory]
	[MemberData(nameof(CapacidadesConPermisoPropio))]
	public void El_permiso_de_la_app_ya_no_concede_las_de_captura(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoAppOperadorMovil]);

		Assert.False(ReglaCapacidades.Concede(permisos, capacidad));
	}

	/// <summary>
	/// El permiso de captura concede registrar <b>y</b> adjuntar evidencia: cubre las dos,
	/// porque la evidencia no existe sin la incidencia a la que se adjunta.
	/// </summary>
	[Theory]
	[MemberData(nameof(CapacidadesConPermisoPropio))]
	public void El_permiso_de_captura_concede_las_dos(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoCapturaIncidencias]);

		Assert.True(ReglaCapacidades.Concede(permisos, capacidad));
	}

	/// <summary>
	/// El caso real que va a devolver Jacob: los dos permisos juntos autorizan todo.
	/// </summary>
	[Theory]
	[MemberData(nameof(Capacidades))]
	public void Los_dos_permisos_juntos_conceden_todas(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor(
		[
			ReglaCapacidades.PermisoAppOperadorMovil,
			ReglaCapacidades.PermisoCapturaIncidencias,
		]);

		Assert.True(ReglaCapacidades.Concede(permisos, capacidad));
	}

	/// <summary>
	/// El permiso de captura no abre las demás: autoriza lo suyo y nada más.
	/// </summary>
	[Fact]
	public void El_permiso_de_captura_no_concede_las_demas()
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoCapturaIncidencias]);

		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.ConsultarCola));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.Sincronizar));
	}

	/// <summary>
	/// El camino que todavía no se ejercita contra Jacob: si algún día concede la capacidad por
	/// separado, se respeta sin tocar las pantallas.
	/// </summary>
	[Fact]
	public void Un_permiso_especifico_concede_solo_su_capacidad()
	{
		var permisos = PermisosOperador.DelServidor(["APP_OPERADOR_COLA"]);

		Assert.True(ReglaCapacidades.Concede(permisos, CapacidadOperador.ConsultarCola));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.Sincronizar));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.RegistrarIncidencia));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.AdjuntarEvidencia));
	}

	/// <summary>
	/// Un permiso ajeno no abre nada: lo que no venga del servidor con un código conocido no
	/// concede capacidades.
	/// </summary>
	[Theory]
	[MemberData(nameof(Capacidades))]
	public void Un_permiso_desconocido_no_concede_nada(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor(["OTRO_MODULO"]);

		Assert.False(ReglaCapacidades.Concede(permisos, capacidad));
	}

	/// <summary>
	/// Jacob emite los códigos en mayúsculas, pero una diferencia de caja no debe convertirse en
	/// una negativa: es el mismo criterio que ya aplica <see cref="PermisosOperador"/>.
	/// </summary>
	[Fact]
	public void La_caja_del_codigo_no_cambia_la_respuesta()
	{
		var permisos = PermisosOperador.DelServidor(["app_operador_movil"]);

		Assert.True(ReglaCapacidades.Concede(permisos, CapacidadOperador.Sincronizar));
	}
}
