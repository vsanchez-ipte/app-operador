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
	/// Hoy Jacob emite un solo permiso y autoriza la app entera. Es lo único que el servidor
	/// sabe decir: no existe un catálogo de capacidades finas.
	/// </summary>
	[Theory]
	[MemberData(nameof(Capacidades))]
	public void El_permiso_de_la_app_concede_todas(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoAppOperadorMovil]);

		Assert.True(ReglaCapacidades.Concede(permisos, capacidad));
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
