using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

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

	private static readonly CapacidadOperador[] ConPermisoPropio =
	[
		CapacidadOperador.RegistrarIncidencia,
		CapacidadOperador.AdjuntarEvidencia,
	];

	public static TheoryData<CapacidadOperador> CapacidadesSinPermisoPropio()
	{
		var datos = new TheoryData<CapacidadOperador>();
		foreach (var capacidad in Todas.Where(c => !ConPermisoPropio.Contains(c)))
		{
			datos.Add(capacidad);
		}

		return datos;
	}

	public static TheoryData<CapacidadOperador> CapacidadesConPermisoPropio()
	{
		var datos = new TheoryData<CapacidadOperador>();
		foreach (var capacidad in ConPermisoPropio)
		{
			datos.Add(capacidad);
		}

		return datos;
	}

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

	[Theory]
	[MemberData(nameof(CapacidadesSinPermisoPropio))]
	public void El_permiso_de_la_app_concede_las_que_no_tienen_permiso_propio(
		CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoAppOperadorMovil]);

		Assert.True(ReglaCapacidades.Concede(permisos, capacidad));
	}

	[Theory]
	[MemberData(nameof(CapacidadesConPermisoPropio))]
	public void El_permiso_de_la_app_ya_no_concede_las_de_captura(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoAppOperadorMovil]);

		Assert.False(ReglaCapacidades.Concede(permisos, capacidad));
	}

	[Theory]
	[MemberData(nameof(CapacidadesConPermisoPropio))]
	public void El_permiso_de_captura_concede_las_dos(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoCapturaIncidencias]);

		Assert.True(ReglaCapacidades.Concede(permisos, capacidad));
	}

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

	[Fact]
	public void El_permiso_de_captura_no_concede_las_demas()
	{
		var permisos = PermisosOperador.DelServidor([ReglaCapacidades.PermisoCapturaIncidencias]);

		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.ConsultarCola));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.Sincronizar));
	}

	[Fact]
	public void Un_permiso_especifico_concede_solo_su_capacidad()
	{
		var permisos = PermisosOperador.DelServidor(["APP_OPERADOR_COLA"]);

		Assert.True(ReglaCapacidades.Concede(permisos, CapacidadOperador.ConsultarCola));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.Sincronizar));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.RegistrarIncidencia));
		Assert.False(ReglaCapacidades.Concede(permisos, CapacidadOperador.AdjuntarEvidencia));
	}

	[Theory]
	[MemberData(nameof(Capacidades))]
	public void Un_permiso_desconocido_no_concede_nada(CapacidadOperador capacidad)
	{
		var permisos = PermisosOperador.DelServidor(["OTRO_MODULO"]);

		Assert.False(ReglaCapacidades.Concede(permisos, capacidad));
	}

	[Fact]
	public void La_caja_del_codigo_no_cambia_la_respuesta()
	{
		var permisos = PermisosOperador.DelServidor(["app_operador_movil"]);

		Assert.True(ReglaCapacidades.Concede(permisos, CapacidadOperador.Sincronizar));
	}
}
